# 10. Realtime Protocol and SignalR Communication

This document defines the normative requirements for the SignalR realtime hub, client/server communication protocols, cross-instance audience routing, event dispatch contracts, slow-client backpressure, and transport failure recoveries. It unifies functional event specifications and non-functional performance SLOs into a single vertical specification.

---

## 1. Topic Overview & Actors

Realtime communication coordinates live quiz synchronization across distributed clients:
* **Hub Endpoint**: `/hubs/game`
* **Registered User / Host**: Subscribes to Host-specific event streams and controls the game.
* **Player / Participant**: Connects via WebSocket, submits answers, and receives player-projected events.
* **Audience Segregation `[NORMATIVE]`**:
  * Hosts and Players belong to separate server-managed groups.
  * Group identifiers are strictly derived server-side:
    * `host:{hostAccountId}:game:{gameId}:hosts`
    * `host:{hostAccountId}:game:{gameId}:players`
  * `{hostAccountId}` is the Host user's `users.id`, stored on the game as `games.host_account_id`; there is no separate account table.
  * Clients can **never** specify, inject, or request arbitrary SignalR group names.

---

## 2. Functional Specification & Workflows

### 2.1 Hub Methods and Invocations `[NORMATIVE]`
* **`RT-HUB-001` (Envelope Contract)**:
  All hub method invocations return a typed response envelope:
  ```json
  {
    "success": true,
    "data": { ... },
    "error": null
  }
  ```
  If `success == false`, `data` is `null` and `error` contains `{ "code": string, "description": string }`.
* **`RT-HUB-002` (Hub Method Signatures)**:

| Method Signature | Invoker & Preconditions | Description & Return Contract | Stable Req ID |
| :--- | :--- | :--- | :--- |
| `JoinGame(string pin, string nickname, string joinOperationId)` | Public / Prospective Player | Joins lobby, reserves seat, returns one-time session token, and attaches socket to player group. | `RT-METH-001` |
| `Reconnect(string sessionToken)` | Player with issued token | Validates token hash, replaces prior connection, increments generation, and returns full authoritative player catch-up state. | `RT-METH-002` |
| `SubmitAnswer(string questionId, List<string> choiceIds)` | Player with active socket | Validates choices and deadline, commits answer, returns `{ "accepted": true, "alreadyAnswered": false }`. | `RT-METH-003` |
| `JoinAsHost(string gameId)` | Authenticated Host | Validates Host ownership and JWT claims; attaches socket to host group. | `RT-METH-004` |

### 2.2 Server-to-Client Event Specifications `[NORMATIVE]`
* **`RT-DISP-001` (Broadcast Payload Invariants)**:
  All server-broadcast events carry the durable `gameId` and the committed `stateVersion`. Presence events additionally carry `presenceVersion`:

| Event Name | Audience | Payload Contents & Privacy Rules | Stable Req ID |
| :--- | :--- | :--- | :--- |
| `ParticipantPresenceChanged` | Both | `connectedParticipantCount`, `reservedParticipantCount`, `presenceVersion`, participant nickname, and event reason (`Joined`, `Disconnected`, `Reconnected`, `Removed`). Zero tokens or connection IDs disclosed. | `RT-EVT-001` |
| `ParticipantRemoved` | Evicted Client | Targeted directly to the evicted participant's socket; payload contains `{ "participantId": "..." }`. Connection is severed immediately. | `RT-EVT-002` |
| `QuestionStarted` | Players | Question text, choices (IDs and text only), media URL, question index, total questions, start time, deadline. **Correct choice indicators (`isCorrect`) are strictly withheld.** | `RT-EVT-003` |
| `QuestionStartedForHost` | Hosts | Full question projection including `correctChoiceIds`, base points, duration, and `effectiveEligibleParticipantCount`. | `RT-EVT-004` |
| `QuestionEnded` | Both | Revealed results: `correctChoiceIds`, per-choice selection counts, total answers received, and historical eligible denominator. | `RT-EVT-005` |
| `LeaderboardUpdated` | Both | Visible ranked standings of non-removed participants: `rank`, `nickname`, `score`. | `RT-EVT-006` |
| `GameEnded` | Both | Final podium and game completion notification. (Suspension allows only a safe terminal notification with no suspension details). | `RT-EVT-007` |

### 2.3 Non-Normative REST Reference Directory
*(Note: Normative definitions, boundaries, and validation for all REST routes are owned by their respective documents: 01, 02, 03, 04, 05, 06, 07, 12, and 13. The following summary table is provided strictly for integration reference).*

