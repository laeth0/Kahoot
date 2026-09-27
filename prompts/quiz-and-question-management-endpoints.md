# Quiz and Question Authoring Endpoints

All endpoints in this catalog require authentication with the `Host` role (`UserRole.Host`).

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             QUIZ ENDPOINTS                                  │
├──────────────────────┬──────────────────────────────────────────────────────┤
│ POST   /api/quizzes  │ Create a new unpublished quiz draft                  │
│ GET    /api/quizzes  │ List owned quizzes (keyset cursor pagination)        │
│ GET    /api/quizzes/{quizId} │ Get quiz details with questions & choices    │
│ PUT    /api/quizzes/{quizId} │ Update quiz metadata (title & description)   │
│ DELETE /api/quizzes/{quizId} │ Delete quiz (never-played invariant)         │
├──────────────────────┴──────────────────────────────────────────────────────┤
│                           QUESTION ENDPOINTS                                │
├──────────────────────────────────────┬──────────────────────────────────────┤
│ POST   /api/quizzes/{quizId}/questions            │ Append question & choices│
│ PUT    /api/quizzes/{quizId}/questions/{questionId} │ Replace question & choices│
│ DELETE /api/quizzes/{quizId}/questions/{questionId} │ Remove question & re-index│
├──────────────────────────────────────┴──────────────────────────────────────┤
│                    REORDER & PUBLISH ENDPOINTS                              │
├──────────────────────────────────────┬──────────────────────────────────────┤
│ POST   /api/quizzes/{quizId}/reorder │ Reorder questions atomically         │
│ POST   /api/quizzes/{quizId}/publish │ Validate invariants & publish quiz   │
└──────────────────────────────────────┴──────────────────────────────────────┘
```

---

### Group A: Quiz Management

#### 1. `POST /api/quizzes` (Create Quiz)
- **Requirement ID**: `QUIZ-AUTH-001`
- **Description**: Creates a new unpublished quiz draft belonging to the authenticated Host.
- **Request Body**:
  ```json
  {
    "title": "World Geography Trivia",
    "description": "A fun trivia quiz about world capitals and landmarks."
  }
  ```
- **Validation Rules**:
  - `title`: Required, trimmed string, 1 to 200 characters (`QUIZ-BOUND-001`).
  - `description`: Optional, trimmed string, max 1,000 characters (`QUIZ-BOUND-002`). Null if empty.
- **Business Logic**:
  - Assigns `HostAccountId` from the caller's JWT token claims.
  - Sets `IsPublished = false`.
  - Sets `Revision = 1`.
  - Generates new `Guid` for `QuizId`.
- **Response**: `201 Created`
  - `Location: /api/quizzes/01923485-9831-7abc-9f5a-3829482910aa`
  ```json
  {
    "id": "01923485-9831-7abc-9f5a-3829482910aa",
    "title": "World Geography Trivia",
    "description": "A fun trivia quiz about world capitals and landmarks.",
    "isPublished": false,
    "revision": 1,
    "questionCount": 0,
    "createdAt": "2026-09-27T18:00:00Z",
    "updatedAt": "2026-09-27T18:00:00Z"
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Title missing or exceeding 200 chars; description exceeding 1,000 chars.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: User is not in `Host` role (e.g., `SystemAdmin`).

---

#### 2. `GET /api/quizzes` (List Quizzes)
- **Requirement ID**: `QUIZ-QUERY-001`
- **Description**: Returns a paginated list of quizzes owned exclusively by the authenticated Host.
- **Query Parameters**:
  - `pageSize` (`int`, optional, default: `50`, min: `1`, max: `100`): Maximum items per page.
  - `cursor` (`string?`, optional): Opaque base64-encoded cursor representing `(CreatedAt, Id)`.
- **Ordering**: Strict deterministic ordering `CreatedAt DESC, Id DESC`.
- **Tenant Isolation (`RA-ISOL-001`)**: Filtered strictly by `HostAccountId == CurrentHostAccountId`. Never returns quizzes belonging to other tenants.
- **Response**: `200 OK`
  ```json
  {
    "items": [
      {
        "id": "01923485-9831-7abc-9f5a-3829482910aa",
        "title": "World Geography Trivia",
        "description": "A fun trivia quiz about world capitals and landmarks.",
        "isPublished": false,
        "revision": 2,
        "questionCount": 12,
        "createdAt": "2026-09-27T18:00:00Z",
        "updatedAt": "2026-09-27T18:30:00Z"
      }
    ],
    "nextCursor": "ZXlKaGJHY2lPaUpTVXpVeE1pSXNJblI1Y0NJNklrcFhWQ0o5...",
    "hasMore": true
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Invalid cursor format or `pageSize` outside [1, 100].
  - `401 Auth.Unauthorized`: Missing or invalid JWT.

---

#### 3. `GET /api/quizzes/{quizId}` (Get Quiz Details)
- **Requirement ID**: `QUIZ-QUERY-002`
- **Description**: Retrieves full details of a single quiz, including its ordered list of questions and their respective choices.
- **Route Parameters**:
  - `quizId` (`Guid`): The unique ID of the quiz.
- **Security (`RA-ISOL-001`)**: If the quiz does not exist OR belongs to another Host tenant, the endpoint returns `404 Quiz.NotFound` (IDOR concealment).
- **Response**: `200 OK`
  ```json
  {
    "id": "01923485-9831-7abc-9f5a-3829482910aa",
    "title": "World Geography Trivia",
    "description": "A fun trivia quiz about world capitals and landmarks.",
    "isPublished": true,
    "revision": 3,
    "createdAt": "2026-09-27T18:00:00Z",
    "updatedAt": "2026-09-27T19:00:00Z",
    "questions": [
      {
        "id": "01923485-9999-7abc-9f5a-111111111111",
        "orderIndex": 0,
        "text": "What is the capital of France?",
        "mediaId": "01923485-aaaa-7abc-9f5a-222222222222",
        "durationSeconds": 30,
        "basePoints": 1000,
        "choices": [
          {
            "id": "01923485-bbbb-7abc-9f5a-333333333333",
            "orderIndex": 0,
            "text": "Paris",
            "isCorrect": true
          },
          {
            "id": "01923485-cccc-7abc-9f5a-444444444444",
            "orderIndex": 1,
            "text": "London",
            "isCorrect": false
          }
        ]
      }
    ]
  }
  ```
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.

---

#### 4. `PUT /api/quizzes/{quizId}` (Update Quiz Metadata)
- **Requirement ID**: `QUIZ-AUTH-002`
- **Description**: Updates the top-level title and description of a quiz.
- **Route Parameters**:
  - `quizId` (`Guid`): The unique ID of the quiz.
- **Request Body**:
  ```json
  {
    "title": "Advanced World Geography",
    "description": "Updated overview with tricky questions."
  }
  ```
- **Business Rules**:
  - **Active Session Lock (`QUIZ-ERR-006`)**: If any live game session launched from this quiz is currently active (`Status != Finished`), returns `409 Quiz.InUse`.
  - **Auto-Unpublish**: Any metadata update resets `IsPublished = false` and increments `Revision` (`Revision++`).
- **Response**: `200 OK` (returns updated quiz summary).
- **Error Codes**:
  - `400 Validation.Failed`: Title missing or exceeding 200 chars; description exceeding 1,000 chars.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.
  - `409 Quiz.InUse`: An active live game session is running for this quiz.

---

#### 5. `DELETE /api/quizzes/{quizId}` (Delete Quiz)
- **Requirement ID**: `QUIZ-DEL-001`
- **Description**: Deletes a quiz permanently, subject to strict lifecycle invariants.
- **Route Parameters**:
  - `quizId` (`Guid`): The unique ID of the quiz.
- **Lifecycle Invariants (`[NORMATIVE]`)**:
  - **Ever-Played Invariant (`QUIZ-ERR-007`)**: If this quiz has **ever** been used to launch a game session (whether finished or ongoing, checked via `SourceQuizId`), deletion is permanently forbidden and rejected with `409 Quiz.HasSessions` to preserve historical analytics and integrity.
  - **Active Session Lock (`QUIZ-ERR-006`)**: If an active game session is currently ongoing, deletion is rejected with `409 Quiz.InUse`.
  - **Never-Played Invariant**: If the quiz has never been used in any game session, it is permanently deleted along with all its child questions and choices.
  - **Media Reference Reconciliation (`MED-LIFE-001`)**: Any media items referenced solely by this quiz become unreferenced (`UnreferencedSince = UtcNow`).
- **Response**: `204 No Content`
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.
  - `409 Quiz.HasSessions`: The quiz was used in one or more game sessions and cannot be deleted.
  - `409 Quiz.InUse`: An active game session is currently in progress.

---

### Group B: Question CRUD & Choices

#### 6. `POST /api/quizzes/{quizId}/questions` (Add Question)
- **Requirement ID**: `QUIZ-QUEST-001`
- **Description**: Appends a new question to the end of the quiz with its complete set of choices.
- **Route Parameters**:
  - `quizId` (`Guid`): Target quiz ID.
- **Request Body**:
  ```json
  {
    "text": "What is the capital of France?",
    "mediaId": "01923485-aaaa-7abc-9f5a-222222222222",
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
- **Validation Rules**:
  - **200-Question Limit (`QUIZ-LIMIT-001`)**: Fails with `400 Validation.Failed` if the quiz already contains 200 questions.
  - `text`: Required, trimmed, 1 to 500 characters (`QUIZ-BOUND-004`).
  - `durationSeconds`: Integer between 5 and 300 seconds (`QUIZ-BOUND-007`).
  - `basePoints`: Integer between 0 and 2,147,483,647 (`QUIZ-BOUND-008`).
  - `choices`: Must contain between 2 and 6 choices (`QUIZ-BOUND-005`).
  - Choice `text`: Required, trimmed, 1 to 300 characters (`QUIZ-BOUND-006`).
  - Choice `isCorrect`: At least 1 choice must have `isCorrect = true`.
  - `mediaId` (optional): If provided, must belong to the authenticated Host and be in valid committed state (`QUIZ-SEC-001`). If from another tenant, returns `400 Quiz.InvalidMediaReference`.
- **Business Logic**:
  - Active game check: If an active game exists, returns `409 Quiz.InUse`.
  - Calculates contiguous zero-based `OrderIndex = currentQuestionCount`.
  - Sets contiguous zero-based `OrderIndex` (0..N) for choices based on array position.
  - Resets parent `IsPublished = false` and increments parent `Revision`.
- **Response**: `201 Created`
  - `Location: /api/quizzes/{quizId}/questions/{questionId}`
  ```json
  {
    "id": "01923485-9999-7abc-9f5a-111111111111",
    "quizId": "01923485-9831-7abc-9f5a-3829482910aa",
    "orderIndex": 0,
    "text": "What is the capital of France?",
    "mediaId": "01923485-aaaa-7abc-9f5a-222222222222",
    "durationSeconds": 30,
    "basePoints": 1000,
    "choices": [
      { "id": "01923485-bbbb-7abc-9f5a-333333333333", "orderIndex": 0, "text": "Paris", "isCorrect": true },
      { "id": "01923485-cccc-7abc-9f5a-444444444444", "orderIndex": 1, "text": "London", "isCorrect": false }
    ]
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Max 200 questions reached, text length violation, choice count outside [2, 6], or 0 correct choices.
  - `400 Quiz.InvalidMediaReference`: Media does not exist or belongs to another tenant.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.
  - `409 Quiz.InUse`: An active live game session is running for this quiz.

---

#### 7. `PUT /api/quizzes/{quizId}/questions/{questionId}` (Update Question)
- **Requirement ID**: `QUIZ-QUEST-002`
- **Description**: Updates text, duration, points, choices, or media of an existing question. Completely replaces the choice collection atomically.
- **Route Parameters**:
  - `quizId` (`Guid`): Target quiz ID.
  - `questionId` (`Guid`): Target question ID.
- **Request Body**: Same schema as Add Question.
- **Business Rules**:
  - Active game check: Returns `409 Quiz.InUse` if active game exists.
  - Verifies that `questionId` belongs to `quizId`.
  - Replaces all existing choices for this question with the newly provided set atomically.
  - Resets parent `IsPublished = false` and increments parent `Revision`.
- **Response**: `200 OK` (returns updated question with choices).
- **Error Codes**:
  - `400 Validation.Failed`: Validation error on fields or choices.
  - `400 Quiz.InvalidMediaReference`: Invalid or cross-tenant media attachment.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.
  - `404 Quiz.QuestionNotFound`: Question does not exist within the specified quiz.
  - `409 Quiz.InUse`: Active game session in progress.

---

#### 8. `DELETE /api/quizzes/{quizId}/questions/{questionId}` (Delete Question)
- **Requirement ID**: `QUIZ-QUEST-003`
- **Description**: Removes a question and its choices. Automatically re-indexes the remaining questions to maintain a contiguous zero-based sequence (`0, 1, 2, ...`).
- **Route Parameters**:
  - `quizId` (`Guid`): Target quiz ID.
  - `questionId` (`Guid`): Target question ID.
- **Business Rules**:
  - Active game check: Returns `409 Quiz.InUse` if active game exists.
  - Verifies question exists within this quiz.
  - Deletes question and associated choices.
  - **Re-indexing (`[NORMATIVE]`)**: Remaining questions with `OrderIndex > deletedQuestion.OrderIndex` have their `OrderIndex` decremented by 1 in the same transaction.
  - Resets parent `IsPublished = false` and increments parent `Revision`.
- **Response**: `204 No Content`
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.
  - `404 Quiz.QuestionNotFound`: Question does not exist within the specified quiz.
  - `409 Quiz.InUse`: Active game session in progress.

---

### Group C: Reorder & Publish

#### 9. `POST /api/quizzes/{quizId}/reorder` (Reorder Questions)
- **Requirement ID**: `QUIZ-REORDER-001`
- **Description**: Reorders questions in a single atomic transaction.
- **Route Parameters**:
  - `quizId` (`Guid`): Target quiz ID.
- **Request Body**:
  ```json
  {
    "questionIds": [
      "01923485-9999-7abc-9f5a-333333333333",
      "01923485-9999-7abc-9f5a-111111111111",
      "01923485-9999-7abc-9f5a-222222222222"
    ]
  }
  ```
- **Validation Rules**:
  - **Exact 1-to-1 Permutation**: The array must contain **exact match** of all question IDs currently belonging to the quiz.
    - Zero missing IDs.
    - Zero extra or foreign IDs.
    - Zero duplicate IDs.
    - If any mismatch occurs, fails with `400 Quiz.QuestionSetMismatch` (`QUIZ-ERR-002`).
- **Business Rules**:
  - Active game check: Returns `409 Quiz.InUse` if active game exists.
  - Updates `OrderIndex` of each question to match its new array index (`0, 1, 2, ...`).
  - Resets parent `IsPublished = false` and increments parent `Revision`.
- **Response**: `200 OK`
  ```json
  {
    "message": "Questions reordered successfully.",
    "revision": 4
  }
  ```
- **Error Codes**:
  - `400 Quiz.QuestionSetMismatch`: Provided array does not exactly match existing questions.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.
  - `409 Quiz.InUse`: Active game session in progress.

---

#### 10. `POST /api/quizzes/{quizId}/publish` (Publish Quiz)
- **Requirement ID**: `QUIZ-PUB-001`
- **Description**: Validates all quiz business rules and transitions the quiz to `IsPublished = true`.
- **Route Parameters**:
  - `quizId` (`Guid`): Target quiz ID.
- **Idempotency Rule (`[NORMATIVE]`)**:
  - If the quiz is **already published** (`IsPublished == true`) and valid, retrying publish is an idempotent `200 OK`.
- **Publication Validation Checklist**:
  1. Contains between **1 and 200 questions** (inclusive).
  2. Every question has non-whitespace `text` (1–500 chars).
  3. Every question has `durationSeconds` between **5 and 300 seconds** (inclusive).
  4. Every question has `basePoints` between **0 and 2,147,483,647** (inclusive).
  5. Every question has between **2 and 6 choices** (inclusive).
  6. Every choice has non-whitespace `text` (1–300 chars).
  7. Every question has **at least 1 choice** marked `isCorrect = true`.
  8. Any referenced `mediaId` exists, is stored, and belongs to the **same Host tenant**.
  9. **Score Arithmetic Overflow Protection (`QUIZ-OVERFLOW-001`)**: Total potential maximum score does not overflow signed 64-bit integer (`checked(sum += basePoints)` $\le$ `long.MaxValue`).
- **Outcome**: Sets `IsPublished = true`, increments `Revision`.
- **Response**: `200 OK`
  ```json
  {
    "id": "01923485-9831-7abc-9f5a-3829482910aa",
    "isPublished": true,
    "revision": 5,
    "publishedAt": "2026-09-27T19:30:00Z"
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Fails any item in the publication checklist above. Detail contains specific violation messages.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another Host.
  - `409 Quiz.InUse`: Active game session in progress.
