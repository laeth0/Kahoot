# 07. Player Joining, Lobby Management, and Presence

This document defines the normative requirements for account-free Player joining, nickname validation and normalization, 500-seat capacity enforcement, recoverable idempotent joins (`JoinOperationId`), player token lifecycles, realtime presence consistency, and Host participant removal. It combines both functional workflows and non-functional specifications into a single document.

---

## 1. Topic Overview & Actors

The lobby phase is the exclusive entry point for Players entering a game:
* **Player / Participant**: Joins using a PIN, nickname, and client-generated `JoinOperationId`; receives a secure session token.
* **Registered User / Host**: Observes the lobby list in real time and can remove disruptive participants.
* **System Administrator**: Zero involvement in game lobbies or participant management.
* **Lobby Invariant `[NORMATIVE]`**: Players may join **only while the game is in `LOBBY`**. Once the game transitions out of `LOBBY` to start Question 1, new joins are permanently blocked.

---

## 2. Functional Specification & Workflows

### 2.1 Player Join Flow `[NORMATIVE]`
* **`JOIN-FLOW-001` (Join Endpoint)**: `POST /api/games/join`
* **`JOIN-FLOW-002` (Payload)**:
  ```json
  {
    "pin": "048912",
    "nickname": "MathWhiz",
    "joinOperationId": "c56a4180-65aa-42ec-a945-5fd21dec0538"
  }
  ```
* **`JOIN-FLOW-003` (Execution Sequence)**:
  1. Resolves game by numeric string `pin`. Verifies game exists and `Status == 'LOBBY'`.
  2. Validates `nickname` scalar length (2–30 chars post-trimming) and character set (permitted printable characters; no control codes).
  3. Computes `NormalizedNickname` via Unicode NFKC normalization + culture-independent case folding.
  4. Validates `joinOperationId` as a valid, high-entropy UUIDv4 string.
  5. Atomically in a single database transaction:
     * Enforces active non-removed seat count $< 500$.
     * Enforces uniqueness of `NormalizedNickname` within that `GameId` (including previously removed nicknames).
     * Enforces uniqueness of `(GameId, JoinOperationIdHash)`.
     * Allocates sequential seat number and creates `Participant` record.
     * Generates a 256-bit cryptographically secure random `PlayerSessionToken`.
     * Stores strictly `SHA256(PlayerSessionToken)` and `SHA256(JoinOperationId)`.
     * Increments `PresenceVersion` and registers reserved seat.
* **`JOIN-FLOW-004` (Join Response)**: `200 OK`
  ```json
  {
    "participantId": "prt_01HPX...",
    "playerSessionToken": "pst_9f8a7b6c5d4e...",
    "gameId": "gam_01HPX...",
    "nickname": "MathWhiz",
    "title": "Math 101 Quiz",
    "seatNumber": 42
  }
  ```

### 2.2 Hardened Recoverable Idempotent Join (`JoinOperationId`) `[NORMATIVE]`
To eliminate lost-response lockouts while preventing credential replay hijacking:
* **`JOIN-IDEM-001` (Temporary Recovery Credential Contract)**:
  * `JoinOperationId` acts as a temporary recovery credential strictly scoped to the initial join transaction.
  * Must be high-entropy, unpredictable (UUIDv4), stored hashed (`SHA-256`), and never logged in plain text.
  * Strictly scoped to exactly one `(GameId, NormalizedNickname)`.
  * **Recovery Lifetime**: Valid strictly while the game remains in `LOBBY` or up to a maximum of **15 minutes** post-creation. It cannot be used after the game starts Question 1 or after the recovery window expires.
  * **Active Socket Fencing**: Once a participant has successfully attached an active WebSocket connection, a subsequent late retry presenting the same `JoinOperationId` returns the existing `participantId` and existing connection metadata **without** generating a replacement token or invalidating the actively used session token.
* **`JOIN-IDEM-002` (Concurrent Retry Serialization)**:
  * If multiple exact retries race concurrently: database serializability or unique constraints on `JoinOperationIdHash` ensure that at most one `Participant` is created and exactly one seat is allocated.
