# Game Lifecycle and Session State Machine Endpoints

This document specifies the REST API endpoints, command contracts, state machine transitions, optimistic concurrency protocol, command idempotency, and background workers for live game sessions based on [docs/06-game-lifecycle.md](../docs/06-game-lifecycle.md).

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                               GAME LIFECYCLE ENDPOINTS                                  │
├───────────────────────────────┬─────────────────────────────────────────────────────────┤
│ POST /api/games               │ Create game session & deep snapshot quiz (Host role)    │
│ GET  /api/games/{id}          │ Get authoritative game session state & version (Host)   │
│ POST /api/games/{id}/start    │ Transition LOBBY -> QUESTION_ACTIVE (Host role)         │
│ POST /api/games/{id}/end-question │ Transition QUESTION_ACTIVE -> QUESTION_RESULTS     │
│ POST /api/games/{id}/show-leaderboard │ Transition QUESTION_RESULTS -> LEADERBOARD      │
│ POST /api/games/{id}/advance  │ Transition QUESTION_RESULTS/LEADERBOARD -> ACTIVE       │
│ POST /api/games/{id}/end      │ Transition Any Unfinished -> FINISHED (Terminal)        │
│ GET  /api/games/{id}/report   │ Query permanent historical game report & rankings       │
├───────────────────────────────┴─────────────────────────────────────────────────────────┤
│                          REAL-TIME & BACKGROUND WORKERS                                 │
├───────────────────────────────┬─────────────────────────────────────────────────────────┤
│ Auto-Close Engine             │ Automatic transition on all-answered / question timer   │
│ Abandonment Finalizer Worker  │ Authoritative sweep of expired Host-disconnect grace    │
└───────────────────────────────┴─────────────────────────────────────────────────────────┘
```

---

## 1. Authoritative Game State Machine

Every game session adheres strictly to this deterministic six-state machine (`GameStatus`). Aliases (e.g. `ACTIVE`, `RESULTS`, `REVEALED`, `DONE`) are strictly forbidden:

```mermaid
stateDiagram-v2
    [*] --> CREATED: Host creates game from published quiz (POST /api/games)
    CREATED --> LOBBY: Transaction commits; 4-8 digit PIN allocated
    LOBBY --> QUESTION_ACTIVE: Host starts game (POST /api/games/{id}/start); Question 1 bound
    QUESTION_ACTIVE --> QUESTION_RESULTS: acceptedAnswerCount == effectiveEligible (non-empty) OR Host EndQuestion
    QUESTION_RESULTS --> LEADERBOARD: Host ShowLeaderboard (POST /api/games/{id}/show-leaderboard)
    LEADERBOARD --> QUESTION_ACTIVE: Host AdvanceQuestion (next question)
    QUESTION_RESULTS --> QUESTION_ACTIVE: Host skips podium; advances question directly
    LEADERBOARD --> FINISHED: Host EndGame OR all questions completed
    QUESTION_RESULTS --> FINISHED: Host EndGame
    LOBBY --> FINISHED: Host terminates lobby OR Abandonment / Host Account Suspension
    QUESTION_ACTIVE --> FINISHED: Host terminates game OR Abandonment / Host Account Suspension
    FINISHED --> [*]: PIN released; terminal immutable archive
