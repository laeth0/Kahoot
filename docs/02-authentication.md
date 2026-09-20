# 02. Authentication and Credential Lifecycle

This document defines the normative requirements for account credentials, System Administrator bootstrap, normal Host self-registration, user login, token refresh rotation, authenticated password changes, session revocation, and scalable background token cleanup. It combines both functional flows and non-functional specifications (performance SLOs, timing-attack defenses, multi-dimensional rate limiting, and concurrency rules) into a single unified specification.

---

## 1. Topic Overview & Actors

Authentication manages credentials and cryptographic session authority for two distinct account types:
* **System Administrator**: Provisioned via deployment bootstrap or created by an existing administrator; possesses platform administration privileges.
* **Registered User / Host**: Self-registers; owns exactly one tenant boundary.
* **Anonymous Visitor**: Public client performing registration or login.
* *(Players do NOT use account credentials; player sessions are defined in [07-joining-and-lobby.md](07-joining-and-lobby.md)).*

### 1.1 Credential Standards `[NORMATIVE]`
* **`AUTH-CRED-001` (Username Contract)**: Required, 3–64 characters evaluated post-Unicode NFKC normalization and culture-independent case folding (`NormalizedUsername`). Globally unique across both normal and administrator accounts.
* **`AUTH-CRED-002` (Password Contract)**: Required, 12–128 characters. Must contain at least one uppercase letter, one lowercase letter, one numeric digit, and one non-alphanumeric special character.
* **`AUTH-CRED-003` (Zero Normalization of Passwords)**: Passwords must **never** be trimmed, lowercased, or Unicode-normalized. Passwords must be validated and processed byte-for-byte as entered.
* **`AUTH-CRED-004` (No Alternative Auth)**: Authentication strictly uses `username + password`. No email address, Tenant ID input, social login, or SSO is supported.

---

## 2. Functional Specification & Workflows

### 2.1 Initial System Administrator Bootstrap `[NORMATIVE]`
* **`AUTH-BOOT-001` (Bootstrap Activation)**: Triggered by system startup when bootstrap configuration is enabled (`BOOTSTRAP_ADMIN_ENABLED=true`).
* **`AUTH-BOOT-002` (Bootstrap Processing)**:
  * Reads bootstrap username and password from secure environment variables.
  * Validates credential length and complexity per Section 1.1.
  * Checks for an existing administrator account with matching normalized username.
  * If absent: securely hashes password and creates an active System Administrator account.
  * If already exists: performs **no overwrite** and logs a safe startup notification.
  * If a normal Host account already exists with that username: **fails startup closed** with an error.
* **`AUTH-BOOT-003` (Post-Bootstrap Hardening)**: Following deployment, operators disable bootstrap configuration. Additional administrators are created via protected administrative APIs.

### 2.2 Normal User Self-Registration `[NORMATIVE]`
* **`AUTH-REG-001` (Registration Flow)**:
  * **Endpoint**: `POST /api/auth/register`
  * **Payload**: `{ "username": "TeacherJane", "password": "..." }`
  * **Processing**:
    * Validates username (3–64 chars) and password (12–128 chars).
    * Computes `NormalizedUsername` via Unicode NFKC normalization + culture-independent case folding.
    * Atomically in a single database transaction:
      * Enforces global uniqueness on `NormalizedUsername`.
      * Securely hashes password using Argon2id / BCrypt.
      * Inserts account with `AccountKind = Host`, `Status = Active`, and `TokenSecurityVersion = 1`.
      * Creates the associated tenant boundary.
  * **Response**: `201 Created` with `{ "accountId": "acc_...", "username": "TeacherJane" }`.
  * **Rule**: Registration does **not** issue session tokens or log the user in; client must initiate an explicit login.

### 2.3 Login Flow `[NORMATIVE]`
* **`AUTH-LOGIN-001` (Login Verification)**:
  * **Endpoint**: `POST /api/auth/login`
  * **Payload**: `{ "username": "TeacherJane", "password": "..." }`
  * **Processing**:
    * Evaluates rate limiting (IP bucket, username backoff, global hash concurrency).
    * Looks up account by `NormalizedUsername`.
    * If account exists: verifies password against stored hash.
    * If account does **not** exist: executes dummy password verification (see Section 5.1).
    * Verifies account `Status == Active`. If `Suspended`, rejects with generic failure code.
    * On success:
      * Generates a signed JWT access token (lifetime: 15 minutes) containing `accountId`, `tenantId`, `role`, and `tokenSecurityVersion`.
      * Generates a high-entropy cryptographic refresh token (sliding window: 14 days; absolute family cap: 30 days).
      * Stores the SHA-256 hash of the refresh token with a new `TokenFamilyId`.
      * Sets the HttpOnly refresh cookie:
        ```text
        Set-Cookie: kahoot_refresh_token=<raw_token>; Path=/api/auth; Secure; HttpOnly; SameSite=Lax; Max-Age=1209600
        ```
  * **Response**: `200 OK` with `{ "accountId": "...", "username": "...", "accountKind": "Host", "tenantId": "...", "accessToken": "...", "expiresIn": 900 }`.

