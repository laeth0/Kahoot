# 09. Scoring Engine and Leaderboards

This document defines the normative requirements for deterministic speed-scaled scoring, exact-set correctness evaluation, live leaderboard ranking, tie-breaking rules, and podium presentation. It unifies functional scoring algorithms and non-functional execution latency SLOs into a single specification.

---

## 1. Topic Overview & Actors

The scoring engine evaluates participant answer submissions and calculates competitive standings:
* **Internal Scoring Engine**: Server-side domain component responsible for awarding points.
* **Registered User / Host**: Views live leaderboard standings, answer distributions, and final podium.
* **Player / Participant**: Receives personal score updates, correctness feedback (post-reveal), and rank.
* **Core Invariants `[NORMATIVE]`**:
  * Scoring is deterministic, server-authoritative, and strictly non-decreasing.
  * Correctness requires **exact set equality**: every correct choice and zero incorrect choices.
  * **Approved Exclusions**: Streak multipliers, power-ups, and negative penalty points are strictly excluded per approved product decisions.

---

## 2. Functional Specification & Workflows

### 2.1 Deterministic Speed-Scaled Scoring Formula `[NORMATIVE]`
* **`SCORE-EXACT-001` (Exact-Set Correctness)**:
  * Let $C_{\text{actual}}$ be the set of choice IDs submitted by the participant.
  * Let $C_{\text{correct}}$ be the set of choice IDs where `isCorrect == true` in the question snapshot.
  * If $C_{\text{actual}} \neq C_{\text{correct}}$, awarded points equal exactly **0**. Partial credit and negative penalties are strictly prohibited.
* **`SCORE-FORM-001` (Formula & Rounding)**:
  For an exact-match correct submission with base points $B$ and duration seconds $D$, where server-measured elapsed response time $t$ is clamped to $[0, D]$:
  $$\text{Points} = \text{roundAwayFromZero}\left(B \times \left(1 - 0.5 \times \frac{t}{D}\right)\right)$$
* **`SCORE-ROUND-001` (Boundary Invariants)**:
  * Immediate submission ($t = 0$): Awards exactly **$B$ points** (100% of base points).
  * Deadline submission ($t = D$): Awards exactly **$\text{roundAwayFromZero}(B / 2)$ points** (50% of base points).
  * Half-point rounding: For odd $B$, midpoints round away from zero (e.g., $B = 101, t = D \implies 51\text{ points}$).
  * Zero points base: If $B = 0$, awarded points equal 0 regardless of response time.

### 2.2 Question Results Materialization `[NORMATIVE]`
* **`SCORE-RES-001` (Aggregation Calculation)**:
  * Triggered when game transitions to `QUESTION_RESULTS` (via auto-close or Host `EndQuestion`).
  * Aggregations materialized:
    * Total accepted answers for the question.
    * Per-choice selection counts (e.g., Choice A: 320, Choice B: 45, Choice C: 12, Choice D: 3).
    * List of correct choice IDs.
    * Historical denominator: `effectiveEligibleParticipantCount`.
* **`SCORE-RES-002` (Broadcast Privacy)**:
  * Host receives full aggregate distribution and correct choices.
  * Players receive event indicating whether their personal submission was correct, points earned, and updated personal total score.

### 2.3 Live Leaderboard & Podium Ranking `[NORMATIVE]`
* **`SCORE-RANK-001` (Deterministic Rank Ordering)**:
  Evaluates all **non-removed participants** in the game session, ordered deterministically by:
  1. `TotalScore DESC` (highest score ranked 1st)
  2. `NormalizedNickname ASC` (culture-independent case-insensitive alphabetical tie-breaker)
  3. `ParticipantId ASC` (durable UUID tie-breaker)
  Ranks are strictly sequential integers starting at 1 ($1, 2, 3, \dots, N$).
* **`SCORE-EXCLUDE-001` (Exclusion of Removed Participants)**:
  * Participants marked `IsRemoved == true` are **strictly excluded** from leaderboard standings and final podiums.
  * If a removed participant submitted an accepted answer before removal, their choices remain in the aggregate question distribution, but their name and score do not appear on the leaderboard.

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Boundaries Table

| Parameter | Boundary Condition | Expected Behavior | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **Response Time $t$** | $t < 0$ (clock anomaly) | Clamped to $t = 0$ (awards $B$ points). | `SCORE-BOUND-001` |
| **Response Time $t$** | $t > D$ (late submission) | Rejected before scoring with `Game.AnswerTooLate`. | `SCORE-BOUND-002` |
| **Base Points $B$** | $B = 0$ | Always awards 0 points for correct answer. | `SCORE-BOUND-003` |
| **Partial Choice Match** | 2 of 3 correct choices selected | Awards 0 points (exact set match required). | `SCORE-BOUND-004` |
| **Extra Wrong Choice** | All correct choices + 1 wrong choice | Awards 0 points. | `SCORE-BOUND-005` |
| **Tied Scores** | Two players with identical 8,500 points | Deterministically ordered by `NormalizedNickname`. | `SCORE-BOUND-006` |
| **Odd Base Points** | $B = 101, t = D$ | Awards exactly 51 points (round away from zero). | `SCORE-BOUND-007` |

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Scoring Engine Execution Latency `[NORMATIVE]`
* **`SCORE-SLO-001` (Calculation Speed)**: Scoring algorithm evaluation must execute in-memory with zero disk I/O:
  * $p99 \le 5\text{ ms}$ per accepted answer.
* Scoring must not serialize parallel answer submissions from other players.

