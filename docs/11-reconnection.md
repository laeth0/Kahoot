# 11. Player Reconnection and State Catch-Up

This document defines the normative requirements for account-free Player session recovery, authoritative state reconstruction, connection generation fencing, post-game read-only recovery boundaries, and mass reconnection storm management. It unifies functional recovery workflows and non-functional performance SLOs into a single specification.

---

## 1. Topic Overview & Actors

Reconnection enables Players to recover their active game state after network drops, browser refreshes, or server restarts:
* **Player / Participant**: Presents their previously issued `PlayerSessionToken` to resume gameplay.
* **Registered User / Host**: Observes live presence changes as players disconnect and reconnect.
* **System Administrator**: Zero involvement in player reconnection.
* **Key Invariant `[NORMATIVE]`**: Reconnection is strictly **state catch-up and session recovery**. It **never** creates a new participant, allocates an additional seat, or permits modifying a previously committed answer.

---

## 2. Functional Specification & Workflows

### 2.1 Reconnection Handshake `[NORMATIVE]`
* **`RECON-REC-001` (Hub Invocation)**:
  SignalR hub invocation:
  ```csharp
  // NON-NORMATIVE REFERENCE EXAMPLE
  Reconnect(string sessionToken)
  ```
* **`RECON-REC-002` (Validation Sequence)**:
  1. Computes `SHA256(sessionToken)`.
  2. Looks up `Participant` record matching `TokenHash` in PostgreSQL.
  3. Verifies participant exists and `IsRemoved == false`.
  4. Verifies associated game's Host account is not `Suspended`.
  5. Evaluates token lifecycle boundary:
     * If game is unfinished (`LOBBY`, `QUESTION_ACTIVE`, `QUESTION_RESULTS`, `LEADERBOARD`): token is valid.
     * If game is `FINISHED`: token is valid if `serverTime < FinishedAt + 24 hours`.
     * If `serverTime >= FinishedAt + 24 hours`: token is expired; rejected with `Game.InvalidSessionToken`.
* **`RECON-GEN-001` (Connection Replacement & Generation Fencing)**:
  * Atomically associates participant with new SignalR `ConnectionId`.
  * Increments participant's `ConnectionGeneration` in database/session store.
  * Older connection generations are immediately fenced and barred from submitting answers.
  * Subscribes new socket to `tenant:{tenantId}:game:{gameId}:players`.

### 2.2 Authoritative Phase-Specific State Catch-Up `[NORMATIVE]`
* **`RECON-CATCH-001` (Catch-Up Projections)**:
  The server compiles and returns a complete, self-contained `PlayerGameStateResponse` reflecting the current authoritative game state:

| Current Game State | Catch-Up Data Returned to Reconnecting Player | Pre-Reveal Privacy Rule |
| :--- | :--- | :--- |
| **`LOBBY`** | `gameId`, `status: "LOBBY"`, `title`, `nickname`, `seatNumber`, `totalParticipants`. | Standard lobby view. |
| **`QUESTION_ACTIVE`** | Active question snapshot (question text, media URL, choices with IDs/text only, index, total questions), server `deadlineUtc`, remaining seconds, and `alreadyAnswered: boolean`. | **Correct choices (`isCorrect`) are strictly omitted.** Provisional points earned are withheld from personal score. |
| **`QUESTION_RESULTS`** | Active question snapshot, correct choice IDs, aggregate per-choice selection counts, personal answer submission, points awarded, and updated total score. | Full reveal of question results. |
| **`LEADERBOARD`** | Top 5 ranked players, reconnecting player's personal score and sequential rank, and current game status. | Visible standings. |
| **`FINISHED`** | Final podium (top 3), complete personal score, final sequential rank, and total accepted answers. | Read-only final summary. |

