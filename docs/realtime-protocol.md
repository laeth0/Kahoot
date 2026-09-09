# Real-Time Protocol (SignalR) — Kahoot-like Platform

> Living specification for the API layer. The Application layer is transport-agnostic:
> every use case is a MediatR command/query returning `Result` / `Result<T>`.
> Controllers and `GameHub` call `ISender.Send(...)`; on success the controller
> broadcasts the outcome to the SignalR group through `GameNotifier`
> (`IHubContext<GameHub, IGameClient>`). This file defines the transport contract.

---

## Groups & connections

- Hub endpoint: `/hubs/game`.
- Two groups per game:
  - `game:{gameId}` — player connections.
  - `game:{gameId}:host` — host connection(s).
- A **player** connection joins `game:{gameId}` after a successful `JoinGame` or
  `Reconnect` hub call. Identity is the opaque `sessionToken` (hashed server-side;
  never the `ConnectionId`); the raw token is held in per-connection state so
  `SubmitAnswer` does not resend it.
- A **host** connection joins `game:{gameId}:host` after a successful `JoinAsHost`
  hub call (`Authorize`d with the host JWT; game ownership is verified).
- On (re)connect the hub sets `Participant.ConnectionId`; on disconnect it clears it
  and raises `ParticipantLeft` to the host group.

### Why host game-control is REST-only

`IHttpContextAccessor.HttpContext` is `null` during hub method invocations, so
`ICurrentUser` (which the host-authenticated MediatR handlers depend on) cannot
resolve the caller inside the hub. Host game-control therefore runs through the
REST controllers, where `HttpContext` is present. The hub is player-facing plus a
host *subscription* channel (`JoinAsHost`), and controllers push events to the
groups after each command succeeds.

---

## Client → Server (hub methods)

All hub methods return `RealtimeResponse<T>` = `{ success, data, error }` where
`error` is `{ code, description }` (never thrown; `JoinAsHost` additionally requires
a valid JWT, so an unauthenticated call is rejected by the pipeline).

| Hub method | Auth | Command | Returns |
|---|---|---|---|
| `JoinGame(pin, nickname)` | none | `JoinGameCommand` | `RealtimeResponse<JoinGameResponse>` (incl. one-time `sessionToken`) |
| `Reconnect(sessionToken)` | none | `ReconnectParticipantCommand` | `RealtimeResponse<PlayerGameStateResponse>` |
| `SubmitAnswer(questionId, selectedChoiceId)` | session token (connection state) | `SubmitAnswerCommand` | `RealtimeResponse<AnswerAckResponse>` |
| `JoinAsHost(gameId)` | host JWT (query-string `access_token`) | ownership check | `RealtimeResponse<bool>` |

### `SubmitAnswer` validation / idempotency

- The `gameId` and the player `sessionToken` are taken from per-connection state
  set by `JoinGame` / `Reconnect`; only `questionId` + `selectedChoiceId` are sent.
- Per-connection spam guard: at most 5 `SubmitAnswer` calls per rolling 3 s;
  excess calls fail fast with `Game.TooManyAnswerAttempts` without touching the DB.
- Failure `error.code` (mapped from `GameErrors`): game not `QuestionActive`,
  `questionId` not current, `selectedChoiceId` not in the question, session token
  invalid, participant removed, or `serverTime > questionEndsAt`
  (**deadline inclusive**).
- Success → `{ success: true, data: { accepted: true, alreadyAnswered: false } }`.
- Duplicate (same `game + question + participant`, enforced by the DB unique index
  `uq_answer_participant_question`) → `{ accepted: true, alreadyAnswered: true }`.
  No second row, no second score. The accepted-answer INSERT and the participant
  score `+=` run in one short transaction only when points > 0; a 0-point answer is
  a single bare INSERT.
- The hub holds no DB dependency: it delegates to
  `AttachParticipantConnectionCommand` / `DetachParticipantConnectionCommand`
  (connection bookkeeping) and `AuthorizeHostGameQuery` (host ownership), plus the
  player MediatR commands.