### 2.4 Refresh Token Rotation & Race Grace Contract `[NORMATIVE]`
* **`AUTH-REF-001` (Refresh Endpoint & CSRF)**:
  * **Endpoint**: `POST /api/auth/refresh`
  * **Credential Input**: Read from `kahoot_refresh_token` cookie (body fallback permitted only when cookie is absent).
  * **Security Contract**: Requires valid `X-CSRF-Token` header and validates allowed `Origin` / `Referer`.
* **`AUTH-ROT-001` (Rotation Execution)**:
  * Hashes incoming raw token using SHA-256 and retrieves record from database.
  * Verifies current server time is strictly before `ExpiresAt` and token is not revoked.
  * Verifies absolute family lifetime: `CurrentUtcTime < FamilyCreatedAt + 30 days`. If expired, returns `401 Auth.InvalidRefreshToken`.
  * Atomically within a single transaction:
    * Marks presented token as consumed/rotated (`RotatedAt = NOW()`).
    * Generates replacement refresh token within the **same** `TokenFamilyId`.
    * Persists hash of replacement token.
    * Issues new 15-minute access token and new 14-day refresh cookie.
* **`AUTH-ROT-002` (Rotation Race Grace vs. Reuse Invariant)**:
  * **Grace Window ($\Delta t < 10\text{ seconds}$)**:
    If a client presents a token that was already consumed within the last **10 seconds**, the server returns `409 Auth.RefreshRace`.
    * **Family Preservation**: The token family remains valid.
    * **Security Guarantee**: The replaying request does **NOT** receive the replacement secret (the server cannot determine if the replay is a multi-tab race or an intercepted token).
    * **Frontend Recovery**: The client waits for the in-flight rotation response to complete.
  * **Reuse Detection ($\Delta t \ge 10\text{ seconds}$)**:
    If an already-consumed token is presented at or after 10 seconds from rotation:
    * Server identifies this as **Malicious Token Reuse**.
    * Immediately revokes the **entire token family**, increments account `TokenSecurityVersion`, logs a security audit event, and returns `401 Auth.RefreshTokenReuse`.

### 2.5 Logout and Global Revocation `[NORMATIVE]`
* **`AUTH-LOG-001` (Logout)**:
  * **Endpoint**: `POST /api/auth/logout`
  * **Security Contract**: Requires valid `X-CSRF-Token` header and validates allowed `Origin` when cookie is present.
  * **Processing**: Atomically revokes all tokens belonging to the identified token family and clears the cookie (`Max-Age=0`). Returns `204 No Content` (returns 204 even if token was missing or unknown to prevent enumeration).
* **`AUTH-LOG-002` (Logout All Devices)**:
  * **Endpoint**: `POST /api/auth/logout-all`
  * **Authorization**: Authenticated Host or System Administrator.
  * **Processing**: Atomically revokes **every active refresh token family** for the account, increments `TokenSecurityVersion` (invalidating all outstanding JWTs within $\le 100\text{ ms}$ across all nodes), and clears local cookie. Returns `204 No Content`.
* **`AUTH-PASS-001` (Password Change)**:
  * **Endpoint**: `POST /api/auth/change-password`
  * **Payload**: `{ "currentPassword": "...", "newPassword": "..." }`
  * **Processing**: Verifies current password; validates new password; hashes new password; atomically updates hash, revokes **all** refresh token families, and increments `TokenSecurityVersion`. All active SignalR sockets are severed. Returns `204 No Content`.

### 2.6 Scalable Background Refresh-Token Cleanup Worker `[NORMATIVE]`
* **`AUTH-CLEAN-001` (Cleanup Eligibility & Evidence Retention)**:
  A refresh token record is eligible for permanent deletion if and only if:
  ```text
  CurrentUtcTime >= Max(ExpiresAt, RevokedAt) + 7 days
  ```
  *(Tokens belonging to a revoked family are retained for 7 days post-invalidation to preserve replay detection evidence).*
