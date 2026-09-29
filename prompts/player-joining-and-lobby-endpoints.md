# Player Joining, Lobby Management, and Presence Endpoints

This document specifies the REST API endpoints, command contracts, transaction workflows, nickname normalization protocols, 500-seat capacity enforcement, recoverable idempotent join mechanics (`JoinOperationId`), player token lifecycles, and realtime presence events based on [docs/07-joining-and-lobby.md](../docs/07-joining-and-lobby.md).

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                           JOINING & LOBBY MANAGEMENT ENDPOINTS                          │
├───────────────────────────────────┬─────────────────────────────────────────────────────┤
│ POST   /api/games/join            │ Account-free player join & seat allocation (Public) │
│ GET    /api/games/join/{pin}      │ Pre-join PIN validation & lobby metadata (Public)   │
│ DELETE /api/games/{id}/participants/{participantId} │ Remove/kick participant (Host)   │
│ GET    /api/games/{id}/participants │ Query lobby participants list & status (Host)     │
├───────────────────────────────────┴─────────────────────────────────────────────────────┤
│                         REAL-TIME SIGNALR PRESENCE & DISPATCH                           │
├───────────────────────────────────┬─────────────────────────────────────────────────────┤
│ ParticipantPresenceChanged        │ Broadcast to Host and Players on join/leave/kick    │
│ ParticipantRemoved                │ Targeted eviction frame sent to kicked player socket│
│ Active Socket Fencing             │ Prevents token invalidation during reconnect retry  │
└───────────────────────────────────┴─────────────────────────────────────────────────────┘
```

---

## 1. Core Architectural & Domain Invariants

### 1.1 The Lobby Invariant (`JOIN-FLOW-003`, `JOIN-ERR-004`)
* Players may join a game session **strictly and exclusively while the game is in `GameStatus.Lobby` (`2`)**.
* Once the Host transitions the game from `LOBBY` to `QUESTION_ACTIVE` via `POST /api/games/{id}/start`, the lobby is permanently closed to new admissions. Any subsequent join attempts must be rejected with `409 Game.NotJoinable`.

### 1.2 Strict 500-Seat Capacity Limit (`JOIN-BOUND-003`, `JOIN-CAP-002`, `JOIN-ERR-006`, `JOIN-RISK-001`)
* A game session supports an authoritative ceiling of **at most 500 concurrent, active (non-removed) participants**.
* Seat allocation is atomic and serialized inside a database transaction with row-level locks on the `games` row (`SELECT FOR UPDATE`) or atomic counter conditions.
* When active non-removed seats reach 500, any new join request is rejected with `409 Game.Full`.

### 1.3 Nickname Normalization & Permanent Tombstone Reservation (`JOIN-FLOW-003`, `JOIN-ERR-005`, `JOIN-RISK-005`)
* **Sanitization**: Inbound nicknames are trimmed of leading/trailing whitespace. Scalar length must be between 2 and 30 characters. Control characters (Unicode categories `Cc` and `Cf`, including zero-width spaces) are strictly rejected with `400 Validation.Failed`.
* **Canonical Normalization**: The server computes `NormalizedNickname` via **Unicode NFKC normalization** followed by **culture-independent uppercase folding** (e.g. `Alex` and `ALEX` produce identical normalized representations).
* **Scope & Uniqueness**: `NormalizedNickname` must be unique per `GameId` (enforced by the unique constraint `ux_participants_game_nickname`).
* **Permanent Tombstone Reservation**: If a participant is removed/kicked by the Host, their nickname **remains permanently reserved** for that game session. Neither the kicked player nor any other player may reuse that nickname (`409 Game.NicknameTaken`).

### 1.4 Hardened Recoverable Idempotency (`JoinOperationId`) (`JOIN-IDEM-001`, `JOIN-IDEM-002`, `JOIN-IDEM-003`, `JOIN-RISK-002`, `JOIN-RISK-004`)
* Every join request requires a client-generated UUIDv4 `joinOperationId`.
* Stored strictly as a cryptographic hash (`SHA-256`) in `participants.join_operation_id_hash`. Raw values are never logged or stored.
* **Outcome B Recovery**: If a client experiences a network disconnect or response drop after the join transaction commits, resubmitting the identical `joinOperationId` (with matching `pin` and `nickname`) returns the existing `participantId`, allocated `seatNumber`, and session token without allocating a duplicate seat or throwing `409 Game.NicknameTaken`.
* **Recovery Window**: `JoinOperationId` is valid for recovery strictly while the game is in `LOBBY` or up to a maximum of **15 minutes** post-creation (`JoinRecoveryExpiresAt = NOW() + 15m`).
* **Active Socket Fencing**: If a participant has already attached an active WebSocket connection, a late join retry presenting the same `joinOperationId` returns the existing session metadata **without generating a replacement token or invalidating the actively used session token**.
* **Tampering Defense**: Submitting an existing `joinOperationId` with altered parameters (different nickname or PIN) is rejected with `400 Validation.Failed`.

### 1.5 Player Session Token Lifecycle (`JOIN-TOKEN-001`, `JOIN-TOKEN-002`, `JOIN-TOKEN-003`, `JOIN-RISK-006`)
* Generated as a 256-bit cryptographically secure random token (e.g. `pst_...`).
* Persisted in `participant_session_tokens` as `SHA256(RawToken)`.
* **Active Game Validity**: Valid across all active game phases (`LOBBY`, `QUESTION_ACTIVE`, `QUESTION_RESULTS`, `LEADERBOARD`).
* **Post-Game 24-Hour Boundary**: Once a game reaches `FINISHED`, the token enters a read-only recovery window:
  - `serverTime < FinishedAt + 24 hours`: **Valid** for read-only score/summary queries.
  - `serverTime >= FinishedAt + 24 hours`: **Invalid**; rejected with `401 Game.InvalidSessionToken`.
* Token expiration never deletes historical participation, score, rank, or answer records.

---

## 2. Group A: Player Joining Endpoints

### 1. `POST /api/games/join` (Player Join & Seat Allocation)
- **Requirement IDs**: `JOIN-FLOW-001`, `JOIN-FLOW-002`, `JOIN-FLOW-003`, `JOIN-FLOW-004`, `JOIN-IDEM-001`, `JOIN-BOUND-001`, `JOIN-BOUND-002`, `JOIN-BOUND-003`, `JOIN-ERR-001`, `JOIN-ERR-002`, `JOIN-ERR-004`, `JOIN-ERR-005`, `JOIN-ERR-006`, `JOIN-ERR-007`, `JOIN-SLO-001`, `JOIN-SEC-001`, `JOIN-SEC-002`
- **Authentication**: Anonymous / Public (Account-free player access).
- **Rate Limiting (`JOIN-SEC-002`)**: NAT-friendly token bucket applied per client IP (1,200 burst tokens, 600 tokens/10s refill) to support an entire school or classroom joining simultaneously from a single NAT IP. If exhausted, returns `429 Request.RateLimited`.
- **Content-Type**: `application/json`
- **Request Body**:
  ```json
  {
    "pin": "048912",
    "nickname": "MathWhiz",
    "joinOperationId": "c56a4180-65aa-42ec-a945-5fd21dec0538"
  }
  ```

#### Request Validation Rules
1. `pin`:
   - Required, string of 4 to 8 ASCII numeric digits (`^[0-9]{4,8}$`).
   - Leading zeroes must be preserved as formatted strings.
2. `nickname`:
   - Required string, trimmed of outer whitespace.
   - Scalar length: $\ge 2$ and $\le 30$ characters.
   - Forbidden: ASCII control characters (`[\x00-\x1F\x7F]`) and Unicode control/format characters (`Cc`, `Cf`).
3. `joinOperationId`:
   - Required, valid UUIDv4 string (`Guid.TryParse` validation).

#### Transactional Execution Pipeline (`IsolationLevel.RepeatableRead` / Row Locking)
1. **Hash Computation**:
   - `joinOperationIdHash = SHA256(Encoding.UTF8.GetBytes(request.joinOperationId))`
   - `normalizedNickname = request.nickname.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant()`
2. **Game Resolution & Lock (`JOIN-FLOW-003`)**:
   - Queries `games` table by `pin`:
     ```sql
     SELECT * FROM games WHERE pin = @Pin AND status != 6 FOR UPDATE
     ```
   - If no active game matches the PIN: returns `404 Game.InvalidPin`.
   - If game `status != GameStatus.Lobby`: returns `409 Game.NotJoinable`.
3. **Idempotency Check (`JOIN-IDEM-001`)**:
   - Checks if a `Participant` already exists matching `(GameId, JoinOperationIdHash)`:
     - If record exists:
       - **Tampering Check**: Verify stored `NormalizedNickname == normalizedNickname`. If mismatched, returns `400 Validation.Failed`.
       - **Recovery Expiry Check**: If `utcNow >= participant.JoinRecoveryExpiresAt`, returns `409 Game.NotJoinable` (recovery window lapsed).
       - **Active Socket Fencing (`JOIN-RISK-004`)**: If the participant has an active connection registered in `HostPresenceService`, retrieve existing active token hash metadata without regenerating or rotating the token.
       - If no active socket is holding the token, generate a fresh `PlayerSessionToken`, update/insert `participant_session_tokens`, and return existing `participantId`, `seatNumber`, and session token.
       - Returns `200 OK` (Outcome B Idempotent Recovery).
4. **Nickname Uniqueness Check (`JOIN-FLOW-003`, `JOIN-ERR-005`, `JOIN-RISK-005`)**:
   - Checks if any `Participant` exists with `GameId == game.Id` and `NormalizedNickname == normalizedNickname` (including removed participants where `IsRemoved == true`):
     - If exists: returns `409 Game.NicknameTaken`.
5. **Capacity Bound Check (`JOIN-BOUND-003`, `JOIN-ERR-006`, `JOIN-RISK-001`)**:
   - Evaluates active seat count: `currentActiveSeats = COUNT(*) FROM participants WHERE game_id = @GameId AND is_removed = FALSE`.
   - If `currentActiveSeats >= 500`: returns `409 Game.Full`.
6. **Seat Allocation & Entity Creation**:
   - `seatNumber = game.NextSeatNumber`
   - `game.NextSeatNumber += 1`
   - `game.ReservedParticipantCount += 1`
   - `game.PresenceVersion += 1`
   - `participantId = Guid.NewGuid()`
   - Generate 256-bit cryptographically secure raw token: `rawToken = "pst_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()`
   - `tokenHash = SHA256(Encoding.UTF8.GetBytes(rawToken))`
   - Inserts `Participant`:
     - `Id`: `participantId`
     - `HostAccountId`: `game.HostAccountId`
     - `GameId`: `game.Id`
     - `DisplayNickname`: `request.nickname.Trim()`
     - `NormalizedNickname`: `normalizedNickname`
     - `SeatNumber`: `seatNumber`
     - `JoinOperationIdHash`: `joinOperationIdHash`
     - `JoinRecoveryExpiresAt`: `utcNow.AddMinutes(15)`
     - `IsRemoved`: `false`
     - `RemovedAt`: `null`
     - `TotalScore`: `0`
     - `Rank`: `null`
     - `ConnectionGeneration`: `1`
     - `CreatedAt`: `utcNow`
   - Inserts `ParticipantSessionToken`:
     - `Id`: `Guid.NewGuid()`
     - `HostAccountId`: `game.HostAccountId`
     - `GameId`: `game.Id`
     - `ParticipantId`: `participantId`
     - `TokenHash`: `tokenHash`
     - `CreatedAt`: `utcNow`
     - `RevokedAt`: `null`
     - `ExpiresAt`: `null`