---

## Server → Client events (`IGameClient`)

Strongly-typed client; method names below are the event names. Payload types are
the Application response records (`Kahoot.Application.Games.Common`).

| Method | Target group(s) | Trigger | Payload |
|---|---|---|---|
| `ParticipantJoined` | players + host | `JoinGame` hub / `POST /api/games/join` | `GameParticipantResponse` |
| `ParticipantLeft` | host | hub disconnect | `Guid participantId` |
| `ParticipantRemoved` | players + host | `RemoveParticipantCommand` | `Guid participantId` |
| `QuestionStarted` | players | `StartGameCommand` / `StartNextQuestionCommand` | `PlayerQuestionResponse` (**no correct answer**) |
| `QuestionStartedForHost` | host | same | `HostQuestionResponse` (**includes** `correctChoiceId`) |
| `QuestionEnded` | players + host | `EndQuestionCommand` | `QuestionResultsResponse` |
| `LeaderboardUpdated` | players + host | `ShowLeaderboardCommand` | `LeaderboardResponse` |
| `GameEnded` | players + host | `EndGameCommand` | `LeaderboardResponse` (final) |

**Players never receive `correctChoiceId` or per-choice `isCorrect` before the
question is closed** — the players group only ever gets `PlayerQuestionResponse`
for `QuestionStarted`; `HostQuestionResponse` goes solely to the host group.

The full leaderboard is not broadcast per answer; it is computed and persisted once
per question close (`ShowLeaderboardCommand` / `EndGameCommand`).

---

## REST controller map

Auth: `host` = valid host JWT (`Authorize`); `none` = anonymous.

| Route | Auth | Request → | Handler | Success |
|---|---|---|---|---|
| `POST /api/auth/login` | none | `LoginRequest` → `LoginCommand` | | 200 `AuthenticationResponse` |
| `POST /api/auth/refresh` | none | `RefreshRequest` → `RefreshTokenCommand` | rotation + reuse detection | 200 `AuthenticationResponse` |
| `POST /api/auth/logout` | none | `LogoutRequest` → `LogoutCommand` | idempotent | 204 |
| `GET /api/quizzes` | host | `ListQuizzesQuery` | | 200 `QuizSummaryResponse[]` |
| `POST /api/quizzes` | host | `CreateQuizRequest` → `CreateQuizCommand` | | 201 `{ id }` |
| `GET /api/quizzes/{id}` | host | `GetQuizQuery` | | 200 `QuizDetailResponse` |
| `PUT /api/quizzes/{id}` | host | `UpdateQuizRequest` → `UpdateQuizCommand` | un-publishes | 204 |
| `DELETE /api/quizzes/{id}` | host | `DeleteQuizCommand` | blocked once ever played | 204 |
| `POST /api/quizzes/{id}/publish` | host | `PublishQuizCommand` | validates the quiz | 204 |
| `POST /api/quizzes/{id}/questions` | host | `SaveQuestionRequest` → `AddQuestionCommand` | | 201 `{ id }` |
| `PUT /api/quizzes/{id}/questions/{questionId}` | host | `SaveQuestionRequest` → `UpdateQuestionCommand` | | 204 |
| `DELETE /api/quizzes/{id}/questions/{questionId}` | host | `DeleteQuestionCommand` | | 204 |
| `PUT /api/quizzes/{id}/questions/order` | host | `ReorderQuestionsRequest` → `ReorderQuestionsCommand` | | 204 |
| `POST /api/uploads/images` | host | `multipart/form-data` field `file` → `IImageUploadService` | magic-byte + size check | 201 `{ url }` |
| `POST /api/games` | host | `CreateGameRequest` → `CreateGameCommand` | | 201 `CreateGameResponse` |
| `GET /api/games/{id}` | host | `GetHostGameStateQuery` | | 200 `HostGameStateResponse` |
| `GET /api/games/{id}/questions/{questionId}/results` | host | `GetQuestionResultsQuery` | | 200 `QuestionResultsResponse` |
| `GET /api/games/{id}/leaderboard` | host | `GetLeaderboardQuery` | | 200 `LeaderboardResponse` |
| `POST /api/games/{id}/start` | host | `StartGameCommand` | broadcasts `QuestionStarted(ForHost)` | 200 `QuestionStartedResponse` |
| `POST /api/games/{id}/advance` | host | `StartNextQuestionCommand` | double-click safe; broadcasts `QuestionStarted(ForHost)` | 200 `QuestionStartedResponse` |
| `POST /api/games/{id}/end-question` | host | `EndQuestionCommand` | early-closes window; broadcasts `QuestionEnded` | 200 `QuestionResultsResponse` |
| `POST /api/games/{id}/leaderboard` | host | `ShowLeaderboardCommand` | persists ranks; broadcasts `LeaderboardUpdated` | 200 `LeaderboardResponse` |
| `POST /api/games/{id}/end` | host | `EndGameCommand` | broadcasts `GameEnded` | 200 `LeaderboardResponse` |
| `DELETE /api/games/{id}/participants/{participantId}` | host | `RemoveParticipantCommand` | idempotent; broadcasts `ParticipantRemoved` | 204 |
| `POST /api/games/join` | none | `JoinGameRequest` → `JoinGameCommand` | join page before opening the socket; broadcasts `ParticipantJoined` | 200 `JoinGameResponse` |

