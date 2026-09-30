| Concept | Brief Meaning |
|---|---|
| **Strict Binary Account Lifecycle** | Operating with only two explicit states (`Active` and `Suspended`) without intermediate, soft-deleted, or archived states. |
| **Administrative Metadata Projection (Privacy Barrier)** | Projecting strictly administrative metadata in management APIs while strictly isolating and barring access to private tenant content (quizzes, questions, gameplay). |
| **Two-Phase Suspension Architecture** | Splitting heavy account suspension into an immediate transactional cutoff (Phase 1) and asynchronous bounded background finalization (Phase 2) to prevent transaction timeouts and lock contention. |
| **Logical vs. Physical Termination (Logical Cutoff)** | Treating resources as immediately terminal in application logic as soon as the cutoff commits, without waiting for physical background row updates. |
| **Bounded Resumable Background Sweeps** | Processing cascading entity updates in bounded batches (e.g., $\le 10$ games per transaction) that can be safely resumed on system restart if interrupted. |
| **Reactivation Gate (`terminationPending`)** | Preventing account reactivation while background game termination is still in-flight (`409 Account.TerminationPending`) to prevent race conditions. |
| **Permanent Finality of Terminated Resources** | Ensuring that account reactivation only restores future operational access and never reopens or resurrects terminated games or sessions. |
| **Optimistic Concurrency via Revision Number** | Requiring a client-supplied entity `revision` number on lifecycle mutations to detect and reject concurrent conflicting updates (`409 Account.ConcurrentModification`). |
| **Last-Active-Administrator Invariant** | Enforcing transactional checks to prevent suspending or disabling the sole remaining active platform administrator (`409 Account.LastAdministrator`). |
| **Immediate Real-Time Socket Eviction** | Forcefully severing and broadcasting WebSocket/SignalR disconnection frames across cluster instances within tight latency limits ($\le 500\text{ ms}$) upon account suspension. |
| **Administrative Keyset Prefix Search** | Combining keyset cursor-based pagination with normalized prefix filtering to query large account datasets without slow table scans or `OFFSET` drift. |
| **Asynchronous Cutoff Acknowledgment (202 vs. 204)** | Returning `202 Accepted` with a tracking flag (`terminationPending: true`) when cascading background work is queued, ensuring safe and idempotent retries. |
