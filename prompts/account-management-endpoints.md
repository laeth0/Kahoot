# Account Management & Tenant Lifecycle Endpoints Specification

> **Source Documentation**: [`docs/03-account-management.md`](../docs/03-account-management.md)  
> **Status**: Normative Implementation Plan  
> **Scope**: Administrative oversight of Host accounts and platform System Administrators.

---

## 1. Overview & Actors

- **System Administrator**: Initiates account queries, status mutations, and administrator lifecycle actions.
- **Registered User / Host**: The target of lifecycle state mutations.
- **Player / Participant**: Ephemeral participant impacted by suspension of their game's Host.
- **Supported Account Lifecycle States**:
  - `Active`: Account is authorized for standard operations.
  - `Suspended`: Account is immediately blocked from all authenticated actions and its unfinished games are permanently terminated.
  - *Note*: No `Deactivated`, `Soft-Deleted`, `Archived`, or `Purged` states exist as normal account lifecycle states.

---

## 2. Comprehensive HTTP Endpoints Catalog

### Group A: Host Account Management (`/api/admin/users`)

All endpoints in this group require authentication with the `SystemAdmin` role.

#### 1. `GET /api/admin/users`
- **Description**: Lists registered Host accounts using keyset cursor pagination with optional filtering.
- **Query Parameters**:
  - `cursor` (`string?`): Opaque base64-encoded keyset cursor for stable pagination across updates.
  - `pageSize` (`int`, default: `50`, min: `1`, max: `100`): Number of records per page (`ACCT-BOUND-003`).
  - `username` (`string?`, max: `64` chars): Prefix filter matched against `NormalizedUsername` (`ACCT-BOUND-002`).
  - `status` (`UserStatus?`): Filter by `Active` or `Suspended`.
