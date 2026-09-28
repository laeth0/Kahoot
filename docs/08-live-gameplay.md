# 08. Live Gameplay and High-Throughput Ingestion

This document defines the normative requirements for active question presentation, countdown timers, inclusive server-side deadlines, authoritative clock skew handling, dynamic participant eligibility, high-throughput answer ingestion, multi-tiered abuse rate-limiting, and anti-leakage safeguards. It unifies functional workflows and non-functional performance SLOs into a single specification.

---

## 1. Topic Overview & Actors

Live gameplay is the real-time competitive core of the platform:
* **Registered User / Host**: Controls question start, reveals, and podium displays.
* **Player / Participant**: Receives question content, evaluates choices, and submits answers before time expires.
* **System Administrator**: Zero visibility or participation in live gameplay.
* **Core Invariants `[NORMATIVE]`**:
  * The server is the **sole authority** on time, eligibility, answer acceptance, and correctness.
  * Correct choices and awarded points are **never disclosed** to Players during the active answering phase.
  * Once submitted and accepted, an answer is immutable and cannot be altered.

---

## 2. Functional Specification & Workflows

### 2.1 Question Activation & Pre-Reveal Concealment `[NORMATIVE]`
* **`PLAY-ACT-001` (Question Presentation Payload)**:
  * Triggered by Host issuing `StartGame` or `AdvanceQuestion`.
  * The realtime `QuestionStarted` broadcast to Players contains:
    ```json
    {
      "questionIndex": 0,
      "totalQuestions": 10,
      "text": "What is the capital of France?",
      "imageUrl": "/uploads/550e8400-e29b-41d4-a716-446655440000.png",
      "durationSeconds": 30,
      "choices": [
        { "choiceId": "chc_01", "text": "Paris" },
        { "choiceId": "chc_02", "text": "London" },
        { "choiceId": "chc_03", "text": "Berlin" },
        { "choiceId": "chc_04", "text": "Madrid" }
      ],
      "startedAt": "2026-09-19T21:00:00.000Z",
      "endsAt": "2026-09-19T21:00:30.000Z"
    }
    ```
  * **Safe Concealment Invariant**: The `isCorrect` boolean flags are strictly withheld on the server. Point calculation hints, correct choice IDs, and live score changes are completely omitted from all player projections.

### 2.2 Server-Side Time Authority & Inclusive Deadlines `[NORMATIVE]`
* **`PLAY-TIME-001` (Authoritative Server Time)**:
  * Client system clocks are completely untrusted and disregarded.
  * All backend nodes must synchronize with UTC via Network Time Protocol (NTP). Node clock skew must not exceed $50\text{ ms}$. If node clock skew exceeds safe boundaries, the node fails readiness or falls back to database-authoritative timestamps (`CURRENT_TIMESTAMP`).
  * Clock rollback or leap-second adjustments must not grant extra answer time; elapsed time is computed monotonically.
* **`PLAY-TIME-002` (Inclusive Deadline Boundary)**:
  * Server calculates `endsAt = startedAt + DurationSeconds`.
  * An answer processed at `serverTime <= endsAt` is accepted.
  * An answer processed at `serverTime > endsAt` is rejected with `Game.AnswerTooLate`.
  * **Non-Transitioning Invariant**: Expiration of `endsAt` stops accepting new answers; it **does NOT by itself change game state**. The game remains in `QUESTION_ACTIVE` until equality auto-close or Host `EndQuestion`.

### 2.3 Dynamic Eligibility & Canonical Auto-Close `[NORMATIVE]`
* **`PLAY-ELIG-001` (Initial Eligibility)**: Upon question activation, `effectiveEligibleParticipantCount` is set to the total active non-removed participants in the game.
* **`PLAY-ELIG-002` (Network Disconnections)**: A player disconnecting their WebSocket connection does **not** decrement eligibility. Their timer continues running; they may reconnect and submit prior to deadline.
* **`PLAY-ELIG-003` (Participant Removal Adjustment)**:
  * If an eligible player is removed by the Host during `QUESTION_ACTIVE` **before submitting an answer**: `effectiveEligibleParticipantCount` decrements by exactly 1.
  * If the player already submitted an accepted answer: the answer, awarded points, and denominator inclusion are preserved.
