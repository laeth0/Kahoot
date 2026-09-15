# Realtime Features

## Overview

SignalR carries live player actions and typed game events through game-specific host
and player groups, with REST retaining responsibility for host controls.

## User Stories

### US-017: Receive isolated live updates

**As a**
game participant

**I want**
to receive only the realtime events intended for my game and role

**So that**
live play remains private and consistent.

### US-018: Retry live actions safely

**As a**
host or player

**I want**
retries and double-clicks to be idempotent

**So that**
network failures cannot corrupt the game.

## Acceptance Criteria

- Given a connected player or host, when a live event is sent, then it is delivered through the appropriate game-specific group rather than fanning out to unrelated connections.
- Given a player question-start event, when the payload is delivered, then it excludes the correct answer.
- Given the same question start, when the host payload is delivered, then the host receives the separate host representation.
- Given a supported retry or host double-click, when an idempotent operation is repeated, then game state and scores remain correct.

## Business Rules

### FR-8: Realtime protocol

- All live game communication uses a typed event contract over SignalR groups.
- Player and host connections use separate groups: `game:{gameId}` and `game:{gameId}:host`.
- Host-only payloads are never delivered to player connections.
- Every event defines its direction, payload, validation, possible errors, and idempotency behaviour in the [realtime protocol](../realtime-protocol.md).
- Submit answer, start game, start question, end question, next question, and end game are idempotent operations.
- Every use case is implemented in `Kahoot.Application` as a MediatR command or query returning `Result` or `Result<T>`.
- Host game-control actions use REST controllers because the hub cannot resolve the authenticated host.
- The hub handles player `JoinGame`, `Reconnect`, and `SubmitAnswer` actions and the host `JoinAsHost` subscription.
- Controllers broadcast successful command outcomes to the appropriate group.
- Players receive `QuestionStartedResponse.Player`; hosts receive `QuestionStartedResponse.Host` separately.
- Presence counts and post-commit delivery semantics are defined centrally in the [realtime protocol](../realtime-protocol.md).
- Clients use absolute presence counts and documented resynchronization paths instead of maintaining local count deltas.

## Edge Cases

- A host double-click or network retry cannot create duplicate state transitions, answers, or scores.
- Realtime events for one game never reach connections belonging only to another game.
