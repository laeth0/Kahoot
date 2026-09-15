# Reconnection

## Overview

Players recover their identity and authoritative game state after a network drop by
using a secure session token rather than a transient connection identifier.

## User Stories

### US-016: Rejoin after a network interruption

**As a**
player

**I want**
to reconnect to my existing game identity

**So that**
I can continue without losing my answer, score, or place.

## Acceptance Criteria

- Given a valid player session token, when the realtime connection is restored, then the existing player record is reused.
- Given a reconnecting player, when state is restored, then the response includes the player's identity, game, current state, current question, deadline, answered status, score, and rank.
- Given a reconnect during `QUESTION_RESULTS` or `LEADERBOARD`, when state is restored, then the response also includes the revealed question results.
- Given a reconnect during `QUESTION_RESULTS`, `LEADERBOARD`, or `FINISHED`, when state is restored, then the response also includes the leaderboard snapshot.
- Given a valid reconnect, when the connection is attached, then the response's participant count and presence version reflect that completed attachment and a reconnected presence event is emitted only when the connection changed.

## Business Rules

### FR-7: Reconnection

- Player identity is tied to a secure session token, not to the realtime connection ID.
- Session-token hashes are uniquely indexed.
- `Reconnect` never inserts a `Participant`.
- Reconnection does not consume a new reserved seat or alter the active question's eligibility denominator.
- During `QUESTION_ACTIVE`, restoration includes the player-safe current question and whether an answer already exists for that player and question.
- The restored participant count is the absolute number of connected, non-removed players.

## Edge Cases

- Repeated reconnect attempts for the same valid session never create duplicate player records.
- A transient connection ID cannot be used as a durable player identity.
- An unknown session token is rejected with `Game.InvalidSessionToken`.
- A valid token belonging to a removed player is rejected with `Game.ParticipantRemoved`.
