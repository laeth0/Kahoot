# Authentication

## Overview

Host authentication protects quiz authoring and game-control operations while
keeping the player experience account-free.

## User Stories

### US-003: Authenticate as a host

**As a**
host

**I want**
to sign in with my username and password

**So that**
I can securely manage my quizzes and games.

### US-004: Bootstrap the first host

**As an**
operator

**I want**
the configured bootstrap host to be created at startup

**So that**
the platform can be administered without embedding credentials in source code.

### US-021: Maintain and end a host session

**As a**
host

**I want**
to refresh an active session and log out when finished

**So that**
I can stay signed in safely and deliberately revoke my session.

## Acceptance Criteria

- Given valid host credentials, when the host logs in, then the system issues a short-lived access token and a rotating refresh token.
- Given a host operation, when the request has no valid access token, then the operation is rejected.
- Given a valid host token, when the host attempts to control a game started from another host's quiz, then access is denied.
- Given enabled bootstrap seeding, valid configured credentials, and no matching account, when the application starts, then the bootstrap host account is created.
- Given an existing account with the configured bootstrap username, when seeding runs, then the account is left unchanged.
- Given a valid unexpired refresh token, when the host refreshes the session, then the old token is revoked, a replacement in the same token family is stored, and a new access token and refresh cookie are issued.
- Given a host refresh-token family, when the host logs out with any token from that family, then every unrevoked token in the family is revoked and the refresh cookie is deleted.

## Business Rules

### FR-2: Host authentication

- Host credentials consist only of a username and password. Email is not used anywhere in the system.
- Passwords are stored only as a strong one-way hash; related security qualities are defined in the non-functional requirements.
- Refresh tokens are stored hashed and support rotation with reuse detection.
- All host operations, including quiz management and game control, require a valid access token.
- Game ownership is enforced: a host may control only games started from their own quizzes.
- Players never authenticate and can never invoke host operations.

### FR-2.1: Bootstrap host account

- When bootstrap seeding is enabled, application startup ensures that the configured bootstrap host account exists.
- The bootstrap username and password come from configuration or environment variables and are never hard-coded.
- The bootstrap username is trimmed and normalized to lowercase.
- The bootstrap password must contain at least 12 characters including uppercase, lowercase, numeric, and special characters.
- Seeding is idempotent.
- Seeding is separate from schema migration as defined in FR-9.

### FR-2.2: Login

- Login accepts a username of at most 64 characters and a password of at most 128 characters; both are required.
- Usernames are trimmed and matched in normalized lowercase form.
- Invalid usernames and passwords return the same invalid-credentials result.
- Successful login returns the host ID, username, access token, and access-token expiration.
- The refresh token is persisted only as a hash and is returned in an HttpOnly cookie scoped to `/api/auth` with `SameSite=Lax`; its `Secure` attribute follows `Auth:SecureCookies`.

### FR-2.3: Token refresh

- Refresh accepts the refresh token from the protected cookie or, when no cookie is present, from the request body.
- A refresh token is valid only while it exists, is unrevoked, and has not expired.
- Rotation revokes the consumed token, links it to its replacement, and preserves the token-family identifier.
- Cookie-based refresh requires an accepted CSRF header and, when an `Origin` header is present, an allowed origin.
- Reuse outside the short concurrency grace window revokes every active token in the family.

### FR-2.4: Logout

- Logout accepts the refresh token from the protected cookie or request body.
- Cookie-based logout applies the same CSRF and origin validation as refresh.
- Logout is idempotent when the token is missing or unknown.
- A successful logout deletes the refresh cookie and returns no content.

### FR-2.5: Refresh-token retention

- Expired tokens and revoked tokens are retained for seven days before they become eligible for deletion.
- Cleanup runs when the background service starts and then hourly.
- Each cleanup deletes at most 100 eligible tokens, oldest expiration first.
- A cleanup failure is logged and does not terminate the background service.

## Edge Cases

- If bootstrap seeding is disabled, startup skips it without requiring credentials.
- If seeding is enabled with a missing username, missing password, or password that fails the complexity rules, application startup fails.
- An expired refresh token is revoked and rejected.
- A concurrent refresh within the 10-second grace window returns `Auth.RefreshRace`; reuse after that window returns `Auth.RefreshTokenReuse` and revokes the token family.
- Cookie-based refresh or logout without the required CSRF header, or from a disallowed origin, is forbidden.