```

### State Semantics
* `CREATED`: Ephemeral state within the database transaction prior to lobby exposure.
* `LOBBY`: PIN is active. Players join, choose nicknames, and receive session tokens. **Players may join only in `LOBBY`.**
* `QUESTION_ACTIVE`: Question text, image URL, and choice options broadcast (correctness flags omitted). Countdown timer running. Answer submissions accepted.
* `QUESTION_RESULTS`: Answer submissions closed. Correct choices revealed; aggregate answer distribution materialized.
* `LEADERBOARD`: Cumulative ranked scores, streak bonuses, and podium standings materialized.
* `FINISHED`: Permanent, immutable terminal state. PIN released for reuse. 24-hour read-only recovery window for players. Zero mutations permitted.

---

## 2. Group A: Game Session Creation

### 1. `POST /api/games` (Create Game Session & Deep Snapshot)
- **Requirement IDs**: `GAME-SNAP-001`, `GAME-SNAP-002`, `GAME-PIN-001`, `GAME-BOUND-001`, `GAME-SLO-001`, `GAME-ERR-001`, `GAME-ERR-002`, `GAME-ERR-003`, `GAME-ERR-004`, `GAME-ERR-008`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Content-Type**: `application/json`
- **Request Body**:
  ```json
  {
    "quizId": "01923485-aaaa-7abc-9f5a-111111111111"
  }
  ```
- **Validation & Execution Rules**:
  1. **Quiz Ownership & Published Guard (`GAME-SNAP-002`, `GAME-ERR-003`, `GAME-ERR-004`)**:
     - Quiz must exist and belong to the authenticated Host (`HostAccountId`). If not found, returns `404 Quiz.NotFound`.
     - Quiz must have `IsPublished == true`. If draft/unpublished, returns `409 Game.QuizNotPublished`.
     - Quiz must contain at least 1 question.
  2. **Collision-Resistant PIN Allocation (`GAME-PIN-001`, `GAME-BOUND-001`, `GAME-ERR-008`)**:
     - Allocates a cryptographically random **4 to 8 numeric digit PIN** (e.g. `"048912"`, `"827391"`).
     - PIN is stored as a `string` preserving leading zeroes.
     - PIN must be unique across all active (unfinished) games (`ux_games_active_pin`).
     - Retries up to 5 times on collision. If all 5 attempts collide, fails with `409 Game.PinUnavailable`.
  3. **Deep Immutable Snapshot Generation (`GAME-SNAP-002`)**:
     - Atomically copies quiz title into `games`.
     - Deep copies all quiz questions into `game_question_snapshots`:
       - `OrderIndex`, `Text`, `ImageId`, `ImageUrl`, `DurationSeconds`, `BasePoints`.
       - Initial counts: `InitialEligibleParticipantCount = 0`, `EffectiveEligibleParticipantCount = 0`, `AcceptedAnswerCount = 0`.
     - Deep copies all choices into `game_choice_snapshots`:
       - `GameQuestionId`, `OrderIndex`, `Text`, `IsCorrect`, `SelectionCount = 0`.
  4. **Initial Game Session State**:
     - `Id`: Generated Guid (UUIDv4)
     - `HostAccountId`: Authenticated Host user ID
     - `SourceQuizId`: Requested quiz ID
     - `Title`: Copied from Quiz title
     - `Pin`: Allocated 4-8 digit numeric string
     - `Status`: `GameStatus.Lobby` (`2`)
     - `StateVersion`: `1`
     - `PresenceVersion`: `0`
     - `ReservedParticipantCount`: `0`
     - `NextSeatNumber`: `1`
     - `HostGraceExpiresAt`: `NOW() + 300s` (Initial 5-minute attachment grace window)
     - `CreatedAt`: Current UTC timestamp
     - `FinishedAt`: `null`
- **Response**: `201 Created`
  - **Headers**:
    - `Location: /api/games/01923485-bbbb-7abc-9f5a-222222222222`
  - **Body**:
    ```json
    {
      "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
      "pin": "048912",
      "title": "World History Trivia",
      "status": "LOBBY",
      "stateVersion": 1,
      "totalQuestions": 10,
      "createdAt": "2026-09-28T18:00:00.000Z"
    }
    ```
- **Error Codes**:
  - `400 Validation.Failed`: Malformed payload or missing `quizId`.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Quiz.NotFound`: Quiz does not exist or belongs to another tenant.
  - `409 Game.QuizNotPublished`: Quiz is not published (`IsPublished == false`).
  - `409 Game.PinUnavailable`: Exhausted 5 PIN generation attempts due to collisions.

---

## 3. Group B: Authoritative State Inspection

### 2. `GET /api/games/{id}` (Get Authoritative Game Session State)
- **Requirement IDs**: `GAME-STATE-001`, `GAME-STATE-002`, `GAME-IDEM-001`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Route Parameters**:
  - `id` (`Guid`): Game session identifier.
