# Functional Requirements

## Purpose

This directory is the living specification for the Kahoot-like platform's
functional behaviour. Requirements are grouped by feature so that a change can be
made in the document that owns the affected workflow without editing one large file.

The original product brief is preserved in
[`Kahoot-like-Platform.md`](../Kahoot-like-Platform.md). Quality attributes and
operational constraints live in
[`non-functional-requirements.md`](../non-functional-requirements.md).

## Organization

Each feature document contains an overview, user stories, acceptance criteria,
business rules, and edge cases. The original `FR-*` identifiers remain stable, and
new sub-identifiers document backend use cases that were not explicit in the former
single-file specification. Each identifier is owned by exactly one document.

| Requirements | Feature document | Scope |
|---|---|---|
| FR-1, FR-1.1, FR-1.2 | [Roles and access](./roles-and-access.md) | Host and player roles and capabilities |
| FR-2, FR-2.1–FR-2.5 | [Authentication](./authentication.md) | Host login, refresh, logout, bootstrap account, and token retention |
| FR-3, FR-3.2–FR-3.5 | [Quiz and question management](./quiz-and-question-management.md) | Quiz queries, authoring, question rules, publishing, ordering, editing, and deletion |
| FR-3.1 | [Media management](./media-management.md) | Question image validation, sanitization, upload, and storage |
| FR-4, FR-4.1–FR-4.3, FR-5a | [Game lifecycle](./game-lifecycle.md) | Session creation, snapshots, state transitions, host state, automatic termination, and isolation |
| FR-5, FR-5.1 | [Joining and lobby](./joining-and-lobby.md) | PIN joins, capacity, handles, versioned presence, and removal |
| FR-6, FR-6.1, FR-6.2 | [Live gameplay](./live-gameplay.md) | Question activation, closure, and answer submission |
| FR-6.3–FR-6.5 | [Scoring and leaderboards](./scoring-and-leaderboards.md) | Answer evaluation, points, results queries, and standings |
| FR-7 | [Reconnection](./reconnection.md) | Session-token identity and state restoration |
| FR-8 | [Realtime features](./realtime-features.md) | SignalR contracts, groups, commands, and delivery semantics |
| FR-9, FR-9.1, FR-9.2, FR-10, FR-10.1 | [Platform operations](./platform-operations.md) | Startup migrations, seeding, service information, and health endpoints |

## Backend Use-Case Coverage

This matrix records where each backend application or API use case is specified. It
is a traceability aid, not a substitute for the detailed requirements.

| Backend use cases | Requirement owner |
|---|---|
| `LoginCommand`, `RefreshTokenCommand`, `LogoutCommand` | [Authentication](./authentication.md) |
| Bootstrap host seeding | [Authentication](./authentication.md) |
| `TokenCleanupHostedService` | [Authentication](./authentication.md) |
| `ListQuizzesQuery`, `GetQuizQuery` | [Quiz and question management](./quiz-and-question-management.md) |
| `CreateQuizCommand`, `UpdateQuizCommand`, `DeleteQuizCommand`, `PublishQuizCommand` | [Quiz and question management](./quiz-and-question-management.md) |
| `AddQuestionCommand`, `UpdateQuestionCommand`, `DeleteQuestionCommand`, `ReorderQuestionsCommand` | [Quiz and question management](./quiz-and-question-management.md) |
| Image upload and stored-media validation | [Media management](./media-management.md) |
| `CreateGameCommand`, `GetHostGameStateQuery` | [Game lifecycle](./game-lifecycle.md) |
| `StartGameCommand`, `StartNextQuestionCommand`, `EndQuestionCommand`, `ShowLeaderboardCommand`, `EndGameCommand` | [Game lifecycle](./game-lifecycle.md) |
| `AutoEndGameCommand` | [Game lifecycle](./game-lifecycle.md) |
| `TryAutoEndQuestionCommand` | [Live gameplay](./live-gameplay.md) |
| `JoinGameCommand`, `RemoveParticipantCommand` | [Joining and lobby](./joining-and-lobby.md) |
| `AttachParticipantConnectionCommand`, `DetachParticipantConnectionCommand` | [Joining and lobby](./joining-and-lobby.md) |
| `AuthorizeHostGameQuery` | [Realtime features](./realtime-features.md) |
| `SubmitAnswerCommand` | [Live gameplay](./live-gameplay.md) |
| `ScoringService`, `LeaderboardService` | [Scoring and leaderboards](./scoring-and-leaderboards.md) |
| `GetQuestionResultsQuery`, `GetLeaderboardQuery` | [Scoring and leaderboards](./scoring-and-leaderboards.md) |
| `ReconnectParticipantCommand` | [Reconnection](./reconnection.md) |
| SignalR `JoinGame`, `Reconnect`, `SubmitAnswer`, and `JoinAsHost` | [Realtime features](./realtime-features.md) |
| `DatabaseMigrationHostedService`, `DatabaseSeederHostedService` | [Platform operations](./platform-operations.md) |
| Root information page and health endpoints | [Platform operations](./platform-operations.md) |

## Maintenance

- Update the owning feature document whenever functional behaviour changes.
- Keep each requirement in one feature document and link to it instead of copying it.
- Preserve existing `FR-*` identifiers; assign a new identifier for new behaviour.
- Update this index when adding, renaming, or removing a feature document.
