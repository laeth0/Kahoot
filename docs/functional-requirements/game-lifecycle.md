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

## Acceptance Criteria

- Given a newly created game, when its lifecycle progresses, then it follows `CREATED → LOBBY → QUESTION_ACTIVE → QUESTION_RESULTS → [LEADERBOARD] → QUESTION_ACTIVE → … → FINISHED`.
- Given question results, when the host advances without showing the leaderboard, then the next question becomes active.
- Given question results, when the host shows the leaderboard and then advances, then the next question becomes active.
- Given a repeated advance request while a question is already active, when the request is processed, then the current question is rebroadcast without starting another question.
- Given multiple runs of the same quiz, when data is read or written for one run, then no participant, answer, score, leaderboard, or state from another run is read or changed.

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

### FR-5a: Game session isolation

- Every game run is a distinct `GameSession`; a published quiz may be run any number of times.
- Each run owns its participants, answers, scores, and leaderboard, all scoped by `gameSessionId`.
- The one-answer-per-player uniqueness key is `(gameSessionId, questionId, participantId)`.
- Data from previous sessions never affects the state, scores, or leaderboard of a new session.
- Previous sessions and their results remain queryable as history.

## Edge Cases

- Advancing past the final question is rejected with `Game.NoMoreQuestions` without changing state; the host ends the game from `QUESTION_RESULTS` or `LEADERBOARD`.
- The losing request in a concurrent advance receives `Game.ConcurrentModification`.
- Any transition not listed in the state table, including `FINISHED → QUESTION_ACTIVE`, is rejected with a meaningful error and leaves the state unchanged.
- `FINISHED` is terminal.
