| Concept | Brief Meaning |
|---|---|
| **NTP Clock Synchronization with Skew Bound** | Requiring all backend nodes to sync with UTC via NTP with a maximum allowed skew of $50\text{ ms}$; nodes exceeding this fail readiness probes or fall back to database-authoritative timestamps. |
| **Monotonic Elapsed Time (Leap-Second Safety)** | Computing elapsed time monotonically so that clock rollbacks or leap-second adjustments cannot grant players extra answering time. |
| **Database-Authoritative Timestamp Fallback** | Falling back to `CURRENT_TIMESTAMP` from the database as the authoritative time source when a node's local clock skew exceeds safe boundaries. |
| **Inclusive Server-Side Deadline (`serverTime <= endsAt`)** | Using a strict inclusive comparison (`<=`) for deadline acceptance so a submission processed at exactly `endsAt` is accepted; anything processed after is rejected. |
| **Unique Constraint as Idempotency Guard** | Enforcing a `UNIQUE(GameId, QuestionId, ParticipantId)` database constraint to prevent double-scoring on answer resubmissions, returning `alreadyAnswered: true` without re-executing scoring logic. |
| **Row-Level Lock with Bounded Wait Timeout** | Using row-level locking on answer inserts with a bounded lock-wait timeout ($\le 3\text{ s}$) to prevent indefinite blocking during high-concurrency answer bursts. |
| **Deadlock Retry Policy** | Automatically retrying up to 2 times within 200 ms on transient database deadlocks during concurrent answer inserts and score updates before surfacing a failure. |
| **Multi-Tier Rate Limiting (Socket + Participant-Scoped)** | Applying two independent rate limit layers: per-socket (5 attempts / 3 seconds) and per-participant across all connections (10 attempts / question) to prevent reconnect-cycling abuse. |
| **ParticipantId-Scoped Rate Limit (NAT Non-Penalization)** | Binding rate limits to the authenticated `ParticipantId` and connection ID instead of client IP, so shared school/office NAT does not penalize unrelated students. |
| **Node Clock Health Probe (Readiness Failure on Drift)** | Failing node readiness checks when clock skew exceeds the safe threshold so load balancers remove the drifted node from the cluster before it makes inconsistent deadline decisions. |
| **5,000 Answers/Second Platform Throughput with Zero Loss** | Sustaining 5,000 valid answer submissions per second across 200 concurrent games for at least 5 seconds with every accepted answer durably committed to relational storage (zero lost answers tolerated). |
| **500-Answer Single-Game Burst in ~1 Second** | Ingesting 500 simultaneous answers for a single game in approximately 1 second without dropping submissions, connection pool starvation, or lock contention errors. |
| **Catch-Up Payload on Reconnect** | Delivering an authoritative state catch-up on WebSocket reconnection (including `alreadyAnswered` status) so clients self-recover from network drops without requiring a separate REST poll. |
