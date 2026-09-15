# Host Bootstrap, Authentication Architecture, and API Defenses

## 1. Host Bootstrap Workflow

### Initial Setup
The application provides a fail-closed, one-time bootstrap mechanism to provision the initial administrative host account without hardcoding credentials in source control or leaving perpetual backdoors.

1. **Configure Environment Variables**:
   In your `.env` file (or orchestrator secrets), configure the bootstrap parameters:
   ```env
   HOST_SEED_ENABLED=true
   HOST_SEED_USERNAME=IEEEXtreme Section
   HOST_SEED_PASSWORD=replace_with_strong_host_password_here
   ```

2. **Complexity and Validation Rules**:
   When `HOST_SEED_ENABLED` is `true`:
   - Both username and password must be non-empty.
   - The password must be at least **12 characters** in length and contain:
     - At least one uppercase letter (`A-Z`)
     - At least one lowercase letter (`a-z`)
     - At least one digit (`0-9`)
     - At least one special character (non-alphanumeric)
   - If any requirement fails, the application fails closed on startup with a descriptive `InvalidOperationException`.
   - Password and secret values are **never logged** or included in exception messages.

3. **Idempotency**:
   - On startup, `DatabaseSeederHostedService` checks if a host with the given username already exists.
   - If the host exists, the service logs `Bootstrap host '{Username}' already exists; skipping host creation.` and cleanly terminates without modifying existing records or passwords.
   - If the host does not exist, the account is created with a PBKDF2/Argon2 password hash via `IPasswordHasher`.

4. **Post-Bootstrap Hygiene**:
   - Once the host account has been created, set `HOST_SEED_ENABLED=false` or remove the variable in `.env` and production environments.
   - When `HOST_SEED_ENABLED` is `false`, the hosted service logs `Host bootstrap seeding is disabled ('Seeding:Host:Enabled' is false); skipping.` and performs no database queries or validations.

---

## 2. Authentication Architecture

### In-Memory Access Tokens
- Host JWT access tokens (15-minute validity) are stored exclusively in application memory (`authService.ts`).
- Access tokens are never placed in `localStorage` or `sessionStorage`, mitigating cross-site scripting (XSS) exfiltration attacks.
- SignalR connections obtain the in-memory token on handshake via `accessTokenFactory`.

### HTTP-Only Refresh Token Cookie
- Upon successful login (`POST /api/auth/login`), the server issues an HTTP-only cookie:
  - **Name**: `kahoot_refresh_token`
  - **Path**: `/api/auth`
  - **SameSite**: `Lax` (or `None` with `Secure` in cross-site setups)
  - **HttpOnly**: `true` (inaccessible to JavaScript `document.cookie`)
  - **Secure**: `true` in production environments
- The HTTP response body returns `HostAuthResponse` containing `accessToken`, `accessTokenExpiresAt`, `hostId`, and `username`, omitting the refresh token secret from the JSON payload.

### Single-Flight Refresh Coordinator
- An Axios response interceptor (`axiosClient.ts`) catches HTTP 401 Unauthorized responses from host-facing APIs.
- Concurrent failing requests are queued behind a single in-flight `POST /api/auth/refresh` request.
- Once the refresh call succeeds, the queued requests are retried with the newly minted access token.
- If refresh fails, the queue is rejected, in-memory state is cleared, and users are redirected to `/login` without infinite redirect loops.

### Cross-Tab Synchronization
- Multi-tab state is synchronized via standard `BroadcastChannel('kahoot_auth_channel')` with fallback to `storage` events.
- When a user logs in or logs out in one browser tab, all other open tabs receive the notification, update their local auth context, or redirect to login.

### Family-Scoped Refresh Rotation & Reuse Detection
- **Family Grouping**: Each initial login generates a unique `FamilyId` (`Guid`).
- **Token Rotation**: Every call to `/api/auth/refresh` revokes the incoming token, generates a new token in the same `FamilyId`, and sets the updated cookie.
- **Race Tolerance Window**:
  - To prevent spurious logouts caused by multiple tabs refreshing simultaneously, a **10-second grace window** is maintained after a token is revoked.
  - If a sibling tab presents a token that was revoked within the last 10 seconds, the backend returns `409 Conflict` with error code `Auth.RefreshRace`.
  - The client treats `409 Conflict` gracefully by awaiting the concurrent refresh rather than logging the user out.
- **Theft / Reuse Detection**:
  - If a refresh token is presented that was revoked **outside** the 10-second grace window, the backend identifies malicious token reuse.
  - The entire token family (`FamilyId`) is immediately revoked in the database, neutralizing compromised credentials across all attacker and victim sessions.
- **Explicit Logout**:
  - Calling `POST /api/auth/logout` looks up the token's `FamilyId`, marks all active tokens in that family as revoked, and clears the client cookie with `MaxAge = 0`.

### Bounded Background Cleanup
- `TokenCleanupHostedService` runs in the backend every hour.
- Expired or revoked refresh tokens are purged in bounded batches (`limit = 100`) to avoid long-lived database locks and prevent unbounded table growth over time.

### Player Game Session Isolation
- Player session tokens (`kahoot_player_session_*`, `kahoot_pin_session_*`) are isolated to `sessionStorage`.
- Players retain session persistence across page reloads within the same browser tab, but sessions do not leak across distinct browser tabs or permanently persist after browser closure.
- Legacy `localStorage` keys are automatically migrated and removed on read or startup.

---

## 3. Proxy Trust and API Defenses

### Forwarded Headers & Anti-Spoofing
- In `nginx/default.conf` and `nginx/default.prod.conf`, incoming client forwarding headers are overwritten:
  ```nginx
  proxy_set_header X-Forwarded-For $remote_addr;
  proxy_set_header X-Forwarded-Proto $scheme;
  ```
- In ASP.NET Core (`Program.cs`), `ForwardedHeadersOptions` enforces `ForwardLimit = 1` and registers trusted internal networks:
  - Loopback (`127.0.0.1/32`, `::1/128`)
  - Docker subnets (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`)

### NAT-Friendly Rate Limiting
- **Authentication Endpoints (`/api/auth/*`)**:
  - Limit: 10 requests per 5-minute fixed window.
  - Partitioning: Keyed on `ClientIp + Username` (read via non-destructive JSON request buffering). Users sharing an organizational NAT/proxy cannot be locked out by a single malicious user attempting incorrect passwords on another username.
- **Game Join Endpoint (`/api/games/join`)**:
  - Limit: Token bucket with 1,200 token capacity and 600 replenishment tokens per 10 seconds.
  - Allows full classrooms (500+ participants) behind a single school IP to join synchronously without receiving HTTP 429 rejections.
- **RFC 7807 429 ProblemDetails**:
  - When rate limits are reached, the server responds with standard `application/problem+json`:
  - Includes HTTP header `Retry-After: <seconds>`.
  - JSON payload contains `type`, `title`, `status: 429`, `detail` specifying retry wait time, `instance`, and `correlationId`.

---

## 4. Health Endpoints

- `GET /health/live`: Process liveness check. Always returns `200 OK` (`Healthy`) if the web server process is responsive.
- `GET /health/ready`: Dependency readiness check. Validates PostgreSQL connectivity (with 3-second timeout) and uploads directory write access. Returns `200 OK` (`Healthy`) when ready to accept traffic, or `503 Service Unavailable` (`Unhealthy`) if critical dependencies are down.
- `GET /health`: Backward-compatible composite endpoint running all registered health checks.
