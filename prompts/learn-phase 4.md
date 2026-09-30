| Concept | Brief Meaning |
|---|---|
| **Never-Played vs. Ever-Played Invariant** | Permitting deletion only for quizzes never used in games, while permanently preserving ever-played quizzes (`409 Quiz.HasSessions`) for historical reporting integrity. |
| **Active Session Lock (`Quiz.InUse`)** | Rejecting quiz modifications or deletions (`409 Quiz.InUse`) while any active game session launched from that quiz is currently in progress. |
| **Immutable Game Snapshots** | Copying quiz questions, choices, duration, and image references into an immutable snapshot at game launch, decoupling live gameplay from subsequent quiz edits. |
| **Contiguous Zero-Based Re-indexing** | Automatically recalculating `OrderIndex` upon question addition or deletion to preserve a strictly sequential, gapless sequence (`0, 1, 2, ...`). |
| **Permutation Reordering Validation** | Enforcing that question reorder requests contain an exact 1-to-1 permutation of all current question IDs (rejecting missing, extra, or duplicate IDs). |
| **Technical Safety Bound (200 Questions)** | Imposing a strict system limit (maximum 200 questions) to bound memory usage, database payload sizes, and snapshot generation latency ($p95 \le 200\text{ ms}$). |
| **Monotonic Revision Tracking** | Incrementing a sequential `Revision` counter on every quiz or question mutation to enforce optimistic concurrency and detect conflicting edits (`409 Quiz.ConcurrentModification`). |
| **Atomic Choice Set Replacement** | Replacing the entire set of choices atomically in a single transaction during question updates to prevent partial states or orphaned choices. |
| **Cross-Tenant Image Attachment Defense** | Validating that referenced image IDs belong to the identical `HostAccountId` via tenant-matched composite foreign keys and unique constraints before attaching. |
| **Serialized Host Mutation Barrier** | Using transactional row locking to serialize game snapshot generation against simultaneous quiz edits, preventing partial or inconsistent snapshot capture. |
| **Plain-Text Content Sanitization (XSS Defense)** | Storing question and choice content strictly as scalar plain text and HTML-encoding upon rendering, eliminating stored XSS risks without markdown parsing. |