* **`PLAY-AUTO-001` (Equality Auto-Close)**:
  * Auto-close triggers when `acceptedAnswerCount == effectiveEligibleParticipantCount` for a **non-initially-empty effective set** ($> 0$).
  * The server automatically transitions the game from `QUESTION_ACTIVE` to `QUESTION_RESULTS`.
  * Questions starting with zero eligible participants do **not** auto-close ($0 == 0$ does not trigger). They wait for explicit Host `EndQuestion`.

### 2.4 Single Accepted Answer Ingestion `[NORMATIVE]`
* **`PLAY-ANS-001` (Submission Endpoint)**: SignalR hub method `SubmitAnswer(questionId, choiceIds)` or REST equivalent.
* **`PLAY-ANS-002` (Execution Sequence & Hotspot Contention)**:
  1. Validates participant session token; verifies Host account is not suspended.
  2. Verifies participant is not removed (`Game.ParticipantRemoved`).
  3. Verifies game is in `QUESTION_ACTIVE` and `questionId` matches current active question snapshot.
  4. Evaluates deadline: verifies `NOW() <= endsAt`.
  5. Evaluates multi-tier rate limiting (socket limit and participant-scoped limit).
  6. Validates choices: non-empty, deduplicated, $\le 6$ items, all belonging to the question snapshot.
  7. Atomically within a single transaction:
     * Enforces unique constraint on `(GameId, QuestionId, ParticipantId)`.
     * Inserts `AnswerSubmission` record.
     * Evaluates scoring formula (see [09-scoring-and-leaderboards.md](09-scoring-and-leaderboards.md)).
     * Increments `acceptedAnswerCount` for the question.
     * Evaluates equality auto-close condition.
  8. Returns immediate response: `{ "accepted": true, "alreadyAnswered": false }`.
* **`PLAY-IDEM-001` (Idempotent Resubmission)**:
  * If a participant resubmits for the same question: returns `{ "accepted": true, "alreadyAnswered": true }`.
  * Does not alter previously recorded choices, score, or timestamp.

### 2.5 Multi-Tiered Abuse & Rate Limiting `[NORMATIVE]`
To prevent connection-cycling bypasses where a malicious script reconnects repeatedly to reset per-socket limits:
* **`PLAY-RATE-001` (Connection-Level Rate Limit)**: Maximum 5 submission attempts per rolling 3 seconds per SignalR socket connection.
* **`PLAY-RATE-002` (Participant-Scoped Rate Limit)**: Maximum 10 submission attempts per participant per question across **all connections combined**. Once 10 attempts are reached for a question, further attempts are rejected with `Game.TooManyAnswerAttempts` regardless of socket reconnections.
* **`PLAY-RATE-003` (NAT Non-Penalization)**: Answer limits are bound to the authenticated `ParticipantId` and connection ID, never penalizing unrelated students behind a shared school/office NAT.

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Boundaries Table

| Parameter | Min - 1 | Min | Normal | Max | Max + 1 | Notes | Stable Req ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Submitted Choices** | 0 (Rejected) | 1 (Accepted) | 1 (Accepted) | 6 (Accepted) | 7 (Rejected) | Must match question choices. | `PLAY-BOUND-001` |
| **Submission Timing** | - | $T_{\text{start}}$ (Valid)| $T_{\text{mid}}$ (Valid) | $T_{\text{end}}$ (Valid) | $T_{\text{end}} + 1\text{ms}$ (409)| Server timestamp authority. | `PLAY-BOUND-002` |
| **Per-Socket Burst** | - | 1 attempt | 3 attempts | 5 attempts | 6th attempt (429)| Per rolling 3 seconds / socket. | `PLAY-BOUND-003` |
| **Participant Burst** | - | 1 attempt | 2 attempts | 10 attempts | 11th attempt (429)| Across all reconnections / question. | `PLAY-BOUND-004` |