- **Validation**:
  - Game must exist and belong to the authenticated Host (`HostAccountId`). If not found, returns `404 Game.NotFound`.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "pin": "048912",
    "title": "World History Trivia",
    "status": "QUESTION_ACTIVE",
    "stateVersion": 3,
    "currentQuestionIndex": 1,
    "totalQuestions": 10,
    "participantCount": 24,
    "hostGraceExpiresAt": null,
    "createdAt": "2026-09-28T18:00:00.000Z",
    "finishedAt": null
  }
  ```
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.

---

## 4. Group C: Host Game Controls & State Transitions

All state-modifying control commands require:
1. `commandId` (`Guid`): Client-generated UUIDv4 for end-to-end command idempotency.
2. `expectedStateVersion` (`long`): Concurrency token guaranteeing optimistic locking against lost updates.

```
Common Control Request Contract:
{
  "commandId": "01923485-cccc-7abc-9f5a-333333333333",
  "expectedStateVersion": 1
}
```

### Common Control Validation & Idempotency Pipeline (`GAME-IDEM-001`, `GAME-ERR-001`, `GAME-ERR-007`)
1. **Idempotency Check (`GAME-IDEM-001`)**:
   - Computes SHA-256 hash of inbound canonical request payload (`RequestHash`).
   - Checks `game_command_idempotency` for `(GameId, CommandId)`:
     - If record exists with **matching** `RequestHash`: returns previously committed response payload directly without re-executing state mutation.
     - If record exists with **differing** `RequestHash`: returns `400 Validation.Failed` (parameter mismatch on replay).
2. **Tenant & Existence Check (`GAME-ERR-002`)**:
   - Queries `games` row for `id` and authenticated `HostAccountId`. If missing, returns `404 Game.NotFound`.
3. **Terminal State Immutability Guard (`GAME-ARCH-001`, `GAME-ERR-005`, `GAME-ERR-010`)**:
   - If game `Status == GameStatus.Finished`, any transition attempt returns `409 Game.InvalidStateTransition`.
4. **Optimistic Concurrency Check (`GAME-ERR-007`)**:
   - If `game.StateVersion != request.expectedStateVersion`: returns `409 Game.ConcurrentModification`.
5. **State Transition Validity (`GAME-STATE-001`, `GAME-ERR-005`)**:
   - Verifies current state is an allowed source state for the requested action. If invalid, returns `409 Game.InvalidStateTransition`.
6. **Execution & Atomic Commit**:
   - Executes specific state transition effects.
   - Increments `game.StateVersion += 1`.
   - Stores `(GameId, HostAccountId, CommandId, CommandName, RequestHash, ResultStateVersion, ResponsePayload, CreatedAt)` in `game_command_idempotency`.
   - Commits database transaction.
   - Publishes real-time event to SignalR game hub.

---

### 3. `POST /api/games/{id}/start` (Start Game Session)
- **Requirement IDs**: `GAME-CTRL-001`, `GAME-AUTO-001`, `GAME-BOUND-002`, `GAME-SEC-001`, `GAME-SLO-002`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Valid Source State**: `LOBBY`
- **Target State**: `QUESTION_ACTIVE`
- **Request Body**:
  ```json
  {
    "commandId": "01923485-cccc-7abc-9f5a-333333333333",
    "expectedStateVersion": 1
  }
  ```
- **State Transition Effects**:
  - Closes lobby to new joins (any subsequent join attempt rejects with `409 Game.InvalidStateTransition`).
  - Sets `CurrentQuestionIndex = 1`.
  - Locates Question 1 snapshot:
    - Sets `StartedAt = NOW()`.
    - Computes inclusive deadline: `EndsAt = StartedAt + DurationSeconds`.
    - Counts current active participants: sets `InitialEligibleParticipantCount` and `EffectiveEligibleParticipantCount` to current active seat count.
    - Sets `AcceptedAnswerCount = 0`.
  - Increments `StateVersion`.
  - Broadcasts `QuestionStarted` event to participants:
    - Contains: `questionId`, `orderIndex`, `text`, `imageUrl`, `durationSeconds`, `endsAt`, and choice list (`choiceId`, `orderIndex`, `text`).
    - **Security Rule (`GAME-SEC-001`)**: `isCorrect` flags are strictly stripped from player broadcasts.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "status": "QUESTION_ACTIVE",
    "stateVersion": 2,
    "currentQuestion": {
      "questionId": "01923485-dddd-7abc-9f5a-444444444444",
      "orderIndex": 1,
      "text": "What is the capital of France?",
      "imageUrl": "/uploads/550e8400-e29b-41d4-a716-446655440000.png",
      "durationSeconds": 30,
      "startedAt": "2026-09-28T18:05:00.000Z",
      "endsAt": "2026-09-28T18:05:30.000Z",
      "eligibleParticipants": 15,
      "choices": [
        { "choiceId": "01923485-eeee-7abc-9f5a-555555555551", "orderIndex": 1, "text": "Paris", "isCorrect": true },
        { "choiceId": "01923485-eeee-7abc-9f5a-555555555552", "orderIndex": 2, "text": "London", "isCorrect": false }
      ]
    }
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Malformed request or command ID replay mismatch.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.
  - `409 Game.InvalidStateTransition`: Game is not in `LOBBY` state.
  - `409 Game.ConcurrentModification`: Stale `expectedStateVersion`.

---

### 4. `POST /api/games/{id}/end-question` (End Active Question Early)
- **Requirement IDs**: `GAME-CTRL-002`, `GAME-AUTO-001`, `GAME-SLO-003`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Valid Source State**: `QUESTION_ACTIVE`
- **Target State**: `QUESTION_RESULTS`
- **Request Body**:
  ```json
  {
    "commandId": "01923485-cccc-7abc-9f5a-333333333334",
    "expectedStateVersion": 2
  }
  ```
- **State Transition Effects**:
  - Immediately stops accepting new answer submissions.
  - Clamps question deadline to `NOW()`.
  - Materializes question statistics:
    - Counts and sets `SelectionCount` for each choice snapshot in `game_choice_snapshots`.
    - Sets `ResultsMaterializedAt = NOW()` on the active question snapshot.
  - Increments `StateVersion`.
  - Broadcasts `QuestionResultsRevealed` event: correct choice IDs, answer distribution per choice, and individual player answer correctness.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "status": "QUESTION_RESULTS",
    "stateVersion": 3,
    "questionId": "01923485-dddd-7abc-9f5a-444444444444",
    "orderIndex": 1,
    "totalAnswers": 15,
    "resultsMaterializedAt": "2026-09-28T18:05:22.000Z",
    "choices": [
      { "choiceId": "01923485-eeee-7abc-9f5a-555555555551", "orderIndex": 1, "text": "Paris", "isCorrect": true, "selectionCount": 12 },
      { "choiceId": "01923485-eeee-7abc-9f5a-555555555552", "orderIndex": 2, "text": "London", "isCorrect": false, "selectionCount": 3 }
    ]
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Malformed request or command ID replay mismatch.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.
  - `409 Game.InvalidStateTransition`: Game is not in `QUESTION_ACTIVE` state.
  - `409 Game.ConcurrentModification`: Stale `expectedStateVersion`.

---

### 5. `POST /api/games/{id}/show-leaderboard` (Display Leaderboard)
- **Requirement IDs**: `GAME-CTRL-003`, `GAME-SLO-002`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Valid Source State**: `QUESTION_RESULTS`
- **Target State**: `LEADERBOARD`
- **Request Body**:
  ```json
  {
    "commandId": "01923485-cccc-7abc-9f5a-333333333335",
    "expectedStateVersion": 3
  }
  ```
- **State Transition Effects**:
  - Materializes cumulative scores and ranks for all active participants.
  - Sorts participants by `TotalScore DESC, LastScoredAt ASC`.
  - Increments `StateVersion`.
  - Broadcasts `LeaderboardUpdated` event: top podium players (1st, 2nd, 3rd, 4th, 5th) to Host and private rank updates to individual players.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "status": "LEADERBOARD",
    "stateVersion": 4,
    "topParticipants": [
      { "participantId": "01923485-ffff-7abc-9f5a-666666666661", "nickname": "SpeedyFox", "totalScore": 2840, "rank": 1 },
      { "participantId": "01923485-ffff-7abc-9f5a-666666666662", "nickname": "QuizWizard", "totalScore": 2650, "rank": 2 },
      { "participantId": "01923485-ffff-7abc-9f5a-666666666663", "nickname": "BrainyOwl", "totalScore": 2400, "rank": 3 }
    ]
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Malformed request or command ID replay mismatch.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.
  - `409 Game.InvalidStateTransition`: Game is not in `QUESTION_RESULTS` state.
  - `409 Game.ConcurrentModification`: Stale `expectedStateVersion`.

---

### 6. `POST /api/games/{id}/advance` (Advance to Next Question)
- **Requirement IDs**: `GAME-CTRL-004`, `GAME-BOUND-004`, `GAME-SLO-002`, `GAME-ERR-006`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Valid Source States**: `QUESTION_RESULTS` or `LEADERBOARD` (Host may skip leaderboard directly).
- **Target State**: `QUESTION_ACTIVE`
- **Request Body**:
  ```json
  {
    "commandId": "01923485-cccc-7abc-9f5a-333333333336",
    "expectedStateVersion": 4
  }
  ```
- **State Transition Effects**:
  - Computes `nextQuestionIndex = (game.CurrentQuestionIndex ?? 0) + 1`.
  - Verifies `nextQuestionIndex <= totalQuestions`. If already on final question, rejects with `409 Game.NoMoreQuestions`.
  - Sets `CurrentQuestionIndex = nextQuestionIndex`.
  - Binds next question snapshot:
    - Sets `StartedAt = NOW()`.
    - Computes `EndsAt = StartedAt + DurationSeconds`.
    - Sets `InitialEligibleParticipantCount` and `EffectiveEligibleParticipantCount` to current active seat count.
    - Sets `AcceptedAnswerCount = 0`.
  - Increments `StateVersion`.
  - Broadcasts `QuestionStarted` event for the new question (correct choices stripped).
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "status": "QUESTION_ACTIVE",
    "stateVersion": 5,
    "currentQuestion": {
      "questionId": "01923485-dddd-7abc-9f5a-444444444445",
      "orderIndex": 2,
      "text": "Which planet is known as the Red Planet?",
      "imageUrl": null,
      "durationSeconds": 20,
      "startedAt": "2026-09-28T18:07:00.000Z",
      "endsAt": "2026-09-28T18:07:20.000Z",
      "eligibleParticipants": 15,
      "choices": [
        { "choiceId": "01923485-eeee-7abc-9f5a-555555555553", "orderIndex": 1, "text": "Mars", "isCorrect": true },
        { "choiceId": "01923485-eeee-7abc-9f5a-555555555554", "orderIndex": 2, "text": "Venus", "isCorrect": false }
      ]
    }
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Malformed request or command ID replay mismatch.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.
  - `409 Game.InvalidStateTransition`: Game is neither in `QUESTION_RESULTS` nor `LEADERBOARD`.
  - `409 Game.NoMoreQuestions`: Already on final question of quiz.
  - `409 Game.ConcurrentModification`: Stale `expectedStateVersion`.

---

### 7. `POST /api/games/{id}/end` (End Game Session)
- **Requirement IDs**: `GAME-CTRL-005`, `GAME-ARCH-001`, `GAME-SEC-002`, `GAME-SLO-004`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Valid Source States**: Any unfinished state (`LOBBY`, `QUESTION_ACTIVE`, `QUESTION_RESULTS`, `LEADERBOARD`).
- **Target State**: `FINISHED`
- **Request Body**:
  ```json
  {
    "commandId": "01923485-cccc-7abc-9f5a-333333333337",
    "expectedStateVersion": 5
  }
  ```
- **State Transition Effects**:
  - Authoritatively terminates the game: sets `Status = GameStatus.Finished` (`6`).
  - Sets `FinishedAt = NOW()`.
  - Clears `HostGraceExpiresAt = null`.
  - **PIN Release (`GAME-STATE-002`, `GAME-ARCH-001`)**:
    - Sets `Pin = null` (or unbinds from active PIN unique index), releasing the PIN for future game creation.
  - Materializes final cumulative player ranks and podium.
  - Increments `StateVersion`.
  - Broadcasts terminal `GameEnded` event to all connections.
  - Closes all WebSocket client write channels; permits 24-hour read-only recovery access.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "status": "FINISHED",
    "stateVersion": 6,
    "finishedAt": "2026-09-28T18:15:00.000Z",
    "podium": [
      { "rank": 1, "nickname": "SpeedyFox", "totalScore": 8420 },
      { "rank": 2, "nickname": "QuizWizard", "totalScore": 7950 },
      { "rank": 3, "nickname": "BrainyOwl", "totalScore": 7100 }
    ]
  }
  ```