7. **Commit & Post-Commit Realtime Dispatch**:
   - Commits database transaction.
   - Dispatches `ParticipantPresenceChanged` event to SignalR group `host:{hostAccountId}:game:{gameId}:hosts`:
     ```json
     {
       "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
       "presenceVersion": 4,
       "reservedParticipantCount": 42,
       "connectedParticipantCount": 38,
       "nickname": "MathWhiz",
       "seatNumber": 42,
       "reason": "Joined"
     }
     ```
8. **Response**: `200 OK`
   ```json
   {
     "participantId": "01923485-dddd-7abc-9f5a-444444444444",
     "playerSessionToken": "pst_9f8a7b6c5d4e3f2a1b0c9d8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a3b2c1d0e9f8a",
     "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
     "nickname": "MathWhiz",
     "title": "Math 101 Quiz",
     "seatNumber": 42
   }
   ```

#### Error Codes
- `400 Validation.Failed`: Malformed payload, invalid PIN format, nickname out of bounds (1 or > 30 chars), control characters, invalid UUIDv4 `joinOperationId`, or mismatched retry payload.
- `404 Game.InvalidPin`: PIN does not match any currently active unfinished game session.
- `409 Game.NotJoinable`: Game session is not in `LOBBY` status (already started or finished).
- `409 Game.NicknameTaken`: Nickname is already in use or was previously used by a removed player in this game.
- `409 Game.Full`: Game lobby has reached the maximum capacity of 500 active participants.
- `429 Request.RateLimited`: Excessive join requests from this client IP address.

