# Real-Time Protocol (SignalR) — Kahoot-like Platform

> Living specification for the API layer. The Application layer is transport-agnostic:
> every use case is a MediatR command/query returning `Result` / `Result<T>`.
> Controllers and `GameHub` call `ISender.Send(...)`; after a successful durable
> command they attempt to broadcast the outcome through `GameNotifier`
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
  and raises `ParticipantPresenceChanged` to both game groups. A disconnect changes
  presence, not seat reservation or active-question eligibility.

### Authoritative counts and event delivery

- `participantCount` is the number of non-removed participants with an active
  connection. It is an authoritative presence count, not the number of reserved seats.
- `presenceVersion` is a durable, monotonically increasing value stored on the game
  session. Connection attachment, detachment, and participant removal update the
  participant row and increment the game version in the same database transaction.
- Every presence event carries the post-change absolute count and committed version.
  Clients replace their displayed count with the server value; they never infer counts
  by applying an event delta.
- A state-changing command commits before its event fan-out is attempted. Fan-out
  is best-effort and does not change a committed command response to failure.
  Clients treat events as timely notifications, not a durable delivery guarantee:
  hosts recover through `GET /api/games/{id}` and players through `Reconnect`.
- Clients ignore events whose version is not newer than their current version. A
  version gap marks local data stale and triggers an authoritative state refresh.
  Successful reconnect responses also replace both count and version.

Presence payloads are:

```text
ParticipantPresenceResponse {
  participantCount: int,
  presenceVersion: long,
  reason: "Joined" | "Reconnected" | "Disconnected" | "Removed",
  participant: GameParticipantResponse,
}
```

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
| `ParticipantPresenceChanged` | players + host | committed connection attachment, matching disconnect, or participant removal | `ParticipantPresenceResponse` |
| `ParticipantRemoved` | removed connection only | committed `RemoveParticipantCommand` | removed participant `Guid` |
| `QuestionStarted` | players | committed `StartGameCommand` / `StartNextQuestionCommand` | `PlayerQuestionResponse` (**no correct answer(s)**) |
| `QuestionStartedForHost` | host | same | `HostQuestionResponse` (**includes** `correctChoiceIds` and `eligibleParticipantCount`) |
| `QuestionEnded` | players + host | committed `EndQuestionCommand` or automatic close | `QuestionResultsResponse` |
| `LeaderboardUpdated` | players + host | `ShowLeaderboardCommand` | `LeaderboardResponse` |
| `GameEnded` | players + host | `EndGameCommand` | `LeaderboardResponse` (final) |

`POST /api/games/join` reserves a seat and returns its session token but does not
attach a SignalR connection and does not broadcast a presence change. The client
must then call `Reconnect(sessionToken)` to attach and receive authoritative state.
The host receives real-time presence only after that successful attachment; until
then, `GET /api/games/{id}` is the authoritative source for the reservation.

**Players never receive `correctChoiceIds` or per-choice `isCorrect` before the
question is closed** — the players group only ever gets `PlayerQuestionResponse`
for `QuestionStarted`; `HostQuestionResponse` goes solely to the host group.

At question activation, the server snapshots all non-removed reserved participants
as the eligible set and sends the host its `eligibleParticipantCount`. The same
effective denominator is returned as `QuestionResultsResponse.participantCount`.
Disconnects never alter it. Removing a non-removed eligible participant who has
not submitted an accepted answer **must** decrement it exactly once, atomically
with the removal and automatic-close check. A repeated removal is idempotent and
does not decrement it again. An already accepted answer remains counted and its
participant remains in the denominator. Auto-end occurs only after a committed
accepted answer or qualifying removal leaves no eligible unanswered participants,
never merely because a participant disconnected.

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
| `POST /api/games/{id}/start` | host | `StartGameCommand` | commits the question and its eligibility snapshot; then best-effort broadcasts `QuestionStarted(ForHost)` | 200 `QuestionStartedResponse` |
| `POST /api/games/{id}/advance` | host | `StartNextQuestionCommand` | valid from `QuestionResults` or `Leaderboard` (leaderboard optional); idempotent re-entry while `QuestionActive`; `xmin` concurrency-guarded; `409 Game.NoMoreQuestions` past the last question; commits then best-effort broadcasts `QuestionStarted` / `QuestionStartedForHost` | 200 `QuestionStartedResponse` |
| `POST /api/games/{id}/end-question` | host | `EndQuestionCommand` | commits the early close; then best-effort broadcasts `QuestionEnded` | 200 `QuestionResultsResponse` |
| `POST /api/games/{id}/leaderboard` | host | `ShowLeaderboardCommand` | persists ranks; then best-effort broadcasts `LeaderboardUpdated` | 200 `LeaderboardResponse` |
| `POST /api/games/{id}/end` | host | `EndGameCommand` | commits end state; then best-effort broadcasts `GameEnded` | 200 `LeaderboardResponse` |
| `DELETE /api/games/{id}/participants/{participantId}` | host | `RemoveParticipantCommand` | idempotent; the first removal of a non-removed, unanswered eligible participant atomically decrements eligibility exactly once before best-effort `ParticipantRemoved` / possible `QuestionEnded` broadcasts; repeated removal does not decrement again | 204 |
| `POST /api/games/join` | none | `JoinGameRequest` → `JoinGameCommand` | reserves one of at most 500 seats while the game is in the lobby; no SignalR attachment or broadcast | 200 `JoinGameResponse` |

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

`PlayerGameStateResponse` and `HostGameStateResponse` also include the authoritative
`participantCount` and durable `presenceVersion`. These fields are the
resynchronization source for presence displays after reconnects or event gaps.

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