### 3.2 Canonical Negative Error Codes

| Status / Hub Error | Error Code | Meaning | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **400** | `Game.NotCurrentQuestion` | Question ID does not match current active question snapshot. | `PLAY-ERR-001` |
| **400** | `Game.InvalidChoices` | Choice IDs empty, exceeding 6, or not belonging to question. | `PLAY-ERR-002` |
| **401** | `Game.InvalidSessionToken` | Missing, malformed, or invalid Player session token. | `PLAY-ERR-003` |
| **403** | `Game.ParticipantRemoved` | Participant was explicitly removed by the Host. | `PLAY-ERR-004` |
| **409** | `Game.AnswerTooLate` | Submission arrived after inclusive server deadline. | `PLAY-ERR-005` |
| **409** | `Game.InvalidStateTransition`| Submitting when game is in `LOBBY`, `QUESTION_RESULTS`, `LEADERBOARD`, or `FINISHED`. | `PLAY-ERR-006` |
| **409** | `Game.Unavailable` | Host account is suspended or game is terminated. | `PLAY-ERR-007` |
| **429** | `Game.TooManyAnswerAttempts` | Answer submission rate limit exceeded (per-socket or participant-scoped). | `PLAY-ERR-008` |

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Answer Ingestion Latency SLO Targets `[NORMATIVE]`
Under standard and peak load:

| Metric | Target SLO | Stable Req ID |
| :--- | :--- | :--- |
| $p50$ | $\le 100\text{ ms}$ | `PLAY-SLO-001` |
| $p95$ | $\le 500\text{ ms}$ | |
| $p99$ | $\le 1.0\text{ second}$ | |

### 4.2 Platform Throughput Targets `[NORMATIVE]`
* **`PLAY-CAP-001` (Single Game Burst)**: Ingests **500 answers in $\sim 1\text{ second}$** for a single game session without dropping submissions or timing out.
* **`PLAY-CAP-002` (Platform-Wide Capacity)**: Sustains **5,000 valid answer submissions/second** for at least 5 consecutive seconds across 200 concurrent live games.
* **`PLAY-CAP-003` (Zero Lost Answers)**: Every accepted answer is committed to durable relational storage. Zero lost answers are tolerated under supported load.

---

## 5. Security & Threat Mitigations

### 5.1 Client Clock Spoofing Defense `[NORMATIVE]`
* **`PLAY-SEC-001` (Server Clock Authority)**: Local client clocks are completely ignored. Deadline evaluations rely exclusively on server UTC. Manipulating client clocks or latency headers cannot bypass deadlines.