---

### 2. `GET /api/games/join/{pin}` (Pre-Join PIN & Lobby Metadata Verification)
- **Requirement IDs**: `JOIN-FLOW-001`, `JOIN-ERR-002`, `JOIN-BOUND-001`
- **Authentication**: Anonymous / Public.
- **Route Parameters**:
  - `pin` (`string`): 4 to 8 numeric digit PIN.
- **Execution**:
  - Resolves active game by `pin`.
  - Verifies `status == GameStatus.Lobby`.
  - Computes current non-removed participant count.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "title": "Math 101 Quiz",
    "status": "LOBBY",
    "participantCount": 42,
    "maxCapacity": 500,
    "isFull": false
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Invalid PIN format.
  - `404 Game.InvalidPin`: No active game found for this PIN.
  - `409 Game.NotJoinable`: Game has already progressed beyond `LOBBY`.

---

## 3. Group B: Host Lobby Management & Participant Removal

### 3. `DELETE /api/games/{gameId}/participants/{participantId}` (Host Remove / Kick Participant)
- **Requirement IDs**: `JOIN-KICK-001`, `JOIN-KICK-002`, `JOIN-PRES-001`, `JOIN-ERR-003`, `JOIN-ERR-004`, `JOIN-RISK-005`, `GAME-AUTO-002`, `RT-EVT-002`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Route Parameters**:
  - `gameId` (`Guid`): Target game session identifier.
  - `participantId` (`Guid`): Target participant identifier.