* **`JOIN-IDEM-003` (Mismatched Input Rejection)**:
  * Presenting an existing `JoinOperationId` with a different nickname or different PIN returns `400 Validation.Failed`.

### 2.3 Player Session Token Lifecycle `[NORMATIVE]`
* **`JOIN-TOKEN-001` (Active Game Validity)**:
  * Token remains valid while game is in `LOBBY`, `QUESTION_ACTIVE`, `QUESTION_RESULTS`, or `LEADERBOARD`.
  * Invalidation triggers: Host explicitly removes participant; Host account is suspended; or game is terminated.
* **`JOIN-TOKEN-002` (Post-Game 24-Hour Read-Only Window Boundary)**:
  * When a game reaches `FINISHED`, the token validity boundary is strictly:
    * `serverTime < FinishedAt + 24 hours`: **Valid** for read-only game recovery and summary queries.
    * `serverTime >= FinishedAt + 24 hours`: **Invalid**; rejected with `401 Game.InvalidSessionToken`.
* **`JOIN-TOKEN-003` (Persistence & Integrity)**:
  * Expiring or cleaning up the session token hash after 24 hours **never** deletes historical business data (participant name, submitted answers, scores, or ranks).

### 2.4 Realtime Presence Tracking Semantics `[NORMATIVE]`
* **`JOIN-PRES-001` (Semantic Guarantees)**:
  To support 25,000 connections without database row thrashing on every socket packet:
  * **Durable Invariants**: Reserved seats and participant identities are durable relational records.
  * **Monotonic Versions**: Any authoritative presence update (join, kick, seat release) increments the monotonic `PresenceVersion`.
  * **Decoupled Transient Presence**: The system does NOT mandate writing every individual transient WebSocket connect and disconnect event directly to PostgreSQL rows. The architecture may satisfy transient presence using memory leases, generation fencing, or distributed presence routing, provided that:
    1. A stale disconnect cannot clear a newer connection generation.
    2. Connection counts are accurate enough for authoritative recovery.
    3. Reconnecting players always resume their correct seat.

### 2.5 Host Participant Removal `[NORMATIVE]`
* **`JOIN-KICK-001` (Removal Endpoint)**: `DELETE /api/games/{gameId}/participants/{participantId}`
* **`JOIN-KICK-002` (Removal Invariants)**:
  * Authenticated Host marks participant `IsRemoved = true` and `RemovedAt = NOW()`.
  * Immediately invalidates player session token and evicts active WebSocket connection.
  * **Nickname Permanence**: The removed nickname **remains permanently reserved** for that game session. No new player can claim it (`409 Game.NicknameTaken`).
  * **Lobby vs. Active Phase**:
    * If removed in `LOBBY`: frees one reserved seat.
    * If removed in `QUESTION_ACTIVE`: if the player has not yet submitted an answer, decrements `effectiveEligibleParticipantCount` by 1 (which may trigger equality auto-close). If the player already submitted an answer, their answer, points, and denominator inclusion are preserved.
    * If game is `FINISHED`: removal is rejected with `409 Game.InvalidStateTransition`.

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Boundaries Table

| Parameter | Min - 1 | Min | Normal | Max | Max + 1 | Notes | Stable Req ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **PIN Length** | 3 (400) | 4 (200) | 6 (200) | 8 (200) | 9 (400) | Numeric digits string. | `JOIN-BOUND-001` |
| **Nickname Length** | 1 (400) | 2 (200) | 12 (200) | 30 (200) | 31 (400) | Post-trimming scalar count. | `JOIN-BOUND-002` |
| **Seats per Game** | - | 0 (allowed) | 50 (200) | 500 (200) | 501 (409) | 500 non-removed limit. | `JOIN-BOUND-003` |
| **Recovery Boundary** | - | - | $23\text{h }59\text{m}$ (Valid) | $24\text{h }00\text{m}$ (Invalid)| $24\text{h }01\text{m}$ (Invalid)| Strict `< FinishedAt + 24h`. | `JOIN-BOUND-004` |

### 3.2 Canonical Negative Error Codes

