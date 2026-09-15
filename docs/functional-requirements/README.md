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
business rules, and edge cases. The original `FR-*` identifiers remain the stable
traceability identifiers. Each identifier is owned by exactly one document in the
table below.

| Requirements | Feature document | Scope |
|---|---|---|
| FR-1, FR-1.1, FR-1.2 | [Roles and access](./roles-and-access.md) | Host and player roles and capabilities |
| FR-2, FR-2.1 | [Authentication](./authentication.md) | Host login, tokens, ownership, and bootstrap account |
| FR-3, FR-3.2 | [Quiz and question management](./quiz-and-question-management.md) | Quiz authoring, question rules, publishing, editing, and deletion |
| FR-3.1 | [Media management](./media-management.md) | Question and choice image upload and storage |
| FR-4, FR-5a | [Game lifecycle](./game-lifecycle.md) | State transitions and game-session isolation |
| FR-5 | [Joining and lobby](./joining-and-lobby.md) | PIN joins, handles, presence, and removal |
| FR-6, FR-6.1, FR-6.2 | [Live gameplay](./live-gameplay.md) | Question activation, closure, and answer submission |
| FR-6.3, FR-6.4 | [Scoring and leaderboards](./scoring-and-leaderboards.md) | Answer evaluation, points, results, and standings |
| FR-7 | [Reconnection](./reconnection.md) | Session-token identity and state restoration |
| FR-8 | [Realtime features](./realtime-features.md) | SignalR contracts, groups, commands, and delivery semantics |
| FR-9, FR-9.1, FR-9.2, FR-10 | [Platform operations](./platform-operations.md) | Startup migrations, seeding, and health endpoint |

## Maintenance

- Update the owning feature document whenever functional behaviour changes.
- Keep each requirement in one feature document and link to it instead of copying it.
- Preserve existing `FR-*` identifiers; assign a new identifier for new behaviour.
- Update this index when adding, renaming, or removing a feature document.