- **Execution Rules**:
  1. **Host Tenant Boundary**:
     - Game must exist and belong to the authenticated Host (`games.host_account_id == currentUser.UserId`). If missing, returns `404 Game.NotFound`.
  2. **Terminal State Immutability (`GAME-ARCH-001`)**:
     - If game `status == GameStatus.Finished`: removal is rejected with `409 Game.InvalidStateTransition` (or `409 Game.ArchiveImmutable`).
  3. **Participant Existence Guard**:
     - Participant must exist with `Id == participantId`, `GameId == gameId`, and `HostAccountId == hostAccountId`. If not found, returns `404 Game.ParticipantNotFound`.
  4. **Idempotent Removal**:
     - If participant is already marked `IsRemoved == true`: returns `204 No Content` immediately.
  5. **State Transition & Denominator Effects**:
     - In a single database transaction:
       - Set `participant.IsRemoved = true`, `participant.RemovedAt = utcNow`.
       - Invalidate all active session tokens for this participant:
         ```sql
         UPDATE participant_session_tokens
         SET revoked_at = @UtcNow
         WHERE participant_id = @ParticipantId AND revoked_at IS NULL
         ```
       - Increment `game.PresenceVersion += 1`.
       - **If Game is in `LOBBY`**:
         - Decrement `game.ReservedParticipantCount = MAX(0, game.ReservedParticipantCount - 1)`.
       - **If Game is in `QUESTION_ACTIVE`**:
         - Inspect whether the removed participant has submitted an answer for the current question (`answer_submissions`).
         - **If participant has NOT submitted an answer**:
           - Decrement current question's `EffectiveEligibleParticipantCount -= 1`.
           - **Equality Auto-Close Check (`GAME-AUTO-002`)**:
             If `snapshot.AcceptedAnswerCount == snapshot.EffectiveEligibleParticipantCount` and `snapshot.EffectiveEligibleParticipantCount > 0`:
             - Trigger immediate auto-closure via `IGameAutoCloseService.EvaluateAutoCloseAsync(...)` to transition `QUESTION_ACTIVE -> QUESTION_RESULTS`.
         - **If participant HAS submitted an answer**:
           - Their submitted answer, score, and inclusion in the eligible denominator are preserved.
  6. **Realtime Socket Eviction & Presence Notification**:
     - Target evicted player socket: Send `ParticipantRemoved` event to the player's connection and terminate the WebSocket connection.
     - Broadcast `ParticipantPresenceChanged` event (`reason: "Removed"`) to host group:
       ```json
       {
         "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
         "presenceVersion": 5,
         "reservedParticipantCount": 41,
         "connectedParticipantCount": 37,
         "nickname": "MathWhiz",
         "seatNumber": 42,
         "reason": "Removed"
       }
       ```
- **Response**: `204 No Content`
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.
  - `404 Game.ParticipantNotFound`: Participant does not exist in this game.
  - `409 Game.InvalidStateTransition`: Game is already in terminal `FINISHED` state.

---

### 4. `GET /api/games/{id}/participants` (Query Lobby Participants List)
- **Requirement IDs**: `JOIN-FLOW-003`, `JOIN-PRES-001`, `RA-PAGE-001`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Route Parameters**:
  - `id` (`Guid`): Game session identifier.
