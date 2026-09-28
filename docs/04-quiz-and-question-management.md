# 04. Quiz and Question Authoring

This document defines the normative requirements for authoring quizzes, questions, text-only choices, question image attachments, question reordering, publication validation, immutable snapshot creation, and quiz deletion. It unifies both functional specifications and non-functional requirements (technical safety bounds, publication latency SLOs, integer overflow prevention, and concurrency rules) into a single document.

---

## 1. Topic Overview & Actors

Quiz authoring allows Hosts to create structured educational content:
* **Registered User / Host**: The exclusive actor for quiz authoring within their own tenant boundary.
* **System Administrator**: Strictly excluded from browsing or modifying quiz content.
* **Player / Participant**: Encounters quiz content only as immutable game snapshots broadcast during active gameplay.
* **State Lifecycle of a Quiz `[NORMATIVE]`**: Exactly two publication states are supported:
  * `Unpublished (Draft)`: Modifiable; questions/choices can be added, updated, reordered, or deleted. Cannot be used to launch a game.
  * `Published`: Validated complete quiz version. Eligible to launch live games. Any content modification automatically reverts status to `Unpublished`.

---

## 2. Functional Specification & Workflows

### 2.1 Quiz Creation & Metadata Management `[NORMATIVE]`
* **`QUIZ-AUTH-001` (Create Quiz)**:
  * **Endpoint**: `POST /api/quizzes`
  * **Payload**: `{ "title": "Math 101 Quiz", "description": "Optional overview..." }`
  * **Rules**: `title` is required, trimmed, 1–200 characters. `description` is optional, trimmed, max 1,000 characters.
  * Assigns `HostAccountId` from the authenticated Host account ID. Sets `IsPublished = false`, `Revision = 1`.
  * Returns `201 Created` with quiz summary.
* **`QUIZ-AUTH-002` (Update Quiz Metadata)**:
  * **Endpoint**: `PUT /api/quizzes/{quizId}`
  * **Rules**: Updates title and description. Any edit immediately resets `IsPublished = false` and increments `Revision`.
* **`QUIZ-DEL-001` (Delete Rules & Lifecycle Invariants)**:
  * **Endpoint**: `DELETE /api/quizzes/{quizId}`
  * **Never-Played Invariant**: A quiz that has **never** been used to launch a game session may be permanently deleted. Its question images become eligible for orphan retention once no question or game snapshot references them.
  * **Ever-Played Invariant**: A quiz that has been used in **any** game session (active or finished) can **never** be deleted, returning `409 Quiz.HasSessions` to preserve historical integrity.
  * **Active Session Lock**: If a game session launched from this quiz is currently unfinished, deletion is rejected with `409 Quiz.InUse`.

### 2.2 Question CRUD and Choice Authoring `[NORMATIVE]`
* **`QUIZ-QUEST-001` (Add Question)**:
  * **Endpoint**: `POST /api/quizzes/{quizId}/questions`
  * **Payload**:
    ```json
    {
      "text": "What is the capital of France?",
      "imageId": "550e8400-e29b-41d4-a716-446655440001",
      "durationSeconds": 30,
      "basePoints": 1000,
      "choices": [
        { "text": "Paris", "isCorrect": true },
        { "text": "London", "isCorrect": false },
        { "text": "Berlin", "isCorrect": false },
        { "text": "Madrid", "isCorrect": false }
      ]
    }
    ```
  * Appends question to the end of the quiz with contiguous zero-based `OrderIndex`. A question has at most one optional image; answers and choices remain text-only.
  * Question create/update and quiz detail responses include nullable `imageId` and `imageUrl`; `imageUrl` is the immutable public `/uploads/` path returned for the stored question image.
  * Technical Limit Check: Fails with `400 Validation.Failed` if quiz already contains 200 questions.
  * Resets `IsPublished = false`, increments quiz `Revision`.
* **`QUIZ-QUEST-002` (Update Question)**:
  * **Endpoint**: `PUT /api/quizzes/{quizId}/questions/{questionId}`
  * Updates text, duration, points, choices, or image. Replaces the complete choice set atomically.
  * Resets `IsPublished = false`, increments quiz `Revision`.