### 2.3 Post-Game Read-Only Recovery Window `[NORMATIVE]`
* **`RECON-WINDOW-001` (Exact 24-Hour Expiry Boundary)**:
  * When a game reaches `FINISHED`, the participant session token enters a read-only final state valid strictly while:
    ```text
    serverTime < FinishedAt + 24 hours
    ```
  * At `serverTime >= FinishedAt + 24 hours`, the token expires and further reconnects return `Game.InvalidSessionToken`.
  * Allows participants who disconnected at the end of a game to review their final rank and summary.
  * Expiring the credential **never** deletes historical business data (participant nickname, answers, scores, or leaderboard records).

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Boundaries Table

| Parameter | Boundary Condition | Result / Handling | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **Seat Capacity** | Reconnecting to full 500-seat game | Allowed. Existing participant reuses previously allocated seat. | `RECON-BOUND-001` |
| **Active Question Timer** | Reconnecting at $T > \text{Deadline}$ | State shows 0s remaining; `alreadyAnswered` reflects submission status. | `RECON-BOUND-002` |
| **Post-Game Expiry** | Reconnecting at $T < \text{FinishedAt} + 24\text{h}$ | Succeeds (`200 OK`); returns final read-only summary. | `RECON-BOUND-003` |
| **Post-Game Expiry** | Reconnecting at $T \ge \text{FinishedAt} + 24\text{h}$ | Rejected with `Game.InvalidSessionToken`. | `RECON-BOUND-004` |

### 3.2 Canonical Negative Error Codes

| Status / Code | Triggering Condition | Stable Req ID |
| :--- | :--- | :--- |
| `Game.InvalidSessionToken` | Malformed token, non-existent token hash, or expired 24h window ($T \ge \text{FinishedAt} + 24\text{h}$). | `RECON-ERR-001` |
| `Game.ParticipantRemoved` | Participant was explicitly removed by the Host prior to reconnect. | `RECON-ERR-002` |
| `Game.Unavailable` | Host account has been suspended or game terminated by admin. | `RECON-ERR-003` |

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Reconnection Latency SLO Targets `[NORMATIVE]`
Under standard and mass reconnection load:

| Metric | Target SLO | Stable Req ID |
| :--- | :--- | :--- |
| $p50$ | $\le 1.0\text{ second}$ | `RECON-SLO-001` |
| $p95$ | $\le 3.0\text{ seconds}$ | |
| $p99$ | $\le 5.0\text{ seconds}$ | |

### 4.2 Mass Reconnection Burst Capacity `[NORMATIVE]`
* **`RECON-STORM-001` (Burst Recovery Rate)**:
  * The system must support restoring **5,000 valid Player sessions within 60 seconds** following a network hiccup or backend instance rolling restart.
  * Reconnection lookups must use indexed queries on `ParticipantSessionTokens.TokenHash` to prevent full table scans.

---

## 5. Security & Threat Mitigations

### 5.1 Connection Generation Fencing `[NORMATIVE]`
* **`RECON-SEC-001` (Generation Check)**:
  * Each reconnect increments `ConnectionGeneration`.
  * Incoming answer submissions must match the current `ConnectionGeneration`.
  * If a player opens two browser tabs with the same session token, the older tab is fenced and rejected if it attempts to submit an answer.

### 5.2 Pre-Reveal Secrecy Guarantee on Catch-Up `[NORMATIVE]`
* **`RECON-SEC-002` (Catch-Up Secrecy)**:
  * Reconnecting during an open `QUESTION_ACTIVE` phase must **never** reveal whether a submitted answer was correct.
  * The catch-up payload reports only `alreadyAnswered = true` and shows the score as of the previous revealed question.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 State-Changing Commit Outcome Contract `[NORMATIVE]`