- **Query Parameters**:
  - `includeRemoved` (`bool`, default `false`): Whether to include kicked participants.
  - `limit` (`int`, default `100`, max `500`): Maximum records to return.
  - `cursor` (`int?`, default `null`): Keyset cursor on `SeatNumber` for deterministic paging.
- **Execution**:
  - Verifies game belongs to authenticated Host.
  - Queries `participants` ordered by `SeatNumber ASC`.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "presenceVersion": 5,
    "reservedParticipantCount": 41,
    "participants": [
      {
        "participantId": "01923485-dddd-7abc-9f5a-444444444444",
        "nickname": "MathWhiz",
        "seatNumber": 1,
        "isRemoved": false,
        "joinedAt": "2026-09-29T12:00:00.000Z"
      }
    ],
    "nextCursor": null
  }
  ```
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.

---

## 4. Group C: Realtime Hub Invocations & Presence Protocol

The SignalR Hub (`/hubs/game`) provides duplex socket connectivity for both Hosts and Players based on [docs/10-realtime-and-protocol.md](../docs/10-realtime-and-protocol.md).

### 4.1 Player Hub Invocations
All client-to-server hub invocations return the typed envelope:
```json
{
  "success": true,
  "data": { ... },
  "error": null
}
```

#### 1. `JoinGame(string pin, string nickname, string joinOperationId)` (`RT-METH-001`)
- WebSocket shortcut that internally delegates to the `JoinGameCommand` pipeline.
- On success:
  - Binds the socket to `host:{hostAccountId}:game:{gameId}:players`.
  - Registers the active socket generation in `HostPresenceService`.
  - Returns `success: true` with the join payload and session token.

#### 2. `Reconnect(string sessionToken)` (`RT-METH-002`, `JOIN-TOKEN-001`, `JOIN-TOKEN-002`)
- Hashes `sessionToken` via SHA-256 and validates against `participant_session_tokens`.
- Enforces active game validity or post-game 24-hour boundary (`serverTime < FinishedAt + 24h`).
- Verifies participant is not marked `IsRemoved == true`.
- Increments `participant.ConnectionGeneration += 1`.
- Replaces prior socket connection and attaches current connection to `host:{hostAccountId}:game:{gameId}:players`.
- Returns full catch-up projection: current game status, state version, current question (choices without answers), and player's cumulative score/streak.

### 4.2 Server-to-Client Event Specifications

| Event Name | Audience | Payload Contents | Invariants & Protection | Stable Req ID |
| :--- | :--- | :--- | :--- | :--- |
| `ParticipantPresenceChanged` | Host & Players | `gameId`, `presenceVersion`, `reservedParticipantCount`, `connectedParticipantCount`, `nickname`, `seatNumber`, `reason` (`Joined`, `Disconnected`, `Reconnected`, `Removed`) | Monotonic `presenceVersion`. Zero session tokens or socket IDs disclosed. | `RT-EVT-001`, `JOIN-PRES-001` |
| `ParticipantRemoved` | Evicted Client | `gameId`, `participantId`, `reason: "KickedByHost"` | Targeted strictly to evicted connection; socket severed immediately. | `RT-EVT-002`, `JOIN-KICK-002` |

---

## 5. Concurrency, Race Conditions & Failure Modes

### 5.1 Concurrency Matrix

| Scenario / Race Condition | Potential Impact | Required System Behavior | Architectural Defense | Verification Test |
| :--- | :--- | :--- | :--- | :--- |
| **500th Seat Contention (`JOIN-RISK-001`)** | Two players submit join requests simultaneously for seat 500. | Over-allocation of seats beyond the 500 limit. | Transaction locks the game row (`SELECT FOR UPDATE`). First commits seat 500 (`200 OK`); second evaluates $500 \ge 500$ and receives `409 Game.Full`. | Strict row-lock transactional serialization. | `JOIN-TEST-006` |
| **Network Response Drop (`JOIN-RISK-002`)** | Client commits join transaction but network drops before receiving response. | Player locked out with `NicknameTaken` or burns duplicate seats. | Client resubmits identical `joinOperationId`. Server detects matching hash, leaves participant intact, and returns existing token/seat. | Idempotent recovery credential. | `JOIN-TEST-007` |
| **Join vs. Game Start Barrier (`JOIN-RISK-003`)** | Player submits join concurrently with Host issuing `StartGame`. | Inconsistent participant eligibility during Question 1. | Database serialization: if Join commits first, included in Question 1 denominator; if Start commits first, join rejected with `409 Game.NotJoinable`. | Commit-point state guard. | `JOIN-TEST-008` |
| **Retry Races Active Socket (`JOIN-RISK-004`)** | Join retry arrives while player already holds an active WebSocket socket. | Token rotation invalidates actively used session token mid-game. | Server checks active connection generation. Returns existing session metadata without rotating or revoking token. | Active socket fencing. | `JOIN-TEST-009` |
| **Rejoin After Host Kick (`JOIN-RISK-005`)** | Kicked player immediately rejoins with the same nickname. | Disruptive user re-enters lobby under same identity. | Nickname remains permanently reserved in `participants` table. Rejoin rejected with `409 Game.NicknameTaken`. | Permanent nickname tombstone. | `JOIN-TEST-004` |
| **Boundary Reconnect ($T = \text{FinishedAt} + 24\text{h}$) (`JOIN-RISK-006`)** | Reconnect attempt at exactly 24 hours post-game completion. | Boundary ambiguity between read-only recovery and expiration. | Strict inequality: `serverTime < FinishedAt + 24h` succeeds; `serverTime >= FinishedAt + 24h` returns `401 Game.InvalidSessionToken`. | Strict `<` comparison operator. | `JOIN-TEST-010` |

---

## 6. Non-Functional Latency, Capacity & Security SLOs

| Metric | Target SLO | Constraint / Behavior | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **Join Latency ($p50$)** | $\le 150\text{ ms}$ | Standard load conditions. | `JOIN-SLO-001` |
| **Join Latency ($p95$)** | $\le 500\text{ ms}$ | Peak classroom burst conditions. | `JOIN-SLO-001` |
| **Join Latency ($p99$)** | $\le 1.0\text{ second}$ | Under peak multi-lobby concurrency. | `JOIN-SLO-001` |
| **Platform Join Burst Capacity** | $\ge 1,500\text{ joins/second}$ | Platform-wide concurrent lobbies. | `JOIN-CAP-001` |
| **Single Lobby Fill Rate** | 0 to 500 players in $< 5\text{ seconds}$ | Zero database deadlocks or lost seats. | `JOIN-CAP-002` |
| **NAT Rate Limiting Capacity** | 1,200 burst / 600 refill per 10s | Token bucket per client IP. | `JOIN-SEC-002` |
| **Token Storage Cryptography** | SHA-256 hash storage | Raw tokens never persisted or logged. | `JOIN-SEC-001` |
| **Socket Eviction Propagation** | $\le 100\text{ ms}$ cluster-wide | Instant disconnect across all replicas via Redis Pub/Sub. | `RT-SCALE-001` |

---

## 7. Canonical Error Codes Reference

| HTTP Status | Error Code | Description & Trigger Condition | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **400** | `Validation.Failed` | Nickname $< 2$ or $> 30$ chars, control characters, invalid PIN format, invalid UUIDv4 `joinOperationId`, or retry parameter mismatch. | `JOIN-ERR-001` |
| **401** | `Auth.Unauthorized` | Missing or invalid JWT on Host management routes. | `JOIN-ERR-001` |
| **401** | `Game.InvalidSessionToken` | Presented player session token is invalid, revoked, or expired ($> \text{FinishedAt} + 24\text{h}$). | `JOIN-TOKEN-002` |
| **403** | `Auth.Forbidden` | Authenticated user is not in `Host` role. | `JOIN-ERR-001` |
| **404** | `Game.InvalidPin` | PIN does not match any active unfinished game session. | `JOIN-ERR-002` |
| **404** | `Game.NotFound` | Game does not exist or belongs to another Host tenant. | `GAME-ERR-002` |
| **404** | `Game.ParticipantNotFound` | Target participant ID does not exist in this game session. | `JOIN-ERR-003` |
| **409** | `Game.NotJoinable` | Game session is not in `LOBBY` status (already started or finished). | `JOIN-ERR-004` |
| **409** | `Game.NicknameTaken` | Normalized nickname already registered in this game (including removed players). | `JOIN-ERR-005` |
| **409** | `Game.Full` | Lobby has reached the maximum capacity of 500 active non-removed participants. | `JOIN-ERR-006` |
| **409** | `Game.InvalidStateTransition` | Removal attempted on a game in terminal `FINISHED` state. | `GAME-ERR-005` |
| **429** | `Request.RateLimited` | Client IP address exceeded token bucket join burst limit. | `JOIN-ERR-007` |