* **`AUTH-CLEAN-002` (Batching & Drain SLO)**:
  * Operates with bounded batches: $\le 500$ rows per transaction.
  * Employs non-blocking locks with short yield intervals between batches.
  * Guaranteed drain capacity: $\ge 20,000$ eligible records per hour.
  * Maximum scheduling delay: background pass executes at least once every 15 minutes.

### 2.7 Secret & Signing-Key Rotation `[NORMATIVE]`
* **`AUTH-ROT-003` (Signing Key Rotation)**:
  * The system supports rotating JWT signing keys using a Key Identifier (`kid`) header.
  * When key rotation occurs, the server maintains the prior signing key as valid for signature verification during a grace transition period equal to the access token lifetime (15 minutes).
  * In the absence of a dual-key `kid` mechanism, rotating the signing key intentionally invalidates all outstanding access tokens immediately, requiring clients to refresh.

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Input Boundaries Table

| Parameter | Min - 1 | Min | Normal | Max | Max + 1 | Notes | Stable Req ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Username** | 2 (400) | 3 (201) | 12 (201) | 64 (201) | 65 (400) | Validated post-NFKC normalization. | `AUTH-BOUND-001` |
| **Password** | 11 (400) | 12 (201) | 24 (201) | 128 (201) | 129 (400) | Evaluated byte-for-byte; zero trimming. | `AUTH-BOUND-002` |
| **Rotation Race Window** | 9.99s (409) | 10.0s (401) | - | - | - | $< 10\text{s} \implies \text{Race}$; $\ge 10\text{s} \implies \text{Reuse}$. | `AUTH-BOUND-003` |
| **Absolute Family Cap**| 29.9d (Active)| 30.0d (Expired)| - | - | - | Max absolute family lifetime. | `AUTH-BOUND-004` |

### 3.2 Canonical Negative Error Codes

| HTTP Status | Error Code | Meaning / Trigger | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **400** | `Validation.Failed` | Password complexity failure, username length violation, or malformed body. | `AUTH-ERR-001` |
| **401** | `Auth.InvalidCredentials` | Generic error for incorrect password, non-existent username, or suspended account. | `AUTH-ERR-002` |
| **401** | `Auth.InvalidRefreshToken` | Refresh token expired, malformed, revoked, or missing. | `AUTH-ERR-003` |
| **401** | `Auth.RefreshTokenReuse` | Previously consumed refresh token presented at or after 10-second grace window. | `AUTH-ERR-004` |
| **403** | `Auth.Forbidden` | CSRF header missing/mismatched on cookie refresh/logout, or Origin disallowed. | `AUTH-ERR-005` |
| **409** | `Auth.UsernameUnavailable` | Username already registered by another normal or admin account. | `AUTH-ERR-006` |
| **409** | `Auth.RefreshRace` | Concurrent refresh request presented within 10-second rotation grace window. | `AUTH-ERR-007` |
| **429** | `Request.RateLimited` | Rate limit threshold or password-hashing concurrency exceeded. | `AUTH-ERR-008` |
| **503** | `Service.Unavailable` | Database connection failure during credential verification. | `AUTH-ERR-009` |

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Latency SLO Targets `[NORMATIVE]`
Under standard and peak production load (up to 2,000 active concurrent Hosts):

| Operation | Metric | Target SLO | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **Login (`POST /api/auth/login`)** | $p50$ | $\le 300\text{ ms}$ | `AUTH-SLO-001` |
| | $p95$ | $\le 750\text{ ms}$ | |
| | $p99$ | $\le 1.5\text{ seconds}$ | |
| **Refresh Rotation (`POST /api/auth/refresh`)** | $p50$ | $\le 50\text{ ms}$ | `AUTH-SLO-002` |
| | $p95$ | $\le 150\text{ ms}$ | |
| | $p99$ | $\le 300\text{ ms}$ | |
| **Password Change (`POST /api/auth/change-password`)** | $p95$ | $\le 800\text{ ms}$ | `AUTH-SLO-003` |
| **Logout / Logout-All** | $p95$ | $\le 100\text{ ms}$ | `AUTH-SLO-004` |

### 4.2 Password Hashing Resource Envelope `[NORMATIVE]`
* **`AUTH-HASH-001` (Argon2id Parameters)**:
  * Algorithm: **Argon2id** (memory: 64 MiB, iterations: 3, parallelism: 1).
  * BCrypt fallback: work factor $\ge 12$.