* Reconnection is an authoritative state read and connection socket re-binding. It commits a new `ConnectionGeneration` and associates the new socket ID.
* If a reconnect request drops before the client receives the catch-up payload: the client retries with the same session token; the server increments generation again and delivers current state.

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `RECON-RISK-001` | Stale disconnect callback from old socket arrives after new socket connects. | New connection evicted or presence falsely decremented. | Disconnect handler checks connection ID; stale disconnect from old socket ID is discarded without affecting new socket. | Connection ID verification in disconnect handler. | `RECON-TEST-006` |
| `RECON-RISK-002` | Simultaneous reconnect from two tabs with same session token. | Race between competing sockets; inconsistent state. | Serialized generation increment; higher generation wins; older tab fenced. | Atomic generation counter. | `RECON-TEST-007` |
| `RECON-RISK-003` | 5,000 players reconnect simultaneously after instance restart (thundering herd). | Database connection pool exhaustion and timeouts. | Clients apply randomized exponential backoff with jitter (0–5s). Reconnect queries use indexed lookup on `TokenHash`. | Client jitter + DB index lookup. | `RECON-TEST-008` |
| `RECON-RISK-004` | Reconnect attempted at exactly $T = \text{FinishedAt} + 24\text{ hours}$. | Edge boundary ambiguity between access and expiration. | Strict operator: $T < \text{FinishedAt} + 24\text{h}$ succeeds; $T \ge \text{FinishedAt} + 24\text{h}$ returns `Game.InvalidSessionToken`. | Strict mathematical comparison. | `RECON-TEST-009` |
| `RECON-RISK-005` | Removed participant attempts to reconnect. | Evicted player rejoins active game. | Participant check detects `IsRemoved == true`; immediately returns `Game.ParticipantRemoved`. | Durable tombstone check. | `RECON-TEST-010` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `RECON-TEST-001` | `RECON-REC-001`, `RECON-CATCH-001` | Functional | Player disconnects during `LOBBY`; reconnects with valid session token. | Successfully attached to lobby; seat preserved; presence updated. |
| `RECON-TEST-002` | `RECON-CATCH-001`, `RECON-SEC-002` | Functional | Player disconnects during `QUESTION_ACTIVE` after submitting answer. | Reconnect returns `alreadyAnswered = true`; unrevealed points withheld. |
| `RECON-TEST-003` | `RECON-CATCH-001` | Functional | Player disconnects during `QUESTION_RESULTS`. | Reconnect returns correct choices, question stats, and earned points. |
| `RECON-TEST-004` | `RECON-CATCH-001`, `RECON-WINDOW-001` | Functional | Player reconnects to a game in `FINISHED` state within 24 hours. | Returns final podium, score, and rank; zero playable controls. |
| `RECON-TEST-005` | `RECON-SLO-001`, `RECON-STORM-001` | Non-Functional | Reconnect 5,000 players within 60 seconds under simulated network recovery. | All 5,000 reconnect successfully; latency meets $p95 \le 3.0\text{ s}$. |
| `RECON-TEST-006` | `RECON-GEN-001`, `RECON-RISK-001` | Concurrency | Stale disconnect callback from old socket arrives after new socket connects. | Old callback ignored; new socket remains connected and authoritative. |
| `RECON-TEST-007` | `RECON-GEN-001`, `RECON-RISK-002` | Concurrency | Simultaneous reconnect from two tabs using same session token. | Latest connection generation succeeds; older tab evicted and fenced. |
| `RECON-TEST-008` | `RECON-STORM-001`, `RECON-RISK-003` | Non-Functional | Thundering herd simulation: 5,000 reconnects flood backend in 5 seconds. | Jitter backoff spreads load; DB connection pool remains $< 70\%$; zero errors. |
| `RECON-TEST-009` | `RECON-WINDOW-001`, `RECON-BOUND-003`, `004` | Boundary | Player reconnects at $23\text{h }59\text{m }59\text{s}$ vs $24\text{h }00\text{m }00\text{s}$ post-finish. | First succeeds (`200 OK`); second rejected with `Game.InvalidSessionToken`. |
| `RECON-TEST-010` | `RECON-ERR-002`, `RECON-RISK-005` | Security | Participant removed by Host attempts to reconnect. | Rejected with `Game.ParticipantRemoved`. |