### 5.2 Pre-Reveal Secrecy Guarantee `[NORMATIVE]`
* **`PLAY-SEC-002` (Zero Information Disclosure)**: Payloads delivered prior to `QUESTION_RESULTS` strictly withhold correctness flags, point calculation hints, and rank modifications.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 State-Changing Commit Outcome Contract `[NORMATIVE]`
* **Outcome A (Failure before commit)**: Answer transaction aborts (e.g., deadlock or pool timeout); zero answer row committed; zero score awarded. Client retries submission.
* **Outcome B (Commit succeeded, response lost)**: Answer committed to PostgreSQL, but socket drops before client receives `{ accepted: true }`. Client reconnects and resubmits: server detects existing `(GameId, QuestionId, ParticipantId)` record and returns `{ accepted: true, alreadyAnswered: true }` without re-scoring.
* **Outcome C (Outcome unknown to caller)**: Network timeout during submission. Client reconnects and invokes `Reconnect`: catch-up payload reports `alreadyAnswered = true` if committed.

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `PLAY-RISK-001` | 500 players submit answers at the exact same millisecond. | Database lock contention or connection pool starvation. | Lightweight insert transaction with unique constraint on `(GameId, QuestionId, ParticipantId)`. Lock wait timeout bounded to 3s. | Row-level locking; retry on transient lock timeout. | `PLAY-TEST-006` |
| `PLAY-RISK-002` | Player submits answer simultaneously with Host clicking `EndQuestion`. | Race between answer commit and state change to `QUESTION_RESULTS`. | Serialized transaction ordering: if answer commits first, scored; if `EndQuestion` commits first, answer rejected with `Game.AnswerTooLate` or `Game.InvalidStateTransition`. | Strict state check inside answer transaction. | `PLAY-TEST-007` |
| `PLAY-RISK-003` | Malicious client disconnects and reconnects to spam answers. | Bypass of per-socket rate limiting. | Participant-scoped rate limiter tracks attempts across all socket instances for that question; rejects beyond 10 attempts with `Game.TooManyAnswerAttempts`. | Multi-tier rate limiting. | `PLAY-TEST-008` |
| `PLAY-RISK-004` | Node clock drifts by 200 ms relative to other cluster nodes. | Inconsistent deadline decisions depending on which node receives answer. | NTP synchronization enforced; node skew $> 50\text{ ms}$ triggers readiness failure; answers check synchronized time. | Node clock health probe. | `PLAY-TEST-009` |
| `PLAY-RISK-005` | Database deadlocks during concurrent answer inserts and score updates. | Transaction aborted and rolled back. | Transient deadlock caught by application; automatic retry up to 2 times within 200 ms before returning failure. | Deadlock retry policy. | `PLAY-TEST-010` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `PLAY-TEST-001` | `PLAY-ANS-001`, `PLAY-ACT-001` | Functional | Player submits single correct choice before deadline. | `{ accepted: true, alreadyAnswered: false }`; points stored. |
| `PLAY-TEST-002` | `PLAY-TIME-002`, `PLAY-ERR-005` | Boundary | Player submits answer after server deadline expires. | Rejected with `Game.AnswerTooLate`. |
| `PLAY-TEST-003` | `PLAY-AUTO-001` | Functional | All 50 eligible players submit answers. | Game automatically transitions to `QUESTION_RESULTS`. |
| `PLAY-TEST-004` | `PLAY-ELIG-003`, `PLAY-AUTO-001` | Functional | Host kicks unanswered player during active question. | Eligibility decrements by 1; if equal to answers, auto-close triggers. |
| `PLAY-TEST-005` | `PLAY-IDEM-001` | Idempotency | Player submits choice A, then resubmits choice B for same question. | First accepted; second returns `alreadyAnswered = true`; choice A preserved. |
| `PLAY-TEST-006` | `PLAY-CAP-001`, `PLAY-SLO-001` | Non-Functional | Ingest 500 answers within 1 second for single game. | All 500 processed; latency meets $p95 \le 500\text{ ms}$, $p99 \le 1.0\text{ s}$. |
| `PLAY-TEST-007` | `PLAY-ANS-002`, `PLAY-RISK-002` | Concurrency | Host calls `EndQuestion` concurrently with 20 player answers. | Serialized cleanly: pre-commit answers scored; post-commit answers rejected. |
| `PLAY-TEST-008` | `PLAY-RATE-002`, `PLAY-RISK-003` | Security | Attacker submits spam answers, reconnects socket, and continues spamming. | Blocked at 11th attempt across all connections with `Game.TooManyAnswerAttempts`. |
| `PLAY-TEST-009` | `PLAY-TIME-001`, `PLAY-RISK-004` | Non-Functional | Simulate 100 ms clock drift on worker node. | Drift alert or fallback to DB time; deadline decisions remain consistent within cluster. |
| `PLAY-TEST-010` | `PLAY-ANS-002`, `PLAY-RISK-005` | Fault Injection | Inject transient database deadlock during answer commit. | Application retries internally; answer commits successfully without error to user. |
| `PLAY-TEST-011` | `PLAY-CAP-002` | Non-Functional | Platform-wide burst test: 5,000 answers/sec for 5 seconds. | Zero lost answers; database integrity verified. |