- **Response**: `200 OK`
  ```json
  {
    "items": [
      {
        "accountId": "01923485-9831-7abc-9f5a-3829482910aa",
        "username": "TeacherJane",
        "accountKind": "Host",
        "status": "Active",
        "createdAt": "2026-09-10T14:30:00Z",
        "statusChangedAt": null,
        "revision": 3,
        "terminationPending": false
      }
    ],
    "nextCursor": "ZXlKaGJHY2lPaUpTVXpVeE1pSXNJblI1Y0NJNklrcFhWQ0o5...",
    "hasMore": true
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Invalid cursor, page size out of bounds, or username $> 64$ characters.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Non-admin caller.

---

#### 2. `GET /api/admin/users/{accountId}`
- **Description**: Retrieves administrative metadata for a single specific Host account.
- **Route Parameters**:
  - `accountId` (`Guid`): Target user account identifier.
- **Response**: `200 OK`
  ```json
  {
    "accountId": "01923485-9831-7abc-9f5a-3829482910aa",
    "username": "TeacherJane",
    "accountKind": "Host",
    "status": "Active",
    "createdAt": "2026-09-10T14:30:00Z",
    "statusChangedAt": null,
    "revision": 3,
    "terminationPending": false
  }
  ```
- **Privacy Guarantee (`ACCT-QUERY-002`)**: Must **never** join, project, or disclose private tenant content (quizzes, question text, media items, game history, player answers, or scores).
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Non-admin caller.
  - `404 Account.NotFound`: Account does not exist or has `Role != Host`.

---

#### 3. `POST /api/admin/users/{accountId}/suspend`
- **Description**: Immediately suspends a Host account and initiates background game finalization.
- **Route Parameters**:
  - `accountId` (`Guid`): Target Host account identifier.
- **Request Body**:
  ```json
  {
    "revision": 3
  }
  ```
- **Behavior & Phase 1 Cutoff (`ACCT-SUSP-001`, `ACCT-SUSP-003`)**:
  - Validates `revision` against database record; fails with `409` on mismatch.
  - In a single atomic database transaction:
    - Sets `Status = Suspended`.
    - Increments `TokenSecurityVersion` (instantly revoking all active JWTs and refresh token families).
    - Sets `StatusChangedAt = UtcNow`.
    - Increments `Revision`.
    - Determines if any unfinished games (`CREATED`, `LOBBY`, `QUESTION_ACTIVE`, `QUESTION_RESULTS`, `LEADERBOARD`) exist.
    - If unfinished games exist, sets `TerminationPending = true`; otherwise `TerminationPending = false`.
  - Evicts all active SignalR socket connections for the Host and connected players within $p95 \le 500\text{ ms}$ (`ACCT-SLO-002`).
- **Response**:
  - `202 Accepted` with `{ "terminationPending": true }` (if unfinished games were queued for Phase 2 finalization).
  - `204 No Content` (if the Host had zero active games and finalization was completed immediately).
- **Error Codes**:
  - `400 Validation.Failed`: Malformed payload or invalid `accountId`.
  - `401 Auth.Unauthorized`: Missing or invalid admin JWT.
  - `403 Auth.Forbidden`: Non-admin caller.
  - `404 Account.NotFound`: Target Host account does not exist.
  - `409 Account.ConcurrentModification`: Supplied `revision` does not match the current database revision.

---

#### 4. `POST /api/admin/users/{accountId}/reactivate`
- **Description**: Reactivates a suspended Host account.
- **Route Parameters**:
  - `accountId` (`Guid`): Target Host account identifier.
- **Request Body**:
  ```json
  {
    "revision": 4
  }
  ```
- **Preconditions (`ACCT-REACT-001`, `ACCT-BOUND-005`)**:
  - Account must currently have `Status == Suspended`.
  - **Reactivation Gate**: `TerminationPending` must be `false` (Phase 2 finalization must be 100% complete).
  - Supplied `revision` must match current database state.
- **Behavior**:
  - Sets `Status = Active`.
  - Sets `StatusChangedAt = UtcNow`.
  - Increments `Revision`.
  - *Invariants*: Old tokens remain permanently revoked (fresh login required); past games remain permanently `FINISHED`.
- **Response**: `204 No Content`
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid admin JWT.
  - `403 Auth.Forbidden`: Non-admin caller.
  - `404 Account.NotFound`: Account not found or wrong role.
  - `409 Account.TerminationPending`: Attempting to reactivate while Phase 2 game finalization is still in progress (`ACCT-ERR-007`).
  - `409 Account.ConcurrentModification`: Revision mismatch.

---

### Group B: System Administrator Account Lifecycle (`/api/admin/administrators`)

Platform administrator management endpoints.

#### 5. `GET /api/admin/administrators`
- **Description**: Lists all platform System Administrator accounts.
- **Query Parameters**: None (administrative accounts are low cardinality).
- **Response**: `200 OK`
  ```json
  [
    {
      "accountId": "01923480-1111-7abc-9f5a-3829482910bb",
      "username": "admin",
      "accountKind": "SystemAdmin",
      "status": "Active",
      "createdAt": "2026-09-01T10:00:00Z",
      "statusChangedAt": null,
      "revision": 1,
      "terminationPending": false
    }
  ]
  ```
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid admin JWT.
  - `403 Auth.Forbidden`: Non-admin caller.

---

#### 6. `POST /api/admin/administrators`
- **Description**: Provisions a new active platform System Administrator account.
- **Request Body**:
  ```json
  {
    "username": "SecAdmin01",
    "password": "CorrectHorseBatteryStaple_2026!"
  }
  ```
- **Validation Rules (`ACCT-ADMIN-001`)**:
  - `username`: 3–64 characters post-NFKC normalization; alphanumeric, hyphens, underscores. Must be globally unique across all accounts.
  - `password`: 12–128 characters; validated against standard password strength rules.
- **Response**: `201 Created`
  ```json
  {
    "accountId": "0192348a-4521-7abc-9f5a-3829482910cc",
    "username": "SecAdmin01",
    "accountKind": "SystemAdmin",
    "status": "Active",
    "createdAt": "2026-09-26T22:30:00Z",
    "statusChangedAt": null,
    "revision": 1,
    "terminationPending": false
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Username or password length/character requirements violated.
  - `401 Auth.Unauthorized`: Missing or invalid admin JWT.
  - `403 Auth.Forbidden`: Non-admin caller.
  - `409 Account.Conflict`: Username is already taken by another Host or Administrator.

---

#### 7. `POST /api/admin/administrators/{id}/suspend`
- **Description**: Suspends a System Administrator account.
- **Route Parameters**:
  - `id` (`Guid`): Target administrator identifier.
- **Request Body**:
  ```json
  {
    "revision": 2
  }
  ```
- **Critical Invariant (`ACCT-ADMIN-003`, `ACCT-BOUND-004`)**:
  - The system must transactionally prevent suspending the final remaining active administrator (`COUNT(ActiveAdmins) > 1` checked under a pessimistic row lock).
  - If the target is the last active admin, the request must fail with `409 Account.LastAdministrator`.
- **Response**: `204 No Content`
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid admin JWT.
  - `403 Auth.Forbidden`: Non-admin caller.
  - `404 Account.NotFound`: Target administrator does not exist.
  - `409 Account.LastAdministrator`: Attempted to suspend the only active platform administrator.
  - `409 Account.ConcurrentModification`: Revision mismatch.

---

#### 8. `POST /api/admin/administrators/{id}/reactivate`
- **Description**: Reactivates a suspended System Administrator account.
- **Route Parameters**:
  - `id` (`Guid`): Target administrator identifier.
- **Request Body**:
  ```json
  {
    "revision": 3
  }
  ```
- **Behavior**: Sets `Status = Active`, increments `Revision`.
- **Response**: `204 No Content`
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid admin JWT.
  - `403 Auth.Forbidden`: Non-admin caller.
  - `404 Account.NotFound`: Target administrator does not exist.
  - `409 Account.ConcurrentModification`: Revision mismatch.

---

## 3. Required Background Worker: `SuspensionFinalizerWorker`

In addition to HTTP endpoints, section 2.2 defines the **Phase 2 Resumable Game Finalizer Background Worker** (`ACCT-SUSP-004`, `ACCT-RISK-002`):

1. **Trigger / Sweeper**:
   - Runs periodically or receives in-process / queue signals whenever a Host is suspended with `TerminationPending == true`.
   - On application boot, immediately scans for any accounts with `TerminationPending == true` to resume interrupted finalizations after a crash.
2. **Execution Rules**:
   - Queries unfinished games owned by the suspended Host.
   - Updates games in **bounded batches ($\le 10$ games per transaction)** to prevent table locks and connection starvation:
     - Sets `Status = FINISHED`.
     - Sets `FinishedAt = SuspensionTimestamp`.
     - Materializes final ranks and releases game PINs.
   - Once all games are finalized, sets `TerminationPending = false` on the User record.

---

## 4. Canonical Error Codes Reference

| HTTP Status | Error Code | Meaning |
| :---: | :--- | :--- |
| **`400`** | `Validation.Failed` | Invalid cursor, invalid page size, or malformed request body. |
| **`401`** | `Auth.Unauthorized` | Missing or invalid admin JWT. |
| **`403`** | `Auth.Forbidden` | Caller does not possess the `SystemAdmin` role. |
| **`404`** | `Account.NotFound` | Target account ID does not exist or has incompatible role. |
| **`409`** | `Account.ConcurrentModification` | Revision mismatch during state mutation. |
| **`409`** | `Account.LastAdministrator` | Attempt to suspend the sole remaining active platform administrator. |
| **`409`** | `Account.TerminationPending` | Attempt to reactivate an account while Phase 2 game finalization is active. |
| **`409`** | `Account.Conflict` | Attempt to create an administrator with an existing username. |
| **`503`** | `Service.Unavailable` | Database failure or transient connection issue. |
