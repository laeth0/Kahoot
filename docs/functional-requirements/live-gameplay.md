# Live Gameplay

## Overview

The server controls question timing and eligibility and validates each player's
single realtime answer against the active question.

## User Stories

### US-012: Present a timed question

**As a**
host

**I want**
question timing and closure to be controlled by the server

**So that**
all players are judged against the same deadline.

### US-013: Submit an answer

**As a**
player

**I want**
to submit my selected choices once during the active question

**So that**
my answer can be accepted and scored fairly.

## Acceptance Criteria

- Given an activated question, when the server starts it, then the server records `questionStartedAt` and `questionEndsAt` using the server clock.
- Given the set of non-removed reserved players at activation, when the question runs, then that snapshot is its eligibility denominator.
- Given an active question, when its deadline occurs, the host closes it, or all effective eligible players have an accepted answer, then the question closes.
- Given an answer with `{ gameId, questionId, participantId, selectedChoiceIds }`, when the game is in `QUESTION_ACTIVE`, the question is current, and server time is no later than `questionEndsAt`, then the server accepts the answer.
- Given an answer arriving exactly at `questionEndsAt`, when it is otherwise valid, then it is accepted.

## Business Rules

### FR-6: Live gameplay

- Live questions and answer submission use server-controlled state and time.

### FR-6.1: Questions

- Disconnecting after activation does not reduce the eligibility denominator and cannot by itself close a question early.
- The correct choices are never sent to player clients before the question is closed and revealed.

### FR-6.2: Answer submission

- Answers are submitted through the realtime channel.
- Every answer must select at least one choice.
- Duplicate choice IDs in a submission are deduplicated.
- Every submitted choice ID must belong to the active question.
- Exactly one accepted answer is allowed per `(gameId, questionId, participantId)`.
- Client-supplied timestamps are never trusted for answer acceptance or scoring.

## Edge Cases

- Removing a non-removed eligible player who has not answered decrements the effective eligibility denominator exactly once.
- Player removal, eligibility decrement, and any resulting early-close decision are atomic.
- Repeating the removal is idempotent and does not decrement the denominator again.
- Removing a player who already answered leaves both the denominator and accepted answer unchanged.
- A duplicate answer returns an idempotent "already answered" response and never creates another answer or score.
- An answer is rejected when the game is not in `QUESTION_ACTIVE`, its `questionId` is not current, or it arrives after `questionEndsAt`.