* **`QUIZ-QUEST-003` (Delete Question)**:
  * **Endpoint**: `DELETE /api/quizzes/{quizId}/questions/{questionId}`
  * Removes question and its choices. Re-indexes remaining questions to maintain a contiguous sequence (`0, 1, 2, ...`).
  * Resets `IsPublished = false`, increments quiz `Revision`.

### 2.3 Question Reordering `[NORMATIVE]`
* **`QUIZ-REORDER-001` (Reorder Endpoint)**:
  * **Endpoint**: `POST /api/quizzes/{quizId}/reorder`
  * **Payload**:
    ```json
    {
      "questionIds": ["qst_03", "qst_01", "qst_02"]
    }
    ```
  * **Validation**: Payload must contain a 1-to-1 permutation of all question IDs currently belonging to the quiz (zero missing, extra, or duplicate IDs).
  * Updates `OrderIndex` values in a single atomic transaction. Resets `IsPublished = false`, increments `Revision`.

### 2.4 Publication Validation `[NORMATIVE]`
* **`QUIZ-PUB-001` (Publication Rules)**:
  * **Endpoint**: `POST /api/quizzes/{quizId}/publish`
  * A quiz transitions to `IsPublished = true` if and only if **all** of the following validation rules pass:
    1. Contains between **1 and 200 questions** (inclusive).
    2. Every question has non-whitespace `text` (1–500 chars).
    3. Every question has `durationSeconds` between **5 and 300 seconds** (inclusive).
    4. Every question has `basePoints` between **0 and 2,147,483,647** (inclusive).
    5. Every question has between **2 and 6 choices** (inclusive).
    6. Every choice has non-whitespace `text` (1–300 chars).
    7. Every question has **at least 1 choice** marked `isCorrect = true` (multiple correct choices permitted).
    8. Any referenced `imageId` identifies a committed question image owned by the **same Host tenant** and attached only to that question.
    9. Arithmetic Safety: Total potential maximum score does not overflow signed 64-bit integer (`long.MaxValue`).
  * **Outcome**: Sets `IsPublished = true`, increments `Revision`. Retrying publication of an unchanged valid quiz is an idempotent `200 OK`.

### 2.5 Bounded Keyset Pagination Queries `[NORMATIVE]`
* **`QUIZ-QUERY-001` (List Quizzes)**:
  * `GET /api/quizzes?pageSize=50&cursor=...`
  * Returns owned quizzes ordered by `CreatedAt DESC, QuizId DESC`.
  * Response includes items, `pageSize`, and opaque `nextCursor`.
* **`QUIZ-QUERY-002` (Get Quiz Details)**:
  * `GET /api/quizzes/{quizId}`: Returns full quiz metadata and ordered questions with choices. Returns `404 Quiz.NotFound` for foreign or non-existent quizzes.

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Technical Safety Limit: Maximum 200 Questions `[NORMATIVE]`
* **`QUIZ-LIMIT-001` (200-Question Bound)**:
  * A quiz may have a maximum of **200 questions**.
  * This is a technical system safety limit to ensure bounded memory allocations and snapshot creation time, NOT a commercial plan quota.
  * Boundary test cases:
    * 0 questions: Rejected on publish (`400 Validation.Failed`).
    * 1 question: Accepted on add and publish.
    * 199 questions: Accepted.
    * 200 questions: Accepted.
    * 201 questions: Rejected on add question (`400 Validation.Failed`).

### 3.2 Field & Collection Boundaries Table

| Parameter | Min - 1 | Min | Normal | Max | Max + 1 | Rule / Error | Stable Req ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Quiz Title** | 0 (400) | 1 (200) | 30 (200) | 200 (200) | 201 (400) | Required; trimmed. | `QUIZ-BOUND-001` |
| **Quiz Description** | - | 0 (null) | 100 (200) | 1,000 (200) | 1,001 (400) | Optional; trimmed. | `QUIZ-BOUND-002` |
| **Questions per Quiz** | 0 (400) | 1 (200) | 20 (200) | 200 (200) | 201 (400) | Technical safety limit. | `QUIZ-BOUND-003` |
| **Question Text** | 0 (400) | 1 (200) | 50 (200) | 500 (200) | 501 (400) | Non-whitespace. | `QUIZ-BOUND-004` |
| **Choices per Question**| 1 (400) | 2 (200) | 4 (200) | 6 (200) | 7 (400) | Text-only choices. | `QUIZ-BOUND-005` |
| **Choice Text** | 0 (400) | 1 (200) | 20 (200) | 300 (200) | 301 (400) | Non-whitespace. | `QUIZ-BOUND-006` |
| **Duration Seconds** | 4 (400) | 5 (200) | 30 (200) | 300 (200) | 301 (400) | Integer seconds. | `QUIZ-BOUND-007` |
| **Base Points** | -1 (400) | 0 (200) | 1,000 (200) | 2,147,483,647 | 2,147,483,648 | Signed 32-bit integer range. | `QUIZ-BOUND-008` |