### 4.2 Leaderboard Materialization Performance `[NORMATIVE]`
* **`SCORE-SLO-002` (Leaderboard Generation Speed)**: For a full 500-player game session, sorting and materializing leaderboard ranks must complete in:
  * $p95 \le 100\text{ ms}$
  * $p99 \le 250\text{ ms}$

### 4.3 Integer Overflow Safety `[NORMATIVE]`
* **`SCORE-OVERFLOW-001` (Checked 64-Bit Arithmetic)**:
  * `TotalScore` is stored as a signed 64-bit integer (`BIGINT` in PostgreSQL).
  * Point additions must execute using checked arithmetic (`checked(participant.TotalScore += points)`).
  * Maximum attainable score across all 200 questions cannot exceed `long.MaxValue` ($9,223,372,036,854,775,807$).

---

## 5. Security & Threat Mitigations

### 5.1 Server-Authoritative Scoring Integrity `[NORMATIVE]`
* **`SCORE-SEC-001` (No Client Score Injection)**: Points are calculated strictly server-side. Payloads from clients contain only `choiceIds`. Any client-supplied `points` or `score` property is discarded or causes rejection.

### 5.2 Pre-Reveal Rank Withholding `[NORMATIVE]`
* **`SCORE-SEC-002` (Provisional Score Secrecy)**: During `QUESTION_ACTIVE`, provisional points earned on the active question are withheld from all player-visible leaderboard queries and reconnect payloads until the reveal phase.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 State-Changing Commit Outcome Contract `[NORMATIVE]`
* **Outcome A (Failure before commit)**: Score update rolls back with answer submission; no partial score added. Caller retries submission.
* **Outcome B (Commit succeeded, response lost)**: Points and answer committed to PostgreSQL. Subsequent replay returns `alreadyAnswered: true`; points are not awarded a second time.
* **Outcome C (Outcome unknown to caller)**: Handled via reconnect catch-up: player's score reflects the committed total.

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `SCORE-RISK-001` | Concurrent answers for same participant. | Double-counting points for the same question. | Unique DB constraint on `(GameId, QuestionId, ParticipantId)` ensures points added exactly once. | Atomic transaction rollback. | `SCORE-TEST-007` |
| `SCORE-RISK-002` | Clamped negative response time ($t < 0$). | Point calculation exceeding 100% of base points. | Formula clamps $t$ to minimum 0; awards exactly $B$ points. | Bounded clamping logic. | `SCORE-TEST-008` |
| `SCORE-RISK-003` | Host kicks player while leaderboard materialization is executing. | Inconsistent ranking output. | Materialization filters `IsRemoved == false` under read committed transaction. | Clean query predicate. | `SCORE-TEST-009` |
| `SCORE-RISK-004` | 500 players achieve identical zero scores. | Indeterminate or unstable leaderboard ordering. | Multi-level tie-breaker: `TotalScore DESC`, `NormalizedNickname ASC`, `ParticipantId ASC`. | Deterministic 3-tuple sorting. | `SCORE-TEST-006` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `SCORE-TEST-001` | `SCORE-FORM-001`, `SCORE-ROUND-001` | Functional | Player submits exact correct choice at $t = 0\text{s}$ with $B = 1000, D = 30\text{s}$. | Awards exactly 1,000 points. |
| `SCORE-TEST-002` | `SCORE-FORM-001` | Functional | Player submits exact correct choice at $t = 15\text{s}$ with $B = 1000, D = 30\text{s}$. | Awards exactly 750 points. |
| `SCORE-TEST-003` | `SCORE-FORM-001` | Functional | Player submits exact correct choice at $t = 30\text{s}$ with $B = 1000, D = 30\text{s}$. | Awards exactly 500 points. |
| `SCORE-TEST-004` | `SCORE-EXACT-001`, `SCORE-BOUND-004` | Functional | Multi-select question with choices A & B correct; player submits only A. | Awards 0 points (partial match rejected). |
| `SCORE-TEST-005` | `SCORE-EXACT-001`, `SCORE-BOUND-005` | Functional | Multi-select question with choices A & B correct; player submits A, B, and C. | Awards 0 points (extra wrong choice rejected). |
| `SCORE-TEST-006` | `SCORE-RANK-001`, `SCORE-BOUND-006` | Functional | Two players have identical scores (5,000 pts) with nicknames `"Bob"` and `"Alice"`. | `"Alice"` ranked above `"Bob"` via deterministic tie-breaker. |
| `SCORE-TEST-007` | `SCORE-FORM-001`, `SCORE-RISK-001` | Concurrency | Two parallel answer submissions commit for same participant on same question. | Exactly one row committed; points added once. |
| `SCORE-TEST-008` | `SCORE-BOUND-001`, `SCORE-RISK-002` | Boundary | Simulated response time $t = -50\text{ ms}$ due to slight clock difference. | Response time clamped to 0; awards exactly base points $B$. |
| `SCORE-TEST-009` | `SCORE-EXCLUDE-001`, `SCORE-RISK-003` | Functional | Host kicks player who had 8,000 points; Host views leaderboard. | Removed player completely excluded from leaderboard standings and podium. |
| `SCORE-TEST-010` | `SCORE-ROUND-001`, `SCORE-BOUND-007` | Boundary | Test scoring formula with odd base points ($B = 101, t = D$). | Awards exactly 51 points (round away from zero verified). |
| `SCORE-TEST-011` | `SCORE-SLO-001`, `SCORE-SLO-002` | Non-Functional | Measure scoring evaluation ($p99 \le 5\text{ ms}$) and leaderboard generation ($p95 \le 100\text{ ms}$) for 500 players. | Latencies satisfy SLO targets; zero memory leaks. |
