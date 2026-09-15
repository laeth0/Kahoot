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

## Business Rules

### FR-6.3: Answer validation and scoring

- Answer validation uses exact set equality between the submitted choice IDs and the question's correct choice IDs.
- Scoring is performed by the server and isolated behind `IScoringService`.
- The scoring formula can be replaced without changing controllers or hubs.

### FR-6.4: Results and leaderboard

- The full leaderboard is not broadcast after every answer.
- Standings are computed when a question closes using a throttled or snapshot strategy.
- `QuestionResultsResponse.participantCount` is the effective eligibility denominator for the question, not a live connection count or the current count of non-removed players.
- `answerCount` is the number of accepted answers and is never decremented by a later player removal.

## Edge Cases

- Selecting all correct choices plus any incorrect choice is incorrect.
- Selecting fewer than all correct choices is incorrect.
- A later player removal does not rewrite the closed question's accepted-answer count.
