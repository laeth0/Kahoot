# Concurrency, Reliability, and Data Integrity

## Purpose

Keep game state correct when requests race, messages repeat, dependencies fail,
or application instances restart.

## NFR-2: Concurrency and Idempotency

- At most one answer may be accepted for each
  `(gameId, questionId, participantId)` tuple. The application layer must check
  this rule, and a database unique constraint must be the final integrity boundary.
- If duplicate answers arrive concurrently, exactly one may create the answer
  and score; later attempts receive an idempotent rejection and cannot change the
  score.
- Game state uses PostgreSQL optimistic concurrency through `xmin`. Conflicting
  transitions must fail safely and be resolved without duplicating effects.
- Submit answer, start game, start question, end question, advance question, and
  end game operations must tolerate client retries and host double-clicks.
- Multiple simultaneous state transitions must never advance a game twice or
  produce an impossible state.

## NFR-3: Server-Authoritative State

- The backend is the source of truth for answer correctness, deadline eligibility,
  official time, points, game state, current question, answer status, leaderboard,
  question availability, and game completion.
- The backend is also authoritative for reserved seats, connected participants,
  and question-eligibility counts.
- Clients must not derive authoritative values by incrementing local state or by
  trusting a local timer.

## NFR-5: Reliability and Startup

- A hosted service must apply pending EF Core migrations asynchronously during
  startup. Migration failure must fail startup.
- Host bootstrap seeding must run after migration, remain idempotent, and do
  nothing when disabled.
- When host bootstrap seeding is enabled, missing credentials or a password that
  does not satisfy the configured complexity policy must fail startup.
- The system must handle duplicate requests, host double-clicks, simultaneous
  transitions, network loss, reconnect-after-answer, database timeout or slowness,
  invalid or expired PINs, answers at or after the deadline, backend restart, and
  mass reconnection deliberately.
- Liveness must describe whether the process can serve requests. Readiness must
  include required dependencies such as PostgreSQL and writable file storage.

## NFR-6: Data Integrity

- PostgreSQL must enforce appropriate primary keys, foreign keys, unique
  constraints, check constraints, and indexes.
- Every accepted answer must have exactly one durable answer record and its
  awarded points must be reflected exactly once in the participant's total score.
- A participant must belong to the game referenced by an answer, and an answer
  must reference a question snapshot belonging to that game.
- Non-obvious indexes must be justified by the query pattern they serve and
  documented in `backend/projectSchema.dbml`.
- Schema changes must use EF Core migrations, and `backend/projectSchema.dbml`
  must be updated in the same change.
- Existing migrations are immutable after they have been shared or deployed.

## Verification

- Database verification after load tests must find zero duplicate answers, zero
  duplicate or lost score applications, zero cross-game references, zero duplicate
  participants, and zero inconsistent game states.
- Correctness requirements have zero tolerance; an otherwise successful load test
  fails if any integrity query returns a violation.
