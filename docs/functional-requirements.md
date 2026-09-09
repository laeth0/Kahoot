# Functional Requirements — Kahoot-like Platform

> Living specification. Keep this file synchronized with every behavioural change.
> The verbatim original brief is preserved in [`Kahoot-like Platform.md`](./Kahoot-like%20Platform.md).
> Non-functional requirements live in [`non-functional-requirements.md`](./non-functional-requirements.md).

---

## FR-1 Roles

The system has exactly two roles.

### FR-1.1 Host

An authenticated account holder who authors quizzes and runs live games.

A host can:

- Log in with a **username and password** (see FR-2).
- Create, edit, and delete quizzes.
- Create questions, add 2–6 answer options, mark exactly one option correct.
- Configure per-question duration and points.
- Start a live game from a quiz and receive a short unique game **PIN**.
- Share a join link / PIN with players.
- See players joining the lobby; remove an inappropriate player.
- Start the quiz, start the next question, end a question.
- See how many players answered.
- Display question statistics and the leaderboard between questions.
- Continue to the next question, end the game, and see final results.

### FR-1.2 Player

A person who plays a game. **Players have no account and never log in.**

A player:

1. Opens the join link the host shared (or the join page and enters the PIN).
2. Enters a **handle name** (nickname).
3. Joins the lobby and waits for the host.
4. Receives questions in real time when the host starts them.
5. Submits one answer per question.
6. Sees whether the answer was accepted, then whether it was correct after the reveal.
7. Sees points earned and leaderboard updates.
8. Continues until the host ends the game, then sees final results.

---

## FR-2 Host Authentication

- **Credential type: username + password only.** Email is not used anywhere in the system.
- Passwords are stored only as a strong one-way hash (see NFR security section).
- Login issues a short-lived access token plus a rotating refresh token; refresh tokens are stored hashed and support rotation with reuse detection.
- All host operations (quiz management, game control) require a valid access token.
- Game-ownership is enforced: a host may only control games started from their own quizzes.
- Players never authenticate and can never invoke host operations.

### FR-2.1 Bootstrap host account (seed data)

- On application startup the system ensures a bootstrap host account exists.
- The bootstrap account's username and password are supplied through configuration / environment variables — never hard-coded.
- If the bootstrap credentials are not configured, seeding is skipped with a warning; startup still succeeds.
- Seeding is idempotent: an existing account with the configured username is left unchanged.
- Seeding is a separate concern from schema migration (see FR-9).

---

## FR-3 Quiz Authoring

- A quiz has a title, an optional description, an `isPublished` flag, and an ordered list of questions.
- A question has text, an optional image, an order, a time limit, base points, and 2–6 choices.
- A choice has text and/or an image (at least one), and an `isCorrect` flag.
- Exactly one choice per question is the correct one.
- A quiz must be **published** before a game can be started from it. Publishing requires the quiz to be valid (see FR-3.2).
- A quiz that has ever been used to run a game cannot be deleted (historical results are preserved).
- A quiz with a game session that is not finished cannot be edited.

### FR-3.1 Images

- The host may attach an image to a question and/or to a choice.
- Images are uploaded through `POST /api/uploads/images` (`multipart/form-data`, field `file`), which validates the file and returns `{ "url": "<relative path>" }`.
- Accepted types are configurable (`FileStorage:AllowedContentTypes`, default JPEG/PNG/WebP/GIF); the server verifies the file's magic bytes, not just the declared content type, and enforces `FileStorage:MaxSizeBytes` (default 5 MB).
- The returned URL is a path relative to the API origin; clients compose the absolute URL using the configured API base URL. The API serves stored files as static content under `FileStorage:PublicBasePath` (default `/uploads`).
- The stored URL is what gets saved on the question/choice when it is created or updated.

### FR-3.2 Publish validation

- A quiz can only be published when every question has valid text, a time limit in range, non-negative points, 2–6 choices, and exactly one correct choice, and every choice has text or an image.

---

## FR-4 Game Lifecycle & State Machine

Game state is modelled explicitly, not with ad-hoc boolean flags.

```
CREATED → LOBBY → QUESTION_ACTIVE → QUESTION_RESULTS → LEADERBOARD → QUESTION_ACTIVE → … → FINISHED
```

Allowed transitions (all others are rejected server-side):