### 3.3 Canonical Negative Error Codes

| Status | Code | Meaning | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **400** | `Validation.Failed` | Boundary violation, empty text, or points overflow. | `QUIZ-ERR-001` |
| **400** | `Quiz.QuestionSetMismatch` | Reorder list does not match current quiz question IDs. | `QUIZ-ERR-002` |
| **400** | `Quiz.InvalidImageReference` | Image ID does not exist, is incomplete, belongs to another tenant, is attached to a different question, or was removed by cleanup. | `QUIZ-ERR-003` |
| **404** | `Quiz.NotFound` | Quiz does not exist or belongs to another Host tenant. | `QUIZ-ERR-004` |
| **404** | `Quiz.QuestionNotFound` | Question does not exist within the specified quiz. | `QUIZ-ERR-005` |
| **409** | `Quiz.InUse` | Attempting to edit a quiz while an active game session is running. | `QUIZ-ERR-006` |
| **409** | `Quiz.HasSessions` | Attempting to delete a quiz that has ever been played. | `QUIZ-ERR-007` |
| **409** | `Quiz.ConcurrentModification` | Revision conflict during concurrent edits. | `QUIZ-ERR-008` |

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Latency SLO Targets `[NORMATIVE]`
Under standard operational load:

| Operation | Metric | Target SLO | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **Publish 200-Question Quiz** | $p95$ | $\le 300\text{ ms}$ | `QUIZ-SLO-001` |
| **Create Game Snapshot (200 Questions)** | $p95$ | $\le 200\text{ ms}$ | `QUIZ-SLO-002` |
| **Reorder Questions (200 Questions)** | $p95$ | $\le 250\text{ ms}$ | `QUIZ-SLO-003` |
| **Fetch Quiz Details with Questions** | $p95$ | $\le 100\text{ ms}$ | `QUIZ-SLO-004` |
| **List Quizzes (Keyset PageSize=50)** | $p95$ | $\le 80\text{ ms}$ | `QUIZ-SLO-005` |

### 4.2 Score Arithmetic Overflow Protection `[NORMATIVE]`
* **`QUIZ-OVERFLOW-001` (Checked Arithmetic)**:
  * Maximum potential score aggregation across all questions must execute using checked 64-bit signed integer arithmetic (`checked(sum += basePoints)`).
  * If the total potential score exceeds `long.MaxValue` ($9,223,372,036,854,775,807$), publication fails with `400 Validation.Failed`.

---

## 5. Security & Threat Mitigations

### 5.1 Cross-Tenant Image Attachment Defense `[NORMATIVE]`
* **`QUIZ-SEC-001` (Tenant Match on Attachment)**:
  * When attaching a `imageId` to a question, the server verifies a committed `QuestionImage` row with the same `HostAccountId` as the question. `Question.ImageId` has a tenant-matched foreign key and a unique index on `(ImageId, HostAccountId)`, so one image cannot be attached to multiple current questions.
  * An image that is missing, foreign, attached to another question, or already deleted by cleanup returns `400 Quiz.InvalidImageReference`. The tenant-matched foreign key and cleanup row lock serialize attachment with deletion.

### 5.2 Content Sanitization & XSS Defense `[NORMATIVE]`
* **`QUIZ-SEC-002` (Plain-Text Handling)**:
  * Question and choice text are stored as plain scalar strings. HTML tags are treated as literal text and HTML-encoded upon rendering.
  * No executable script tags or markdown macros are evaluated.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 State-Changing Commit Outcome Contract `[NORMATIVE]`
