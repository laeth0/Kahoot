| Concept | Brief Meaning |
|---|---|
| **Deterministic Six-State Machine** | Enforcing exactly six canonical game states (`CREATED`, `LOBBY`, `QUESTION_ACTIVE`, `QUESTION_RESULTS`, `LEADERBOARD`, `FINISHED`) with no aliases, preventing ambiguous state references across the codebase. |
| **CREATED as Ephemeral Internal State** | Treating `CREATED` as a transient in-transaction state never exposed to clients; the game only becomes observable once it reaches `LOBBY` after the transaction commits. |
| **Join-Only-in-LOBBY Invariant** | Permitting new player joins strictly and exclusively during the `LOBBY` state; all join attempts after `Start Game` are rejected. |
| **Deep Immutable Game Snapshot** | Atomically copying the full quiz content (question text, choices, correctness flags, image URLs, durations, base points) into game snapshot records at launch, permanently decoupling live gameplay from any future quiz edits. |
| **PIN Allocation with Collision Retry** | Allocating unique 4–8 digit numeric PINs (stored as strings to preserve leading zeroes) from a large random keyspace, with up to 5 automatic retries on collision before failing. |
| **Inclusive Question Deadline (`DeadlineUtc`)** | Computing `DeadlineUtc = NOW() + DurationSeconds` and using it strictly to stop answer acceptance without automatically changing game state on expiry. |
| **Deadline ≠ State Transition (Explicit Host Control)** | Keeping the game in `QUESTION_ACTIVE` after the deadline expires until either the auto-close equality rule triggers or the Host explicitly calls `EndQuestion`. |
| **Equality-Based Auto-Close Rule** | Automatically transitioning from `QUESTION_ACTIVE` to `QUESTION_RESULTS` exactly when `acceptedAnswerCount == effectiveEligibleParticipantCount` for a non-empty eligible set. |
| **Initially-Zero-Eligible Guard** | Preventing premature auto-close when a question starts with zero eligible players, avoiding the false $0 == 0$ equality match. |
| **Effective Eligible Count & Removal Trigger** | Decrementing `effectiveEligibleParticipantCount` when the Host removes an unanswered eligible participant, potentially triggering auto-close if the equality condition is then met. |
| **StateVersion Optimistic Concurrency** | Requiring the current `stateVersion` on every Host control command to detect and reject stale concurrent transitions (`409 Game.ConcurrentModification`). |
| **CommandId Idempotency Log** | Persisting `(GameId, CommandId, ResultStateVersion, ResponsePayload)` so identical command replays return the previously committed result without re-executing the state transition. |
| **Host Disconnect Grace Period (5 Minutes)** | Starting a 300-second grace timer when the last Host connection drops, keeping the game alive and playable while allowing Host reconnection to cancel the timer. |
| **Authoritative Abandonment Finalization** | Transitioning an abandoned game to `FINISHED` (with final score materialization and PIN release) within `T + 30 seconds` after the grace timer expires, regardless of which server instance handles it. |
| **Rolling Restart Safety via Grace Window** | Designing the 300-second disconnect grace window to safely exceed the maximum rolling restart drain period ($\le 30\text{s}$), preventing false abandonment during planned deployments. |
| **FINISHED Immutable Archive Lock** | Permanently preventing all answer submissions, score mutations, participant removals, and snapshot changes once a game reaches `FINISHED` state. |
| **Pre-Reveal Correct Answer Concealment** | Stripping `isCorrect` flags from the `QUESTION_ACTIVE` player broadcast at the server, ensuring correctness data is never transmitted before the reveal phase. |
| **24-Hour Player Read-Only Recovery Window** | Granting players a 24-hour post-finish window to reconnect and view their read-only game summary using their ephemeral session token. |
| **PIN Release on FINISHED** | Freeing the game PIN for reuse by future games exclusively upon reaching the `FINISHED` state. |
