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

## Acceptance Criteria

- Given valid host credentials, when the host logs in, then the system issues a short-lived access token and a rotating refresh token.
- Given a host operation, when the request has no valid access token, then the operation is rejected.
- Given a valid host token, when the host attempts to control a game started from another host's quiz, then access is denied.
- Given configured bootstrap credentials and no matching account, when the application starts, then the bootstrap host account is created.
- Given an existing account with the configured bootstrap username, when seeding runs, then the account is left unchanged.

## Business Rules

### FR-2: Host authentication

- Host credentials consist only of a username and password. Email is not used anywhere in the system.
- Passwords are stored only as a strong one-way hash; related security qualities are defined in the non-functional requirements.
- Refresh tokens are stored hashed and support rotation with reuse detection.
- All host operations, including quiz management and game control, require a valid access token.
- Game ownership is enforced: a host may control only games started from their own quizzes.
- Players never authenticate and can never invoke host operations.

### FR-2.1: Bootstrap host account

- On application startup, the system ensures that a bootstrap host account exists.
- The bootstrap username and password come from configuration or environment variables and are never hard-coded.
- Seeding is idempotent.
- Seeding is separate from schema migration as defined in FR-9.

## Edge Cases

- If bootstrap credentials are not configured, seeding is skipped with a warning and application startup still succeeds.
- Refresh-token reuse is detected rather than issuing another valid token.