* **Outcome A (Failure before commit)**: Edit, publish, or reorder transaction rolls back; quiz remains in previous draft/published state; caller retries safely.
* **Outcome B (Commit succeeded, response lost)**:
  * Publish: Retrying `POST /api/quizzes/{id}/publish` with same state returns idempotent `200 OK`.
  * Update: Resubmission with old `revision` returns `409 Quiz.ConcurrentModification`. Client re-fetches latest state.
* **Outcome C (Outcome unknown to caller)**: Caller queries `GET /api/quizzes/{quizId}` to inspect `Revision` and `IsPublished` status before prompting user.

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `QUIZ-RISK-001` | Host edits quiz while a live game session is active. | Live game players receive mutated questions or crash mid-game. | Request rejected with `409 Quiz.InUse`. Game runs off immutable snapshot. | Host must wait until all games reach `FINISHED`. | `QUIZ-TEST-007` |
| `QUIZ-RISK-002` | Two browser tabs concurrently edit same quiz. | Lost updates or overwritten questions. | Optimistic concurrency via `Revision` column; first commits, second receives `409 Quiz.ConcurrentModification`. | Client reloads fresh quiz and reapplies edit. | `QUIZ-TEST-008` |
| `QUIZ-RISK-003` | Host creates 200 questions with massive base points causing integer overflow. | Runtime exceptions or corrupted scores in database. | Publication validation evaluates checked sum against `long.MaxValue`; rejects with `400 Validation.Failed`. | Bounded score math. | `QUIZ-TEST-009` |
| `QUIZ-RISK-004` | Question deletion race against game creation snapshot. | Game snapshot captures partial or inconsistent question set. | Game creation executes in serializable/snapshot isolation transaction; snapshot captures consistent committed state. | Read committed snapshot isolation. | `QUIZ-TEST-010` |
| `QUIZ-RISK-005` | Image cleanup runs while Host attaches an image to a question. | Question references a missing file. | Cleanup locks and rechecks the image row; the foreign key prevents a committed question reference to a deleted row. | Database-first cleanup. | `QUIZ-TEST-011` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `QUIZ-TEST-001` | `QUIZ-AUTH-001` | Functional | Host creates quiz with title and description. | `201 Created`; `IsPublished = false`, `Revision = 1`. |
| `QUIZ-TEST-002` | `QUIZ-QUEST-001` | Functional | Host adds question with 4 choices (1 correct). | `200 OK`; contiguous `OrderIndex` assigned. |
| `QUIZ-TEST-003` | `QUIZ-PUB-001` | Functional | Host publishes valid 10-question quiz. | `200 OK`; `IsPublished = true`. |
| `QUIZ-TEST-004` | `QUIZ-AUTH-002` | Functional | Host updates question title on published quiz. | `200 OK`; `IsPublished` automatically resets to `false`. |
| `QUIZ-TEST-005` | `QUIZ-DEL-001` | Functional | Host attempts to delete a quiz used in a finished game. | Rejected with `409 Quiz.HasSessions`. |
| `QUIZ-TEST-006` | `QUIZ-LIMIT-001`, `QUIZ-BOUND-003` | Boundary | Add 200 questions to quiz; attempt to add 201st question. | 200th question succeeds; 201st rejected with `400 Validation.Failed`. |
| `QUIZ-TEST-007` | `QUIZ-ERR-006`, `QUIZ-RISK-001` | Concurrency | Host attempts to update question text while game is in `QUESTION_ACTIVE`. | Rejected with `409 Quiz.InUse`. |
| `QUIZ-TEST-008` | `QUIZ-ERR-008`, `QUIZ-RISK-002` | Concurrency | Concurrent question insertion and reordering against same quiz revision. | Exactly one commits; second fails with `409 Quiz.ConcurrentModification`. |
| `QUIZ-TEST-009` | `QUIZ-OVERFLOW-001`, `QUIZ-RISK-003` | Boundary | Publish quiz where base points sum overflows `long.MaxValue`. | Validation fails with `400 Validation.Failed`. |
| `QUIZ-TEST-010` | `QUIZ-PUB-001`, `QUIZ-SLO-001` | Non-Functional | Measure publication latency of maximum 200-question quiz. | Meets $p95 \le 300\text{ ms}$. |
| `QUIZ-TEST-011` | `QUIZ-SEC-001`, `QUIZ-RISK-005` | Security | Host A attempts to attach Host B's uploaded `imageId` to Question. | Rejected with `400 Quiz.InvalidImageReference`. |