- **Error Codes**:
  - `400 Validation.Failed`: Malformed request or command ID replay mismatch.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.
  - `409 Game.InvalidStateTransition`: Game is already `FINISHED`.
  - `409 Game.ConcurrentModification`: Stale `expectedStateVersion`.

---

## 5. Group D: Historical Game Reporting

### 8. `GET /api/games/{id}/report` (Historical Game Report)
- **Requirement IDs**: `GAME-ARCH-001`, `GAME-SEC-002`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Route Parameters**:
  - `id` (`Guid`): Game session identifier.
- **Validation**:
  - Game must exist, belong to Host, and be in `FINISHED` state.
- **Response**: `200 OK`
  ```json
  {
    "gameId": "01923485-bbbb-7abc-9f5a-222222222222",
    "title": "World History Trivia",
    "status": "FINISHED",
    "totalQuestions": 10,
    "totalParticipants": 24,
    "createdAt": "2026-09-28T18:00:00.000Z",
    "finishedAt": "2026-09-28T18:15:00.000Z",
    "questions": [
      {
        "orderIndex": 1,
        "text": "What is the capital of France?",
        "totalAnswers": 24,
        "correctAnswers": 20,
        "choices": [
          { "orderIndex": 1, "text": "Paris", "isCorrect": true, "selectionCount": 20 },
          { "orderIndex": 2, "text": "London", "isCorrect": false, "selectionCount": 4 }
        ]
      }
    ],
    "leaderboard": [
      { "rank": 1, "nickname": "SpeedyFox", "totalScore": 8420 },
      { "rank": 2, "nickname": "QuizWizard", "totalScore": 7950 },
      { "rank": 3, "nickname": "BrainyOwl", "totalScore": 7100 }
    ]
  }
  ```
