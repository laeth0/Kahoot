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
- Given an active question, when the host closes it or all eligible players have an accepted answer, then the state moves to `QUESTION_RESULTS` and revealed results are broadcast.
- Given an attached player, when the player calls `SubmitAnswer(questionId, selectedChoices)` while the game is in `QUESTION_ACTIVE`, the question is current, and server time is no later than `questionEndsAt`, then the server uses the connection's session token to accept the answer.
- Given an answer arriving exactly at `questionEndsAt`, when it is otherwise valid, then it is accepted.
- Given an active question with at least one eligible player, when its accepted-answer count reaches the fixed eligibility count, then it closes automatically exactly once.

## Business Rules

### FR-6: Live gameplay

- Live questions and answer submission use server-controlled state and time.

### FR-6.1: Questions

- Question activation selects the next snapshotted question by ordered position, records zero-based and total question counts, sets the server-controlled start and end times, snapshots all non-removed reserved players as eligible, and resets the accepted-answer count to zero.
- Disconnecting or removal after activation does not reduce the fixed eligibility count and cannot by itself close a question early.
- The deadline closes answer acceptance but does not independently transition backend state; the host end-question action remains the deadline fallback.
- A question with no eligible players does not auto-close and remains available for explicit host closure.
- The correct choices are never sent to player clients before the question is closed and revealed.

### FR-6.2: Answer submission

- Answers are submitted through the realtime channel after a successful join or reconnect has associated the connection with a game and session token.
- The realtime method accepts one choice ID or an array of choice IDs and normalizes valid string or JSON GUID representations before dispatch.
- Every answer must select at least one choice.
- Duplicate choice IDs in a submission are deduplicated.
- Every submitted choice ID must belong to the active question.
- A submission may contain at most six non-empty choice IDs.
- Exactly one accepted answer is allowed per `(gameId, questionId, participantId)`.
- Client-supplied timestamps are never trusted for answer acceptance or scoring.
- Persisting the answer, updating a positive score, and incrementing the accepted-answer count occur in one transaction.
- Each connection may make at most five answer attempts in a rolling three-second submission window.

## Edge Cases

- Disconnecting or removing a player after activation leaves the eligibility count unchanged; a removed player's accepted answer remains stored.
- A duplicate answer returns an idempotent "already answered" response and never creates another answer or score.
- An answer is rejected when the game is not in `QUESTION_ACTIVE`, its `questionId` is not current, or it arrives after `questionEndsAt`.
- An answer is rejected for a missing or invalid session token, a removed player, no selected valid choice, or any choice outside the current question.
- Exceeding the per-connection attempt limit returns `Game.TooManyAnswerAttempts` without executing the answer command.