```text
[NON-NORMATIVE REFERENCE DIRECTORY]
Auth Routes:         POST /api/auth/{register, login, refresh, logout, logout-all, change-password} (Owned by Doc 02)
Admin Routes:        GET /api/admin/users, POST /api/admin/users/{id}/{suspend, reactivate}, etc. (Owned by Doc 03)
Quiz Routes:         GET/POST /api/quizzes, PUT/DELETE /api/quizzes/{id}, questions CRUD, reorder, publish (Owned by Doc 04)
Media Routes:        POST /api/uploads/images, GET /uploads/{filename} (Owned by Doc 05)
Game Controls:       POST /api/games, POST /api/games/{id}/{start, end-question, show-leaderboard, advance, end} (Owned by Doc 06)
Lobby Routes:        POST /api/games/join, DELETE /api/games/{id}/participants/{pid} (Owned by Doc 07)
Health Endpoints:    GET /health/live, GET /health/ready, GET /health (Owned by Doc 12)
```

### 2.4 Multi-Instance Event Distribution & Socket Fencing `[NORMATIVE]`
* **`RT-SCALE-001` (Cross-Instance Routing Guarantees)**:
  In a multi-instance backend topology:
  1. Any authorized Host or Player may connect to any healthy backend instance.
  2. Group event delivery must operate seamlessly across all instances via a shared/distributed realtime routing mechanism. (Technology selection—such as Redis, NATS, or other message buses—is an architectural implementation choice, not a normative requirement of this specification).
  3. Eviction of a removed participant or suspended account must propagate across all backend instances within $\le 100\text{ ms}$, terminating matching sockets on every node.
  4. Event loss during cross-instance transit must be detectable via `stateVersion` gaps and recoverable via authoritative catch-up queries.
  5. Slow clients on one node or high traffic in one tenant must never cause memory exhaustion or event starvation for unrelated tenants on any node.

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Boundaries Table

| Parameter | Boundary Condition | Result / Handling | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **Max Aggregate Connections** | $\ge 25,000$ active sockets | Supported platform capacity baseline. | `RT-BOUND-001` |
| **Outbound Buffer Ceiling**| $> 64\text{ KB}$ or $> 50$ queued frames | Slow client disconnected; server RAM protected. | `RT-BOUND-002` |
| **Frame Size Limit** | $> 32\text{ KB}$ frame payload | Protocol violation; socket closed immediately. | `RT-BOUND-003` |
| **Stale State Version** | Client receives event with version $< K$ | Client discards stale out-of-order event. | `RT-BOUND-004` |
| **Idle Timeout** | Zero keep-alive frames for 120 seconds | Socket closed cleanly; resources freed. | `RT-BOUND-005` |

### 3.2 Transport & Connection Failure Handling `[NORMATIVE]`
* **`RT-FAIL-001` (Explicit Failure Recovery Protocols)**:
  1. **Handshake Succeeded, Group Authorization Failed**: If token is invalid or belongs to another game, hub sends error envelope `{ "code": "Auth.Forbidden" }` and terminates WebSocket with code `4403`.
  2. **Unauthenticated / Abandoned Socket**: If connection establishes but client fails to invoke `JoinGame`, `Reconnect`, or `JoinAsHost` within 15 seconds, server forcefully closes socket.
  3. **Half-Open TCP Connection**: Server sends periodic ping/heartbeat every 15 seconds. If two consecutive pings go unacknowledged, server marks socket dead and releases memory.
  4. **Backend Crash Without Disconnect Callback**: Ephemeral memory handles drop instantly. The client detects transport severance, invokes exponential backoff with jitter, and reconnects to another healthy backend node.
  5. **Duplicate / Out-of-Order Events**: If client receives an event with `stateVersion <= currentVersion`, client safely ignores it. If an event gap is detected (`stateVersion > currentVersion + 1`), client triggers an authoritative state catch-up query.
  6. **Slow-Reader Buffer Overflow**: If client buffer reaches 64 KB, server forcibly severs connection, drops unsent frames, and frees connection memory.
  7. **Reconnect During Rolling Deployment**: Application instances advertise readiness drain state. In-flight sockets receive clean close frame (`1001 Going Away`), prompting immediate client reconnection to a ready peer node.

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Realtime Latency SLO Targets `[NORMATIVE]`
Under target production load (25,000 aggregate connections, 200 concurrent games):

| Operation / Interaction | Metric | Target SLO | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **SignalR Negotiation & Handshake** | $p50$ | $\le 1.0\text{ second}$ | `RT-SLO-001` |
| | $p95$ | $\le 3.0\text{ seconds}$ | |
| | $p99$ | $\le 5.0\text{ seconds}$ | |
| **Realtime Event Delivery (Post-Commit)** | $p50$ | $\le 150\text{ ms}$ | `RT-SLO-002` |
| | $p95$ | $\le 500\text{ ms}$ | |
| | $p99$ | $\le 1.0\text{ second}$ | |
| **Server Fan-Out Dispatch Initiation** | $p95$ | $\le 100\text{ ms}$ | `RT-SLO-003` |
| | $p99$ | $\le 250\text{ ms}$ | |
| **Connection Success Rate** | Platform | $\ge 99.5\%$ successful connects | `RT-SLO-004` |

---

## 5. Security & Threat Mitigations