- **Error Codes**:
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role.
  - `404 Game.NotFound`: Game does not exist or belongs to another tenant.
  - `409 Game.InvalidStateTransition`: Game is not yet `FINISHED`.

---

## 6. Group E: Real-Time Protocols & Autonomous Background Workers

### 1. Question Deadline & Autonomous Auto-Close Engine (`GAME-AUTO-001`, `GAME-AUTO-002`, `GAME-RISK-004`)
- **Question Deadline Rule (`GAME-AUTO-001`)**:
  - Answers submitted with `serverTime > EndsAt` are rejected with `409 Game.AnswerTooLate`.
  - **Boundary Invariant**: Expiration of the question deadline **does not by itself advance game state**. The game remains in `QUESTION_ACTIVE` until either:
    1. An automatic closure condition is satisfied; OR
    2. Host explicitly issues `POST /api/games/{id}/end-question`.
- **Equality-Based Auto-Close Rule (`GAME-AUTO-002`)**:
  - Whenever an answer is accepted, or an unanswered participant is removed:
    - If `EffectiveEligibleParticipantCount > 0` AND `AcceptedAnswerCount == EffectiveEligibleParticipantCount`:
      - The server **automatically triggers the transition from `QUESTION_ACTIVE` to `QUESTION_RESULTS`**.
      - Materializes statistics, increments `StateVersion`, and broadcasts `QuestionResultsRevealed`.
  - **Zero-Eligible Boundary Guard (`GAME-RISK-004`)**:
    - If a question starts with zero eligible players (`EffectiveEligibleParticipantCount == 0`), auto-close **does not trigger** on $0 == 0$. The question remains in `QUESTION_ACTIVE` awaiting explicit Host closure.