There is **no registration endpoint** — the only host account is the configuration
seed (`Seeding:Host`). Players never authenticate.

### Rate limiting (`Microsoft.AspNetCore.RateLimiting`, keyed by client IP)

- `POST /api/auth/*` — fixed window, 10 requests / 5 min (anti credential-stuffing).
- `POST /api/games/join` — token bucket, 60 burst + 30 / 10 s sustained (tolerates a
  NAT'd classroom of distinct players; throttles scripted floods).
- All other endpoints — global token bucket, 240 burst + 120 / 30 s per IP.
- `/health` is exempt. Rejections are `429` `ProblemDetails`.
- `X-Forwarded-*` is honoured (`UseForwardedHeaders`) so the partition key is the
  real client IP behind Railway's proxy.

---

## Reconnection payload

`ReconnectParticipantCommand` returns `PlayerGameStateResponse`, which now also
carries, for the post-question phases:

- `lastQuestionResults` (`QuestionResultsResponse`) when status is `QuestionResults`
  or `Leaderboard` — safe to send, the answer is already revealed.
- `leaderboard` (`LeaderboardResponse`) when status is `QuestionResults`,
  `Leaderboard` or `Finished`.

`currentQuestion` (`PlayerQuestionResponse`, no correct answer) is still only set
while a question is active.

---

## Mapping `Result` failures to HTTP (`ApiControllerBase`)

- `ValidationError` (`Validation.Failed`) → **400** `ValidationProblemDetails`
  (`errors` dictionary keyed by field).
- `Auth.Forbidden` → **403**.
- Any other `Auth.*` code (`Auth.Unauthorized`, `Auth.InvalidCredentials`,
  `Auth.InvalidRefreshToken`, `Auth.RefreshTokenReuse`) → **401**.
- Code ending `.NotFound`, or `Game.InvalidPin` → **404**.
- Conflict set → **409**: `Game.InvalidStateTransition`, `Game.ConcurrentModification`,
  `Game.NicknameTaken`, `Game.PinUnavailable`, `Game.NoMoreQuestions`,
  `Game.NotJoinable`, `Game.QuizNotPublished`, `Quiz.InUse`, `Quiz.HasSessions`,
  `Quiz.ConcurrentModification`.
- Any other failure `Result` → **400** `ProblemDetails` with `code` extension.

Non-validation problem bodies are `application/problem+json`:
`{ "title": <description>, "status": <code>, "code": <error code> }`. Provider/SQL
text never leaks — `IDbExceptionInterpreter` converts unique-constraint violations
into typed errors inside the handlers, and unhandled exceptions are caught by
`GlobalExceptionHandler` (500 `ProblemDetails`, details logged only).
