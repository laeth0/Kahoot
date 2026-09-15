# Scoring and Leaderboards

## Overview

The server evaluates answers by exact choice-set matching, awards speed-scaled
points, and produces question results and leaderboard snapshots after closure.

## User Stories

### US-014: Receive a fair score

**As a**
player

**I want**
my answer to be validated and scored by the server

**So that**
all players are evaluated consistently.

### US-015: Review results and standings

**As a**
host

**I want**
to show question statistics and leaderboard standings after a question

**So that**
players can understand the outcome and their progress.

## Acceptance Criteria

- Given a submitted selection that exactly equals the set of correct choice IDs, when the server evaluates it, then the answer is correct.
- Given a submitted selection that omits a correct choice or contains an incorrect choice, when the server evaluates it, then the answer is incorrect.
- Given a correct answer, when the server scores it, then the awarded score is `basePoints × timeFactor`.
- Given an incorrect answer, when the server scores it, then it awards 0 points.
- Given a closed question, when the host requests results or the leaderboard, then the host can display per-choice answer counts and current standings.
- Given an owned game and any snapshotted question in it, when the host requests that question's results, then the response reveals correct choice IDs, per-choice selection counts, participant count, answer count, and question index.
- Given an owned game, when the host retrieves its leaderboard, then non-removed players are returned in ranked order without causing a state transition.

## Business Rules

### FR-6.3: Answer validation and scoring

- Answer validation uses exact set equality between the submitted choice IDs and the question's correct choice IDs.
- Scoring is performed by the server and isolated behind `IScoringService`.
- Correct-answer points decrease linearly from the full base points to half the base points over the question time limit, rounded away from zero and clamped to that range.
- Response time is measured from the server-recorded question start and clamped from zero through the configured time limit.
- Incorrect answers and questions with non-positive base points award zero.
- The scoring strategy can be replaced without changing controllers or hubs.

### FR-6.4: Results and leaderboard

- The full leaderboard is not computed or broadcast after every answer.
- Standings and persisted ranks are computed when the host shows the leaderboard or ends the game.
- `QuestionResultsResponse.participantCount` is the current number of non-removed participant records in the game when results are built, regardless of connection state.
- `answerCount` is the number of accepted answers and is never decremented by a later player removal.
- Removed participants are excluded from leaderboards.
- Players are ordered by descending total score and then by nickname without regard to case; sequential ranks begin at 1.
- Showing or finalizing a leaderboard persists each included player's latest rank.

### FR-6.5: Results and leaderboard queries

- Only the owning host can retrieve question results or a leaderboard.
- Question-results retrieval accepts any question snapshot belonging to the game, not only the current question.
- A result includes each choice's text, answer count, and correctness after reveal.
- Leaderboard retrieval is available independently of the show-leaderboard transition and returns the latest stored ranks, with positional ranks as a fallback.

## Edge Cases

- Selecting all correct choices plus any incorrect choice is incorrect.
- Selecting fewer than all correct choices is incorrect.
- A later player removal does not rewrite the closed question's accepted-answer count.
- A question ID that is not a snapshot in the owned game is rejected with `Game.NotCurrentQuestion`.
- A missing or unowned game is reported as not found without disclosing another host's game.