| From | To |
|---|---|
| CREATED | LOBBY |
| LOBBY | QUESTION_ACTIVE, FINISHED |
| QUESTION_ACTIVE | QUESTION_RESULTS, FINISHED |
| QUESTION_RESULTS | LEADERBOARD, FINISHED |
| LEADERBOARD | QUESTION_ACTIVE, FINISHED |
| FINISHED | (terminal) |

- Invalid transitions (e.g. `FINISHED → QUESTION_ACTIVE`) return a meaningful error and do not change state.
- The state machine lives in the application layer (`Kahoot.Application`), not in the domain entities.

---

## FR-5 Joining a Game

- Players join with a game PIN (embedded in the shared link or typed on the join page) plus a handle name.
- The PIN is short, generated server-side, and unique among games that are not finished.
- A handle name must be unique within its game (case-insensitive) and pass validation/sanitization.
- The host sees each join in real time and may remove a player; a removed player cannot rejoin the same game with the same handle.

---

## FR-5a Game Session Isolation

- Each game run is a distinct `GameSession`; the same published quiz can be run any number of times.
- Every run gets its own participants, answers, scores, and leaderboard. All of these rows are scoped by `gameSessionId`, and the one-answer-per-player uniqueness key is `(gameSessionId, questionId, participantId)`.
- Data from earlier sessions is never read or mutated by a new session and never affects its state, scores, or leaderboard.
- Prior sessions and their results remain queryable for history.

---

## FR-6 Live Gameplay

### FR-6.1 Questions

- When the host starts a question the server records `questionStartedAt` and `questionEndsAt` (server clock).
- The correct answer is never sent to player clients before the question is closed/revealed.

### FR-6.2 Answer submission

- A player submits `{ gameId, questionId, participantId, selectedChoiceId }` over the real-time channel.
- The server accepts an answer only if: the game is in `QUESTION_ACTIVE`, `questionId` is the current question, and `serverTime <= questionEndsAt` (**the deadline is inclusive** — an answer arriving exactly at `questionEndsAt` is accepted).
- Exactly one accepted answer is allowed per `(gameId, questionId, participantId)`. Duplicate submissions receive an idempotent "already answered" response and never create a second answer or score.
- Client-supplied timestamps are never trusted for acceptance or scoring.

### FR-6.3 Scoring

- Scoring is server-side, isolated behind a scoring service.
- Score considers correctness and response speed, e.g. `score = basePoints × timeFactor`.
- The formula is replaceable without touching controllers or hubs.

### FR-6.4 Results & leaderboard

- After a question closes the host can show per-question statistics (answer counts per choice) and the leaderboard.
- The full leaderboard is not broadcast after every answer; standings are computed when a question closes (throttled/snapshot strategy).

---

## FR-7 Reconnection

- A player reconnecting after a network drop keeps their identity via a secure session token — **not** the transient real-time connection id.
- Reconnection must not create a second player record.
- On reconnect the server restores: player identity, game, current state, current question, question deadline, whether the player already answered, current score, result state, and leaderboard where appropriate.

---

## FR-8 Real-Time Protocol

- All live game communication uses a typed event contract over SignalR groups (`game:{gameId}`), so events never fan out to unrelated connections.
- Each event documents direction, payload, validation, possible errors, and idempotency behaviour — see [`realtime-protocol.md`](./realtime-protocol.md).
- Idempotent operations: submit answer, start game, start question, end question, next question, end game (host double-clicks and network retries must not corrupt state).
- Every use case is implemented in `Kahoot.Application` as a MediatR command/query returning `Result` / `Result<T>`; the `question:start` payload sent to players excludes the correct answer (`QuestionStartedResponse.Player`), which the host receives separately (`QuestionStartedResponse.Host`).

---

## FR-9 Startup Database Operations

### FR-9.1 Automatic migrations

- Pending EF Core migrations are applied automatically on application startup by a hosted service, not from `Program.cs`.
- Migration runs asynchronously with the startup cancellation token; a migration failure fails application startup (fail fast).

### FR-9.2 Seeding

- Runs as a separate hosted service, after migrations have completed.
- Currently seeds only the bootstrap host account (FR-2.1).
- Development seed data, test data, and production reference data remain separate concerns.

---

## FR-10 Operational Endpoints

- A `/health` endpoint reports liveness without exposing sensitive information.