### 5.1 Query-String Token Redaction in WebSockets `[NORMATIVE]`
* **`RT-SEC-001` (Log Redaction)**: When browser WebSocket clients connect, access tokens passed via `?access_token=...` must be stripped and redacted at reverse proxy boundaries (e.g. Nginx logging) and backend diagnostic logs to prevent credential exposure.

### 5.2 Slow-Client Backpressure & Memory Protection `[NORMATIVE]`
* **`RT-SEC-002` (Buffer Ceiling)**: Outbound socket buffers are capped at **64 KB** (or 50 queued messages). Unresponsive or slow 3G clients are disconnected before consuming server memory headroom.

### 5.3 Strict Audience Isolation `[NORMATIVE]`
* **`RT-SEC-003` (Group Namespace Fencing)**: Group names are computed strictly server-side based on claims. Players cannot join Host groups. Broadcasts to `:hosts` are strictly segregated from `:players`.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 Commit-First Best-Effort Broadcast `[NORMATIVE]`
* **`RT-ORD-001` (Durable State Priority)**:
  * The server **always commits state to PostgreSQL first** before initiating SignalR fan-out.
  * If a fan-out fails or network drops occur during broadcast:
    * Committed database state remains authoritative and is **never rolled back**.
    * Clients resynchronize state via REST (Host) or `Reconnect` (Player).

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `RT-RISK-001` | Slow client fails to drain socket buffer during 500-player broadcast. | Unbounded server memory consumption and crash. | Server clamps buffer to 64 KB; severs slow connection; other clients unaffected. | Backpressure buffer cutoff. | `RT-TEST-006` |
| `RT-RISK-002` | Event delivery drops or arrives out of order over WebSocket. | Client UI displays stale or corrupted game phase. | Client compares event `stateVersion`; if gap detected, triggers authoritative catch-up. | Sequence gap detection. | `RT-TEST-007` |
| `RT-RISK-003` | Host account suspended while 500 players hold active sockets. | Players continue interacting with suspended tenant game. | Suspension broadcast triggers instant socket severance across cluster within 100 ms. | Cluster-wide revocation broadcast. | `RT-TEST-008` |
| `RT-RISK-004` | WebSocket connection opened but client never calls Join or Auth. | Resource exhaustion from idle zombie sockets. | Socket timeout fires after 15 seconds; unauthenticated socket closed. | Handshake timeout guard. | `RT-TEST-009` |
| `RT-RISK-005` | Node crash while hosting 5,000 active SignalR sockets. | Mass disconnect storm against remaining backend nodes. | Clients apply exponential backoff with jitter; reconnect to surviving instances using session tokens. | Jittered reconnect storm handling. | `RT-TEST-010` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `RT-TEST-001` | `RT-HUB-002`, `RT-METH-004` | Functional | Host connects to `/hubs/game` and invokes `JoinAsHost(gameId)`. | Subscribed to host group; receives `ParticipantPresenceChanged`. |
| `RT-TEST-002` | `RT-HUB-002`, `RT-METH-001` | Functional | Player connects and invokes `JoinGame("048912", "Player1", uuid)`. | Receives session token and initial game state. |
| `RT-TEST-003` | `RT-DISP-001`, `RT-EVT-003` | Functional | Host advances question; `QuestionStarted` emitted to 500 players. | All players receive question text and choices; zero correct answer flags. |
| `RT-TEST-004` | `RT-SEC-003` | Security | Player attempts to invoke `JoinAsHost`. | Hub invocation rejected with `403 Auth.Forbidden`. |
| `RT-TEST-005` | `RT-SLO-001`, `RT-SLO-002` | Non-Functional | Measure SignalR handshake latency ($p95 \le 3.0\text{ s}$) and broadcast latency ($p95 \le 500\text{ ms}$) under 25,000 connections. | Latencies satisfy SLO targets; zero memory exhaustion. |
| `RT-TEST-006` | `RT-BOUND-002`, `RT-RISK-001` | Security / Capacity | Throttle one client's socket to 1 byte/sec during question fan-out. | Slow client's buffer fills to 64 KB and is disconnected; other clients unaffected. |
| `RT-TEST-007` | `RT-ORD-001`, `RT-RISK-002` | Concurrency | Client drops network mid-broadcast; receives `stateVersion = 4` after last seeing `2`. | Gap detected; invokes `Reconnect`; catches up to current phase. |
| `RT-TEST-008` | `RT-SCALE-001`, `RT-RISK-003` | Concurrency | Suspend Host account running 200-player game. | Sockets for Host and all 200 players severed across cluster within $\le 100\text{ ms}$. |
| `RT-TEST-009` | `RT-BOUND-005`, `RT-RISK-004` | Boundary | Client connects WebSocket but sends zero method calls for 15 seconds. | Server forcefully closes unauthenticated socket; connection freed. |
| `RT-TEST-010` | `RT-FAIL-001`, `RT-RISK-005` | Fault Injection | Terminate one backend instance under load of 5,000 active sockets. | Sockets reconnect to surviving instance with jittered backoff; zero dropped scores. |