* **`AUTH-HASH-002` (Concurrency & Memory Cap)**:
  * Global hashing concurrency floor: maximum 16 concurrent hashing operations per process.
  * Maximum memory consumption dedicated to password hashing: $16 \times 64\text{ MiB} \approx 1\text{ GiB}$.
  * Excess hashing requests queue up to 50 items; beyond queue limit, request sheds load immediately with `429 Request.RateLimited`.

---

## 5. Security & Threat Mitigations

### 5.1 Dummy-Hash Timing-Attack Defense `[NORMATIVE]`
* **`AUTH-SEC-001` (Enumeration Defense)**:
  * When a login request specifies a non-existent username, the server executes a dummy adaptive password verification against a static/pre-computed hash with identical work parameters.
  * **Timing Requirement**: Statistical distribution comparisons across 10,000 iterations in controlled test environments must confirm that response time distributions for non-existent users and wrong-password existing users are computationally indistinguishable. The server does NOT claim exact network millisecond equality.
  * Returns generic `401 Auth.InvalidCredentials`.

### 5.2 Multi-Dimensional Rate Limiting (No Account Lockout) `[NORMATIVE]`
* **`AUTH-SEC-002` (Layered Rate Limiting)**:
  * **Dimension 1: Client IP**: Max 30 login attempts/minute per trusted client IP.
  * **Dimension 2: Target Username**: After 5 failed attempts against a specific normalized username, enforce progressive backoff delays (1s, 2s, 4s, capped at 10s per attempt). Permanent or temporary account lockout state is **strictly prohibited**.
  * **Dimension 3: Global Concurrency**: Max 16 concurrent hash threads with queue ceiling of 50.

### 5.3 Token Storage & Cookie Hardening `[NORMATIVE]`
* **`AUTH-SEC-003` (Cookie Security)**:
  * Access tokens stored exclusively in browser memory.
  * Refresh tokens delivered in `HttpOnly; Secure; SameSite=Lax` cookies scoped to `/api/auth`.
  * Database stores strictly `SHA256(RawToken)`. Raw refresh tokens are never persisted or logged.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 State-Changing Commit Outcome Contract `[NORMATIVE]`

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant AuthAPI as Auth Controller
    participant DB as PostgreSQL 16+

    Note over Client,DB: Registration, Refresh, or Password Change Flow
    Client->>AuthAPI: POST /api/auth/{action}
    AuthAPI->>DB: BEGIN Transaction
    alt Outcome A: Failure Before Commit
        AuthAPI->>DB: Abort / Connection Drop
        AuthAPI-->>Client: 503 Service.Unavailable
        Note over Client: Rollback confirmed; Safe to retry original input
    else Outcome B: Commit Succeeded, Response Dropped
        AuthAPI->>DB: COMMIT Transaction (State Durable)
        Note over AuthAPI,Client: Network Drops before HTTP Response
        Client->>Client: Timeout; Retries Request
        Client->>AuthAPI: POST /api/auth/{action} (Retry)
        Note over AuthAPI: Idempotent detection / safe 409 returned
    else Outcome C: Commit Outcome Unknown
        AuthAPI->>DB: Query In-Flight; Client Socket Closes
        Note over Client: Caller queries state or retries with idempotency handling
    end