| Status | Code | Trigger | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **400** | `Validation.Failed` | Nickname boundary violation, control chars, or mismatched `joinOperationId`. | `JOIN-ERR-001` |
| **404** | `Game.InvalidPin` | PIN does not match any unfinished game. | `JOIN-ERR-002` |
| **404** | `Game.ParticipantNotFound` | Target participant ID does not exist in this game. | `JOIN-ERR-003` |
| **409** | `Game.NotJoinable` | Game is in `QUESTION_ACTIVE`, `QUESTION_RESULTS`, `LEADERBOARD`, or `FINISHED`. | `JOIN-ERR-004` |
| **409** | `Game.NicknameTaken` | Normalized nickname already registered in this game (including removed players). | `JOIN-ERR-005` |
| **409** | `Game.Full` | Game has reached maximum 500 non-removed participants. | `JOIN-ERR-006` |
| **429** | `Request.RateLimited` | Join burst rate limit exceeded. | `JOIN-ERR-007` |

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Join Latency SLO Targets `[NORMATIVE]`
Under standard and burst lobby load:

| Metric | Target SLO | Stable Req ID |
| :--- | :--- | :--- |
| $p50$ | $\le 150\text{ ms}$ | `JOIN-SLO-001` |
| $p95$ | $\le 500\text{ ms}$ | |
| $p99$ | $\le 1.0\text{ second}$ | |

### 4.2 Platform Burst Join Capacity `[NORMATIVE]`
* **`JOIN-CAP-001` (Platform Burst Rate)**: The system sustains **1,500 player joins/second** platform-wide across concurrent lobbies.
* **`JOIN-CAP-002` (Single Game Fill Rate)**: A 500-seat game lobby supports filling from 0 to 500 players in $< 5\text{ seconds}$ without database deadlock or seat contention errors.

---

## 5. Security & Threat Mitigations

### 5.1 Token Security & Logging Sanitization `[NORMATIVE]`
* **`JOIN-SEC-001` (Hash Storage)**: Database stores only `SHA256(RawToken)` and `SHA256(JoinOperationId)`. Raw values are returned strictly once in the join response and never logged in server logs or query strings.
* **`JOIN-SEC-002` (NAT-Friendly Rate Limiting)**: Token bucket rate limiting applied per client IP: baseline capacity 1,200 tokens; refill rate 600 tokens per 10 seconds. Allows an entire classroom (500 players behind single NAT) to join in a rapid burst without false throttling.

