# Real-Time Protocol (SignalR) — Kahoot-like Platform

> Living specification for the API layer. The Application layer is transport-agnostic:
> every use case is a MediatR command/query returning `Result` / `Result<T>`.
> The API's `GameHub` and controllers call `ISender.Send(...)` and then broadcast the
> result to the SignalR group. This file defines those events so the hub can be built
> without guessing.

---

## Groups & connections

- One SignalR group per game: `game:{gameId}`.
- The **host** connection joins `game:{gameId}` after `POST /api/games` (authenticated).
- A **player** connection joins `game:{gameId}` after `JoinGame` / `ReconnectParticipant`,
  identified by the opaque `sessionToken` (hashed server-side; never the `ConnectionId`).
- On disconnect the hub sets `Participant.ConnectionId = null`; on (re)connect it sets it
  again. Identity always comes from the session token, never the connection id.

---

## Client → Server (hub methods → Application commands)

| Hub method | Auth | Command | Notes |
|---|---|---|---|
| `JoinGame(pin, nickname)` | none | `JoinGameCommand` | returns `JoinGameResponse` incl. one-time `sessionToken` |
| `Reconnect(sessionToken)` | none | `ReconnectParticipantCommand` | returns `PlayerGameStateResponse` |
| `SubmitAnswer(gameId, questionId, selectedChoiceId)` | session token (connection state) | `SubmitAnswerCommand` | idempotent; ack only, no correctness |
| `StartGame(gameId)` | host JWT | `StartGameCommand` | |
| `StartNextQuestion(gameId)` | host JWT | `StartNextQuestionCommand` | double-click safe |
| `EndQuestion(gameId)` | host JWT | `EndQuestionCommand` | early-closes the answer window |
| `ShowLeaderboard(gameId)` | host JWT | `ShowLeaderboardCommand` | computes + persists ranks once |
| `EndGame(gameId)` | host JWT | `EndGameCommand` | |
| `RemoveParticipant(gameId, participantId)` | host JWT | `RemoveParticipantCommand` | |

All host commands also have REST equivalents (see controller map below); the hub methods
exist so the host screen can drive the game over the same socket.

### `SubmitAnswer` payload / validation / idempotency

```
Client -> Server: SubmitAnswer { gameId, questionId, selectedChoiceId }
                  (participant session token taken from the connection, not the payload)
```

- Rejected (`Result` failure, mapped to `answer:rejected`) when: game not `QuestionActive`,
  `questionId` != current, `selectedChoiceId` not in the question, session token invalid,
  participant removed, or `serverTime > questionEndsAt` (**deadline inclusive**).
- Accepted → `answer:accepted` `{ accepted: true, alreadyAnswered: false }`.
- Duplicate (same `gameId+questionId+participant`, enforced by the DB unique index) →
  `answer:accepted` `{ accepted: true, alreadyAnswered: true }`. No second row, no second score.

---

## Server → Client events

Payload types are the Application response records (`Kahoot.Application.Games.Common`).

| Event | Target | Trigger (command) | Payload |
|---|---|---|---|
| `participant:joined` | `game:{id}` | `JoinGameCommand` success | `{ participantId, nickname }` |
| `participant:left` | `game:{id}` | hub disconnect | `{ participantId }` |
| `participant:removed` | `game:{id}` + removed player | `RemoveParticipantCommand` | `{ participantId }` |
| `game:state` | requesting connection | `GetHostGameStateQuery` / `ReconnectParticipantCommand` | `HostGameStateResponse` / `PlayerGameStateResponse` |
| `question:start` | `game:{id}` (players) | `StartGameCommand` / `StartNextQuestionCommand` | `QuestionStartedResponse.Player` (**no correct answer**) |
| `question:start:host` | host connection | same | `QuestionStartedResponse.Host` (**includes** `correctChoiceId`) |
| `question:end` | `game:{id}` | `EndQuestionCommand` | `QuestionResultsResponse` (safe to reveal now) |
| `question:results` | `game:{id}` | `EndQuestionCommand` / `GetQuestionResultsQuery` | `QuestionResultsResponse` |
| `leaderboard:update` | `game:{id}` | `ShowLeaderboardCommand` / `EndGameCommand` | `LeaderboardResponse` |
| `game:end` | `game:{id}` | `EndGameCommand` | `LeaderboardResponse` (final) |
| `answer:accepted` / `answer:rejected` | submitting connection | `SubmitAnswerCommand` | ack / `{ code, description }` |
| `connection:restored` | reconnecting player | `ReconnectParticipantCommand` | `PlayerGameStateResponse` |

**The `question:start` payload broadcast to players MUST be `QuestionStartedResponse.Player`.**
`QuestionStartedResponse.Host` (which carries `correctChoiceId` and per-choice `isCorrect`)
goes only to the host connection.

---

## REST controller map (host + player bootstrap)

| Route | Auth | Request → | Handler |
|---|---|---|---|
| `POST /api/auth/register` | none | `RegisterHostCommand` | (expose only if self-registration is wanted) |
| `POST /api/auth/login` | none | `LoginCommand` | |
| `POST /api/auth/refresh` | none | `RefreshTokenCommand` | rotation + reuse detection |
| `POST /api/auth/logout` | none | `LogoutCommand` | |
| `GET /api/quizzes` | host | `ListQuizzesQuery` | |
| `POST /api/quizzes` | host | `CreateQuizCommand` | |
| `GET /api/quizzes/{id}` | host | `GetQuizQuery` | |
| `PUT /api/quizzes/{id}` | host | `UpdateQuizCommand` | un-publishes |
| `DELETE /api/quizzes/{id}` | host | `DeleteQuizCommand` | blocked if ever played |
| `POST /api/quizzes/{id}/publish` | host | `PublishQuizCommand` | validates the quiz |
| `POST /api/quizzes/{id}/questions` | host | `AddQuestionCommand` | |
| `PUT /api/quizzes/{id}/questions/{qid}` | host | `UpdateQuestionCommand` | |
| `DELETE /api/quizzes/{id}/questions/{qid}` | host | `DeleteQuestionCommand` | |
| `PUT /api/quizzes/{id}/questions/order` | host | `ReorderQuestionsCommand` | |
| `POST /api/uploads/images` | host | `IImageUploadService` | already implemented |
| `POST /api/games` | host | `CreateGameCommand` | → `{ gameId, pin }` |
| `GET /api/games/{id}` | host | `GetHostGameStateQuery` | |
| `GET /api/games/{id}/questions/{qid}/results` | host | `GetQuestionResultsQuery` | |
| `GET /api/games/{id}/leaderboard` | host | `GetLeaderboardQuery` | |
| `POST /api/games/{id}/start` … `/end` | host | game commands | mirror the hub methods |
| `POST /api/games/join` | none | `JoinGameCommand` | for the join page before opening the socket |

---

## Mapping `Result` failures to responses

- `ValidationError` (`Validation.Failed`) → HTTP 400 `ProblemDetails` with `errors` dictionary
  / hub `*:rejected` with the dictionary.
- `Auth.Unauthorized` → 401. `Auth.Forbidden` → 403.
- `*.NotFound` → 404.
- `Game.InvalidStateTransition`, `Game.ConcurrentModification`, `Quiz.InUse`,
  `Quiz.HasSessions`, `Quiz.ConcurrentModification`, `Game.NicknameTaken`,
  `Game.PinUnavailable` → 409.
- Everything else with a failure `Result` → 400 with `{ code, description }`.
- Never leak provider/SQL text; the `IDbExceptionInterpreter` already converts unique
  violations into typed errors inside the handlers.