---

### 2. Host Disconnect Grace & Abandonment Finalizer Worker (`GAME-ABANDON-001`, `GAME-ABANDON-002`, `GAME-RISK-003`)
- **Disconnect Grace Trigger (`GAME-ABANDON-001`)**:
  - SignalR Hub tracks active Host connections per `gameId`.
  - When the **last active Host connection disconnects**, sets `HostGraceExpiresAt = NOW() + 300s` (5 minutes).
  - If any authenticated Host connection reconnects before expiration, clears `HostGraceExpiresAt = null`.
- **Abandonment Finalizer Background Worker (`GAME-ABANDON-002`)**:
  - Periodic background worker runs every 10 seconds.
  - Queries active games (`Status != FINISHED`) where `HostGraceExpiresAt IS NOT NULL AND HostGraceExpiresAt <= NOW()`.
  - Uses `FOR UPDATE SKIP LOCKED` to serialize across replicas.
  - Transitions eligible games to `FINISHED`:
    - Sets `FinishedAt = NOW()`.
    - Sets `Pin = null` (releases PIN).
    - Materializes final cumulative player scores and ranks.
    - Increments `StateVersion`.
    - Broadcasts `GameEnded` with termination reason `HostAbandoned`.
  - **Scheduling Guarantee**: Authoritative finalization occurs within $\le 30\text{ seconds}$ following grace expiry ($T + 30\text{s}$).

---