For deployments behind a reverse proxy, set `TRUSTED_PROXY_NETWORK_0` to the proxy network CIDR (or configure `TrustedProxies:Networks` through deployment configuration). Only trusted proxy addresses may supply `X-Forwarded-For`; direct client headers are ignored. The Redis token bucket is shared by API replicas and expires idle client entries.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 State-Changing Commit Outcome Contract `[NORMATIVE]`
* **Outcome A (Failure before commit)**: Join transaction aborts; zero seat allocated; zero participant record; caller retries safely.
* **Outcome B (Commit succeeded, response lost)**: Participant and seat committed, response dropped. Client retries with identical `joinOperationId`: server matches record, leaves participant intact, and returns token without double-allocating a seat.
* **Outcome C (Outcome unknown to caller)**: Network timeout during join. Client resubmits identical `joinOperationId` to achieve deterministic Outcome B recovery.

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `JOIN-RISK-001` | Two players compete for the 500th seat simultaneously. | Over-allocation of seats beyond 500 limit. | Transaction locks seat counter (`SELECT FOR UPDATE`); exactly one commits seat 500; second receives `409 Game.Full`. | Strict transactional seat bound. | `JOIN-TEST-006` |
| `JOIN-RISK-002` | Network drops after join commits; client retries without `joinOperationId`. | Duplicate seat allocated or player locked out with `NicknameTaken`. | Client required to supply `joinOperationId`. With ID, returns existing identity; without ID, rejects duplicate nickname. | Recoverable idempotency key. | `JOIN-TEST-007` |
| `JOIN-RISK-003` | Player attempts to join as Host starts Question 1. | Inconsistent participant eligibility during active question. | Serialized: if join commits first, included in Question 1; if Start commits first, join rejected with `409 Game.NotJoinable`. | Clean state check at commit point. | `JOIN-TEST-008` |
| `JOIN-RISK-004` | Join retry races with active participant socket attachment. | Token rotation invalidates actively used session token. | If participant has an active connection generation, join retry returns existing session metadata without regenerating token. | Active socket fencing. | `JOIN-TEST-009` |
| `JOIN-RISK-005` | Host kicks participant who attempts immediate rejoin with same nickname. | Disruptive user re-enters lobby under same identity. | Nickname is permanently reserved for that `GameId`; join returns `409 Game.NicknameTaken`. | Durable tombstone on nickname. | `JOIN-TEST-004` |
| `JOIN-RISK-006` | Reconnect attempted at exactly $T = \text{FinishedAt} + 24\text{ hours}$. | Boundary ambiguity between read-only recovery and expiration. | Strict boundary: `serverTime < FinishedAt + 24h` succeeds; `serverTime >= FinishedAt + 24h` returns `401 Game.InvalidSessionToken`. | Strict `<` comparison operator. | `JOIN-TEST-010` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `JOIN-TEST-001` | `JOIN-FLOW-001`, `JOIN-FLOW-003` | Functional | Player joins lobby with PIN `"048912"` and nickname `"Alex"`. | Returns `200 OK` with session token; seat allocated. |
| `JOIN-TEST-002` | `JOIN-FLOW-003`, `JOIN-ERR-005` | Functional | Second player joins same game with nickname `"ALEX"`. | Rejected with `409 Game.NicknameTaken` (case-folded comparison). |
| `JOIN-TEST-003` | `JOIN-KICK-001`, `JOIN-KICK-002` | Functional | Host kicks participant `"Alex"`. | Participant marked removed; token revoked; seat freed. |
| `JOIN-TEST-004` | `JOIN-KICK-002`, `JOIN-RISK-005` | Functional | Third player attempts to join using removed nickname `"Alex"`. | Rejected with `409 Game.NicknameTaken` (nickname permanently reserved). |
| `JOIN-TEST-005` | `JOIN-FLOW-003`, `JOIN-ERR-004` | Functional | Player attempts to join while game is in `QUESTION_ACTIVE`. | Rejected with `409 Game.NotJoinable`. |
| `JOIN-TEST-006` | `JOIN-BOUND-003`, `JOIN-RISK-001` | Concurrency | Two parallel requests compete for 500th seat. | Exactly one gets seat 500 (`200 OK`); other rejected with `409 Game.Full`. Total seats = 500. |
| `JOIN-TEST-007` | `JOIN-IDEM-001`, `JOIN-RISK-002` | Idempotency | Client retries join with same `joinOperationId`, PIN, and nickname after dropped response. | Returns existing participant ID and valid token; zero duplicate seats. |
| `JOIN-TEST-008` | `JOIN-FLOW-003`, `JOIN-RISK-003` | Concurrency | Barrier test: Host starts game while 5 players submit joins. | Serialized cleanly: either admitted to lobby or rejected with `409 Game.NotJoinable`. |
| `JOIN-TEST-009` | `JOIN-IDEM-001`, `JOIN-RISK-004` | Concurrency | Join retry arrives while player already holds active WebSocket connection. | Existing session metadata returned; active connection is NOT severed or invalidated. |
| `JOIN-TEST-010` | `JOIN-TOKEN-002`, `JOIN-BOUND-004` | Boundary | Player reconnects at $23\text{h }59\text{m }59\text{s}$ vs $24\text{h }00\text{m }00\text{s}$ post-finish. | $23\text{h }59\text{m }59\text{s}$ succeeds (`200 OK`); $24\text{h }00\text{m }00\text{s}$ rejected with `401 Game.InvalidSessionToken`. |
| `JOIN-TEST-011` | `JOIN-CAP-001`, `JOIN-SLO-001` | Non-Functional | Measure join latency under 1,500 joins/second burst. | Meets $p95 \le 500\text{ ms}$ and $p99 \le 1.0\text{ s}$. |
