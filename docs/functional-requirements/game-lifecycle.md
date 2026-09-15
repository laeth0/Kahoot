# Game Lifecycle

## Overview

Every game run follows an explicit state machine and owns an isolated session of
participants, answers, scores, and results.

## User Stories

### US-008: Control a live game

**As a**
host

**I want**
game actions to follow predictable state transitions

**So that**
retries and invalid actions cannot corrupt the session.

### US-009: Run a quiz more than once

**As a**
host

**I want**
each run of a quiz to have independent data

**So that**
earlier sessions never affect a new game.

### US-022: Create a durable game session

**As a**
host

**I want**
the published quiz to be snapshotted when I create a game

**So that**
the live session remains stable even if the source quiz changes later.

### US-023: Recover and safely terminate a hosted game

**As a**
host

**I want**
to retrieve authoritative game state and have abandoned games close automatically

**So that**
the session can recover from reloads and does not remain open indefinitely.

## Acceptance Criteria

- Given a newly created game, when its lifecycle progresses, then it follows `CREATED → LOBBY → QUESTION_ACTIVE → QUESTION_RESULTS → [LEADERBOARD] → QUESTION_ACTIVE → … → FINISHED`.
- Given question results, when the host advances without showing the leaderboard, then the next question becomes active.
- Given question results, when the host shows the leaderboard and then advances, then the next question becomes active.
- Given a repeated advance request while a question is already active, when the request is processed, then the current question is rebroadcast without starting another question.
- Given multiple runs of the same quiz, when data is read or written for one run, then no participant, answer, score, leaderboard, or state from another run is read or changed.
- Given an owned published quiz, when the host creates a game, then a distinct lobby session is returned with a unique active PIN, a join URL, and an immutable copy of the quiz title, questions, choices, order, timing, points, and correctness data.
- Given an owned game, when the host retrieves its state, then the response includes its PIN, snapshotted quiz title, status, current question timing and index, total questions, answered count, connected-player count, presence version, participant details, and join URL.
- Given a hosted unfinished game, when its last host connection is lost and none reconnects within the configured grace period, then the game finishes and its final leaderboard is broadcast.

## Business Rules

### FR-4: State machine

Game state is explicit and is not represented by ad-hoc boolean flags.

| From | To |
|---|---|
| `CREATED` | `LOBBY` |
| `LOBBY` | `QUESTION_ACTIVE`, `FINISHED` |
| `QUESTION_ACTIVE` | `QUESTION_RESULTS`, `FINISHED` |
| `QUESTION_RESULTS` | `QUESTION_ACTIVE`, `LEADERBOARD`, `FINISHED` |
| `LEADERBOARD` | `QUESTION_ACTIVE`, `FINISHED` |
| `FINISHED` | Terminal state |

- The leaderboard step is optional.
- Concurrent advance calls are serialized by an optimistic concurrency token.
- The state machine belongs to `Kahoot.Application`, not to domain entities.

### FR-4.1: Game creation and quiz snapshots

- Only the authenticated owner can create a game from a quiz.
- A game can be created only from a published quiz.
- Every creation produces a new `GameSession` in `LOBBY`; source quiz data is copied into game-specific question and choice snapshots in display order.
- The game stores the quiz title and uses the snapshots for live play and results.
- The PIN is unique among unfinished games, and creation returns both the PIN and generated join URL.

### FR-4.2: Host state and controls

- Only the owning host can retrieve or control a game.
- Starting the game activates the first snapshotted question from `LOBBY`.
- Advancing activates the next snapshot from `QUESTION_RESULTS` or `LEADERBOARD`.
- Ending a question moves `QUESTION_ACTIVE` to `QUESTION_RESULTS`, closes the answer deadline immediately when necessary, and returns revealed results.
- Showing the leaderboard moves `QUESTION_RESULTS` to `LEADERBOARD` and persists participant ranks.
- Ending the game is valid from any non-finished gameplay state, records the finish time, persists final ranks, and returns the final leaderboard.
- Start, advance, end-question, show-leaderboard, and end-game re-entry paths return the existing result for their current target state without applying the transition twice.

### FR-4.3: Host presence and automatic game termination

- Host hub subscriptions require authentication and ownership of the game.
- Multiple host connections may subscribe to the same owned game.
- The disconnect grace period begins only after the final tracked host connection disconnects and is cancelled when a host reconnects.
- After the configurable grace period, an unfinished game is automatically marked `FINISHED`, its finish time and final ranks are persisted, and `GameEnded` is broadcast.
- Automatic completion is idempotent when the game is missing, already finished, or concurrently completed elsewhere.

### FR-5a: Game session isolation

- Every game run is a distinct `GameSession`; a published quiz may be run any number of times.
- Each run owns its participants, answers, scores, and leaderboard, all scoped by `gameSessionId`.
- The one-answer-per-player uniqueness key is `(gameSessionId, questionId, participantId)`.
- Data from previous sessions never affects the state, scores, or leaderboard of a new session.
- Previous sessions and their results remain queryable as history.

## Edge Cases

- Advancing past the final question is rejected with `Game.NoMoreQuestions` without changing state; the host ends the game from `QUESTION_RESULTS` or `LEADERBOARD`.
- Starting a game with no snapshotted questions is rejected with `Game.NoMoreQuestions`.
- Creating a game from a draft quiz is rejected with `Game.QuizNotPublished`.
- Failure to allocate a unique active PIN after the bounded retry sequence returns `Game.PinUnavailable`.
- The losing request in a concurrent advance receives `Game.ConcurrentModification`.
- Any transition not listed in the state table, including `FINISHED → QUESTION_ACTIVE`, is rejected with a meaningful error and leaves the state unchanged.
- `FINISHED` is terminal.