### 3. Immediate Suspension Termination (`GAME-STATE-001`, `SuspensionFinalizerWorker`)
- When a Host account is suspended by an Administrator:
  - `SuspensionFinalizerWorker` immediately executes:
    - Sets all active games (`Status != FINISHED`) of that Host to `Status = FINISHED`.
    - Sets `IsTerminatedBySuspension = true`, `FinishedAt = NOW()`.
    - Releases active PINs (`Pin = null`).
    - Broadcasts `GameEnded` with reason `HostSuspended`.
    - Evicts all connected sockets via `ISocketEvictionService`.

---

## 7. Group F: Immutable Archive Enforcement (`GAME-ARCH-001`, `GAME-SEC-002`, `GAME-RISK-005`)

Once a game transitions to `Status = FINISHED`:
1. **Zero Participant Removal**:
   - `DELETE /api/games/{id}/participants/{pid}` rejects with `409 Game.InvalidStateTransition` (or `409 Game.ArchiveImmutable`).
2. **Zero Answer Submissions**:
   - `POST /api/games/{id}/questions/{qid}/answers` rejects with `409 Game.InvalidStateTransition`.
3. **Zero State Modifications**:
   - All Host control endpoints (`/start`, `/end-question`, `/show-leaderboard`, `/advance`, `/end`) reject with `409 Game.InvalidStateTransition`.
4. **Read-Only Preservation**:
   - Historical snapshots and participant scores are permanently preserved.
   - Referenced question images are protected from background cleanup workers as long as `game_question_snapshots` reference their `ImageId`.

---

## 8. Non-Functional Latency & Capacity Targets

| Metric | Target | Stable Req ID |
| :--- | :--- | :--- |
| **Simultaneous Live Game Sessions** | $\ge 200$ active games concurrently | `GAME-SLO-001` |
| **Simultaneous Connected Players** | $\ge 20,000$ active players | `GAME-SLO-001` |
| **Game Session Creation Latency (`POST /api/games`)** | $p95 \le 200\text{ ms}$ | `GAME-SLO-001` |
| **Start Game / Advance Question Latency** | $p95 \le 100\text{ ms}$ | `GAME-SLO-002` |
| **End Question Latency (Clamping Submissions)** | $p95 \le 80\text{ ms}$ | `GAME-SLO-003` |
| **End Game / Finalize Ranks Latency** | $p95 \le 250\text{ ms}$ | `GAME-SLO-004` |
| **PIN Keyspace & Collision Resistance** | $100,000,000$ combinations, collision $< 0.0002\%$ | `GAME-PIN-001` |
| **Abandonment Finalization Schedule Delay** | $\le 30\text{ seconds}$ after grace expiry | `GAME-ABANDON-002` |

---

## 9. Canonical Error Codes Reference

| HTTP Status | Error Code | Description / Trigger Scenario | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **400** | `Validation.Failed` | Malformed JSON, missing fields, or command replay payload mismatch. | `GAME-ERR-001` |
| **401** | `Auth.Unauthorized` | Missing, expired, or invalid JWT access token. | `GAME-ERR-001` |
| **403** | `Auth.Forbidden` | Caller does not possess `Host` role. | `GAME-ERR-001` |
| **404** | `Game.NotFound` | Target `gameId` does not exist or belongs to another Host tenant. | `GAME-ERR-002` |
| **404** | `Quiz.NotFound` | Target `quizId` does not exist or belongs to another Host tenant. | `GAME-ERR-003` |
| **409** | `Game.QuizNotPublished` | Attempted to launch a game from an unpublished draft quiz. | `GAME-ERR-004` |
| **409** | `Game.InvalidStateTransition` | Transition not permitted from current game state (e.g. advance from LOBBY). | `GAME-ERR-005` |
| **409** | `Game.NoMoreQuestions` | Host issued `Advance` while already on the final question of the quiz. | `GAME-ERR-006` |
| **409** | `Game.ConcurrentModification` | Client submitted stale `expectedStateVersion`. | `GAME-ERR-007` |
| **409** | `Game.PinUnavailable` | Exhausted 5 random PIN generation retries due to active collisions. | `GAME-ERR-008` |
| **409** | `Game.AnswerTooLate` | Answer submitted after question `EndsAt` deadline expired. | `GAME-ERR-009` |
| **409** | `Game.ArchiveImmutable` | Attempted to mutate or remove records from a game in `FINISHED` state. | `GAME-ERR-010` |
| **409** | `Game.Full` | Lobby join rejected when 500 participant seat capacity reached. | `GAME-BOUND-003` |