```

* **Outcome A (Failure before commit)**: Transaction aborts; no state change occurs. Caller retries safely.
* **Outcome B (Commit succeeded, response lost)**:
  * Registration: Resubmission with same username returns `409 Auth.UsernameUnavailable`. Caller proceeds to login.
  * Refresh: Resubmission within 10s returns `409 Auth.RefreshRace`. Caller waits for in-flight token.
  * Logout / Logout-All: Resubmission is idempotent; returns `204 No Content`.
* **Outcome C (Outcome unknown to caller)**: Network timeout during operation. Caller must not assume rollback; for password change or registration, caller attempts login with new credentials before alerting the user.

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `AUTH-RISK-001` | Database connection pool exhausted during login. | Threads hanging; request pile-up. | Bounded acquisition timeout ($\le 3\text{ s}$); fail fast with `503 Service.Unavailable`. | Backoff with jitter; pool sizing invariants. | `AUTH-TEST-007` |
| `AUTH-RISK-002` | CPU saturation from simultaneous password hashes. | Thread starvation; denial of service across other routes. | Concurrency throttled to 16 threads; queue capped at 50; excess rejected with `429 Request.RateLimited`. | Load shedding; CPU reserved for gameplay. | `AUTH-TEST-008` |
| `AUTH-RISK-003` | Two tabs trigger token refresh simultaneously ($< 10\text{s}$). | Second tab fails; user prematurely logged out. | Second request returns `409 Auth.RefreshRace`. Token family preserved; client catches 409 and coordinates. | Rotation race grace window. | `AUTH-TEST-009` |
| `AUTH-RISK-004` | Malicious token replay after rotation ($\ge 10\text{s}$). | Attacker attempting session hijack via stolen refresh token. | Immediate revocation of entire token family; security version incremented; audit log recorded; returns `401 Auth.RefreshTokenReuse`. | Automatic family revocation. | `AUTH-TEST-010` |
| `AUTH-RISK-005` | Process crash during password change transaction. | Half-updated credentials or lingering active sessions. | Wrapped in atomic DB transaction: password update + token family revocation commit together or rollback completely. | Transactional all-or-nothing atomicity. | `AUTH-TEST-004` |
| `AUTH-RISK-006` | Background refresh token cleanup worker failure. | Accumulation of expired token hashes in database. | Worker retries with exponential backoff (10s, 30s, 60s); backlog tracked via operational metrics. | Resumable bounded batching ($\le 500$ rows). | `AUTH-TEST-011` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `AUTH-TEST-001` | `AUTH-REG-001`, `AUTH-BOUND-001` | Functional | Normal user self-registers with valid 3-char username and 12-char password. | `201 Created` returned. Account and tenant created. Zero session token issued. |
| `AUTH-TEST-002` | `AUTH-LOGIN-001`, `AUTH-SEC-003` | Functional | User logs in with valid credentials. | `200 OK` with 15-min JWT in body and 14-day refresh token in HttpOnly `SameSite=Lax` cookie. |
| `AUTH-TEST-003` | `AUTH-REF-001`, `AUTH-ROT-001` | Functional | Token refresh rotation with valid cookie, CSRF header, and allowed Origin. | Old token consumed; replacement token issued; `200 OK` with new JWT. |
| `AUTH-TEST-004` | `AUTH-PASS-001`, `AUTH-RISK-005` | Functional / Security | Authenticated user changes password via `POST /api/auth/change-password`. | Password hash updated; all active refresh families revoked; subsequent API calls with old JWT denied. |
| `AUTH-TEST-005` | `AUTH-LOG-002` | Functional | Authenticated user executes `POST /api/auth/logout-all`. | All active refresh families revoked; `TokenSecurityVersion` incremented; `204 No Content`. |
| `AUTH-TEST-006` | `AUTH-SLO-001`, `AUTH-SLO-002` | Non-Functional | Measure login latency under 2,000 concurrent active Hosts and refresh latency under 1,500 RPS. | Login meets $p95 \le 750\text{ ms}$, $p99 \le 1.5\text{ s}$; refresh meets $p95 \le 150\text{ ms}$. |
| `AUTH-TEST-007` | `AUTH-SEC-001`, `AUTH-RISK-001` | Security | Statistical timing analysis comparing non-existent username vs. wrong-password valid user over 10,000 requests. | Execution distributions are computationally indistinguishable (dummy hash verified). |
| `AUTH-TEST-008` | `AUTH-HASH-002`, `AUTH-RISK-002` | Security / Capacity | Flood authentication with 50 simultaneous login requests. | Hashing concurrency capped at 16; excess queued; queue overflow returns 429; memory stays within 1 GiB cap. |
| `AUTH-TEST-009` | `AUTH-ROT-002`, `AUTH-RISK-003` | Concurrency | Two browser tabs submit identical refresh token within 500 ms. | First succeeds (`200 OK`); second receives `409 Auth.RefreshRace`. Zero family revocation; replay gets no new secret. |
| `AUTH-TEST-010` | `AUTH-ROT-002`, `AUTH-RISK-004` | Security | Re-present rotated refresh token after 12 seconds. | Returns `401 Auth.RefreshTokenReuse`; entire token family revoked. |
| `AUTH-TEST-011` | `AUTH-CLEAN-001`, `AUTH-CLEAN-002` | Maintenance | Run cleanup worker with 5,000 expired/revoked tokens older than 7 days. | Deletes all 5,000 in 10 consecutive 500-row transactions; throughput $\ge 20,000$ rows/hr. |
| `AUTH-TEST-012` | `AUTH-ROT-001`, `AUTH-BOUND-004` | Functional | Attempt refresh on a token family that has been rotating for 30 consecutive days. | Rejected with `401 Auth.InvalidRefreshToken` (absolute family cap enforced). |
