# Production Bottleneck Analysis and Solution Plan

> Scope: documentation and recommendations only. This file does not change the
> application, infrastructure, secrets, or load-test code. Analysis is based on
> [`walkthrough.md`](./walkthrough.md), the current answer-processing path, the
> production Compose limits, and authoritative PostgreSQL, Npgsql, EF Core,
> Docker, OpenTelemetry, and k6 guidance. Researched 2026-09-14.

## Executive conclusion

The failed answer-burst test is not a SignalR connection or broadcast problem.
The measured WebSocket connection success, handshake latency, broadcast latency,
and data-integrity checks all passed. The release blocker is the synchronous
answer acknowledgement path under a single-game burst:

- Approximately 231 players submitted in one second.
- Client-observed answer latency reached p50 7.148 s, p95 9.291 s, and p99
  9.891 s.
- Each accepted answer performs its own validation read and insert. A correct
  answer also performs a score update in a transaction.
- After every new answer, the hub synchronously invokes an auto-end handler that
  reads the game, counts all answers, and runs a participant/answer anti-join.
- PostgreSQL and the backend are each hard-limited to 0.75 CPU in production,
  while the same 2-vCPU VM also runs the complete observability stack.

The walkthrough calls this “database contention,” but the available evidence is
not enough to prove whether the dominant wait is CPU throttling, Npgsql pool wait,
PostgreSQL row/lock wait, WAL/disk latency, query execution, or a combination.
The first action must therefore capture those dimensions during the same burst.

The recommended fix is to remove repeated aggregate queries from the answer hot
path, commit each unique answer, score change, and durable progress increment in
one short atomic database operation, and let only the one caller that wins a
conditional state transition build and broadcast results. Resource and pool
tuning should follow measurement, not substitute for that application fix.

## Evidence from this repository

| Finding | Repository evidence | Consequence |
|---|---|---|
| SignalR transport is healthy at the tested scale | `walkthrough.md` reports 100% connection success, 233–278 ms handshake p95, and approximately 1 ms question delivery p95 | Do not add a backplane or Azure SignalR Service to solve this answer-write bottleneck |
| The answer request starts with a database validation projection | `SubmitAnswerCommandHandler.cs` reads the game, participant, current question, and selected choice before writing | At least one read round trip per submission |
| Correct answers use an explicit transaction | The handler inserts the answer, updates participant score, and commits | Correct answers add update and transaction-protocol work |
| Every accepted answer immediately runs auto-end detection | `GameHub.SubmitAnswer` awaits `TryAutoEndQuestionCommand` before returning the hub acknowledgement | Client latency includes auto-end work for every answer |
| Auto-end performs repeated aggregates | The auto-end handler reads the game, counts answers, and counts connected unanswered participants with an anti-join | The same growing tables are scanned/probed hundreds of times during the burst |
| Application timing does not cover the entire client-visible path | `kahoot.answer.processing.duration` stops when `SubmitAnswerCommandHandler` returns, before the hub's auto-end call | The application histogram can under-report the latency measured by k6 |
| The database and backend are CPU-capped | `docker-compose.prod.yml` assigns `cpus: 0.75` to each | Neither critical service can use a complete vCPU during a burst |
| The host is oversubscribed by design | The 2-vCPU VM also runs Nginx, frontend, Prometheus, Grafana, Loki, Jaeger, OTel Collector, cAdvisor, and exporters | Telemetry and application work compete for the same two cores and storage |
| Required scale and configured load disagree | NFR-1 requires 500 near-simultaneous answers, while the current scenario defaults to 250 | Passing 250 is necessary but not the production acceptance gate |
| The latency gate is internally inconsistent | NFR and scenario comments say p95 < 500 ms; `thresholds.js` enforces p95 < 1000 ms | A single explicit release SLO must be restored before judging success |

### Estimated database-work amplification

For each newly accepted answer, the current path normally executes:

1. One validation `SELECT`.
2. One answer `INSERT`.
3. One participant score `UPDATE` when points are awarded.
4. One auto-end game `SELECT`.
5. One answer `COUNT`.
6. One unanswered-participant `COUNT` with an answer anti-join.

Using the walkthrough's 229 accepted answers and the load script's approximate
85% correct-answer mix, this implies roughly 1,340 SQL statements, plus transaction
protocol messages and the winning close/results queries, for one classroom answer
burst. This is an inference from the current code, not a database trace. It must be
confirmed with Npgsql spans and `pg_stat_statements` call counts.

## Recommended solution, in priority order

### P0 — Measure the actual wait before changing capacity settings

Run the unchanged burst in a production-like environment and preserve a single
time-correlated evidence bundle:

- k6 answer p50/p90/p95/p99, throughput, accepted/rejected counts, and correctness
  invariants;
- end-to-end hub invocation duration, plus separate spans for validation, answer
  persistence, score update, progress update, auto-close attempt, results query,
  and fan-out;
- Npgsql used/open/max pool connections and database operation duration;
- PostgreSQL `pg_stat_activity.wait_event_type`, ungranted `pg_locks`, transaction
  age, and `pg_stat_statements` calls, total time, mean time, rows, and buffer/I/O
  statistics for the answer and auto-end queries;
- backend and database CPU usage against their container quota, CFS throttled
  periods/time, memory, disk latency/IOPS, and PostgreSQL WAL/checkpoint activity;
- .NET thread-pool queue length and exception rate.

PostgreSQL documents `pg_stat_statements` as the query-level planning/execution
statistics source and `pg_locks` as the way to identify ungranted lock contention.
Use `EXPLAIN (ANALYZE, BUFFERS, WAL)` on representative queries in a transaction
that is rolled back when the statement has side effects. Npgsql already exposes
pool and operation metrics and is already registered with OpenTelemetry in this
project.

Decision rules:

| Observation | Interpretation | Next action |
|---|---|---|
| Backend or DB CFS throttling rises during the burst | Container CPU quota is actively extending the queue | Re-run with corrected CPU allocation before evaluating query tuning |
| Npgsql used connections reach max and client duration grows before DB duration | Pool acquisition is contributing | Reduce hot-path round trips first, then benchmark bounded pool sizes |
| Many active sessions wait on `Lock`/`transactionid`/`tuple` | Hot-row or unique-index serialization dominates | Shorten the atomic operation and inspect the exact blocker |
| `WALSync`, `DataFileRead`, or I/O latency dominates | Storage durability path is limiting throughput | Move DB I/O to appropriately provisioned storage/service; retain durability |
| Auto-end query IDs dominate calls or total execution time | Repeated counts are the primary amplification | Replace them with durable progress counters and one conditional close |
| DB spans are short but hub acknowledgements remain long | Work outside the command handler is dominating | Inspect auto-end scheduling, thread-pool queue, and fan-out spans |

Authoritative references: [PostgreSQL monitoring](https://www.postgresql.org/docs/18/monitoring.html),
[`pg_stat_statements`](https://www.postgresql.org/docs/18/pgstatstatements.html),
[PostgreSQL lock monitoring](https://www.postgresql.org/docs/18/monitoring-locks.html),
[PostgreSQL `EXPLAIN`](https://www.postgresql.org/docs/18/sql-explain.html), and
[Npgsql metrics](https://www.npgsql.org/doc/diagnostics/metrics.html).

### P1 — Remove aggregate queries from every answer acknowledgement

Persist two counters for the active question:

- `eligible_count`: captured when the question activates;
- `answered_count`: incremented exactly once for every successfully inserted
  answer, including wrong or zero-point answers.

The unique database constraint on `(game_session_id, question_id, participant_id)`
must remain the final idempotency authority. A duplicate insert must not update the
score or the counter.

After a unique answer commits, compare stored counters instead of running `COUNT`
and anti-join queries. The close operation should be one conditional database
transition equivalent to “change active to results only when answered is at least
eligible.” PostgreSQL should return the changed row so only the winning caller
builds results and broadcasts `QuestionEnded`. PostgreSQL's `RETURNING` feature is
specifically designed to avoid a follow-up query for modified-row data.

Correct eligibility behavior must match the project contract:

- disconnect does not change eligibility and must not invoke auto-end;
- removal of an eligible participant who has not answered decrements eligibility
  exactly once in the same atomic operation as removal;
- removal after an accepted answer preserves both counters;
- counters never become negative and must satisfy
  `0 <= answered_count <= eligible_count <= 500`.

References: [PostgreSQL `UPDATE ... RETURNING`](https://www.postgresql.org/docs/18/sql-update.html)
and [returning modified rows](https://www.postgresql.org/docs/18/dml-returning.html).

### P2 — Collapse answer persistence into one short atomic database operation

The target shape is one parameterized database round trip that:

1. Revalidates participant ownership/removal, active game/question, selected choice,
   and server deadline at the database boundary.
2. Inserts the answer only when the uniqueness constraint permits it.
3. Updates the participant score only from the row that was actually inserted.
4. Increments `answered_count` only from the row that was actually inserted.
5. Returns accepted/duplicate, awarded points, post-update counters, and whether a
   close attempt is required.

A PostgreSQL data-modifying CTE, a narrowly scoped database function, or an
equivalent parameterized Npgsql batch can implement this shape. Choose only after
benchmarking the generated plan. Do not concatenate SQL, weaken validation, or
accept before the durable transaction commits.

EF Core documents that change tracking and `SaveChanges` add runtime work, while
set-based execution can avoid tracking overhead; it also warns that immediately
executed updates do not automatically provide concurrency control. Any optimized
path therefore needs explicit predicates and row-count/`RETURNING` checks. Npgsql
documents batching as a way to reduce round trips and automatic preparation as a
possible optimization for frequently repeated statements.

References: [EF Core saving strategies](https://learn.microsoft.com/en-us/ef/core/saving/),
[EF Core `ExecuteUpdate`](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete),
[Npgsql performance guidance](https://www.npgsql.org/doc/performance.html), and
[Npgsql prepared statements](https://www.npgsql.org/doc/prepare.html).

### P3 — Keep acknowledgement and fan-out semantics honest

The SignalR acknowledgement should represent the durable answer outcome. Normal
answers should not wait on repeated global aggregates or unrelated notifications.
If the answer wins the close transition, result construction and notification may
follow the commit, but a fan-out failure must not turn the committed answer into a
failed acknowledgement. Reconnect/REST state remains the recovery source.

Add a metric for the complete `GameHub.SubmitAnswer` invocation. Keep the existing
command-handler metric as a component metric, not as a substitute for client-visible
latency. This closes the current observability gap between k6 and application
histograms.

Do not move answer durability to an in-memory channel. A queued design is safe only
with a durable broker, idempotent consumers, backpressure, and an acknowledgement
contract that does not claim persistence prematurely. That complexity is not
justified for 500 answers when a short PostgreSQL operation should handle the load.

### P4 — Correct the production resource envelope

`cpus: 0.75` is a hard allocation limit in Compose, not merely documentation.
Docker defines `cpus` as the fractional number of CPUs allocated to a container.
The current production file therefore constrains both PostgreSQL and ASP.NET Core
to less than one core each during the exact workload that needs concurrent CPU.

Use the P0 evidence to choose one of these deployment shapes:

1. Preferred production shape: move PostgreSQL to Azure Database for PostgreSQL
   Flexible Server and keep application/observability workloads isolated from the
   database compute and storage path.
2. Lower-cost intermediate shape: resize to at least 4 vCPU, give backend and DB
   measured headroom, and move the observability stack to another host or managed
   services.
3. Diagnostic-only experiment: on a non-production clone, remove the two 0.75 CPU
   caps and run the same test with and without the observability stack. This proves
   quota/telemetry impact but is not by itself a production design.

Do not simply raise every container limit on the existing 2-vCPU host; that changes
hard throttling into uncontrolled contention. Size CPU and storage from measured
peak demand. Docker's resource documentation explains the CPU constraint, while
Azure's PostgreSQL guidance emphasizes aligning compute, storage, IOPS, throughput,
and latency with the workload.

References: [Docker Compose service CPU limits](https://docs.docker.com/reference/compose-file/services/#cpus)
and [Azure PostgreSQL operational performance planning](https://learn.microsoft.com/en-us/azure/postgresql/compute-storage/concepts-optimal-performance).

### P5 — Tune the pool only after reducing database work

Npgsql pooling is enabled by default and its default maximum pool size is 100. A
larger pool is not automatically faster: on a CPU-capped PostgreSQL instance it can
increase concurrency, context switching, and lock/WAL queues. Conversely, an
undersized pool can make callers wait before PostgreSQL sees the command.

After P1/P2, run the identical burst with a bounded matrix such as 20, 40, 60, and
100 maximum connections. Select the smallest value that meets the latency gate
without pool starvation, PostgreSQL overload, or errors. Record pool-used/max,
database active sessions, CPU, waits, throughput, and p95/p99 for every run. Set
finite connection and command timeouts, but never use timeouts to hide accepted
answers or retry non-idempotent writes.

Reference: [Npgsql connection-string and pool parameters](https://www.npgsql.org/doc/connection-string-parameters).

### P6 — Reduce observability overhead without losing the evidence

The API currently uses an always-on trace sampler and instruments Npgsql commands.
During the current amplified path, one classroom burst can create well over a
thousand database spans in addition to application and SignalR spans. Measure its
cost; do not assume it is the primary cause.

For normal production traffic, prefer a configurable parent-based ratio sampler,
with collector tail-sampling rules that retain errors and slow answer traces. Keep
metrics at full fidelity. Temporarily use always-on tracing for controlled diagnostic
runs when the telemetry pipeline has sufficient resources. OpenTelemetry documents
sampling as the mechanism for controlling recording/export overhead and telemetry
volume.

Reference: [OpenTelemetry .NET sampling](https://opentelemetry.io/docs/languages/dotnet/sampling/).

### P7 — Apply secondary optimizations only when the primary path passes

- Consider Npgsql automatic preparation for the stable repeated answer statement;
  verify plan quality and prepared ratio.
- Consider EF Core `DbContext` pooling only if profiles show context initialization
  or allocation pressure after database waits are fixed. DbContext pooling and
  connection pooling solve different problems.
- Keep table statistics current and use query plans to validate indexes. Do not add
  speculative indexes: the answer uniqueness and game/question indexes already
  cover the important predicates, and every extra index increases insert cost.
- Retain synchronous durable commits. Disabling `synchronous_commit`, using
  unlogged answer data, or acknowledging an in-memory queue would violate the zero
  lost-accepted-answer requirement.
- Partitioning, sharded counters, and a durable message broker are escalation paths
  only if the short atomic counter update still shows measured hot-row contention
  at 500 answers.

## Solutions that do not address this bottleneck

| Proposal | Why it is not the first fix |
|---|---|
| Add Redis as a cache | Answers are writes requiring database durability and uniqueness; caching does not remove the authoritative write |
| Add a SignalR backplane | Connection and broadcast scenarios already passed; a backplane addresses multi-server connection routing, not repeated PostgreSQL aggregates |
| Move immediately to Azure SignalR Service | Useful when scaling connections/app instances, but current evidence identifies the answer database path, not connection handling, as the failure |
| Increase Npgsql pool size blindly | More concurrent database sessions can worsen contention on a small CPU-capped database |
| Raise the k6 threshold | Hides the regression and conflicts with NFR-1; k6 thresholds are intended to encode SLO pass/fail criteria |
| Stagger player answers | Makes the test less representative of the classroom broadcast-and-answer burst |
| Disable durable PostgreSQL commits | Can improve apparent latency by weakening the zero-loss guarantee |
| Add many indexes | The repeated query count remains, while answer inserts become more expensive |

Azure recommends Azure SignalR Service for Azure-hosted SignalR scale-out, but it
should be considered later when the application has multiple backend instances or
connection capacity becomes the measured constraint. It is not the current answer
latency remedy. See [ASP.NET Core SignalR hosting and scaling](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-10.0).

## Verification matrix and release gates

Resolve the threshold contradiction first. The repository's stated production
requirement is 500 near-simultaneous answers and p95 below 500 ms; use that as the
release gate unless the product owner deliberately changes the NFR. The existing
p95 < 1000 ms threshold may remain as an intermediate engineering gate, but it must
not be reported as full NFR compliance.

Run each candidate at least three times after warm-up in the same environment and
compare medians plus run-to-run variance. k6 recommends modeling the intended load
and using thresholds as SLO pass/fail criteria.

| Stage | Load | Required outcome |
|---|---:|---|
| Baseline reproduction | 250 answers in approximately 1 second | Capture all P0 evidence; reproduce without correctness failure |
| Application-path proof | 250 answers in approximately 1 second | p95 < 500 ms target; p99 < 3 s; zero lost/duplicate answers and score/state violations |
| Required capacity | 500 answers in approximately 1 second | p95 < 500 ms; zero integrity violations; no pool exhaustion, deadlocks, or sustained CPU throttling |
| Soak | Repeated games/bursts for at least 30 minutes | Stable memory/pool use; no growing queue, connection leak, telemetry loss, or latency drift |
| Failure recovery | Fault fan-out after a committed answer | Acknowledgement remains accepted and reconnect reconstructs authoritative state |

Also retain the already passing connection, broadcast, reconnect, and multi-game
isolation scenarios so an answer-path optimization cannot regress them.

References: [k6 thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/)
and [k6 API load-testing workflow](https://grafana.com/docs/k6/latest/testing-guides/api-load-testing/).

## Implementation sequence for a future coding session

1. Add the missing end-to-end and phase timing plus CPU-throttling panels; capture a
   clean baseline.
2. Add durable active-question eligibility/answered counters and their invariants.
3. Replace per-answer aggregate auto-end queries with one conditional close winner.
4. Collapse unique answer insert, score update, and counter increment into a short
   atomic operation; keep parameterization and the uniqueness constraint.
5. Remove disconnect-triggered auto-end and align removal with the eligibility
   contract.
6. Re-test at 250, then 500, before infrastructure tuning.
7. Correct backend/database CPU allocation or isolate PostgreSQL and observability;
   repeat the same tests.
8. Benchmark pool sizes and optional preparation/context pooling one variable at a
   time.
9. Set production trace sampling from evidence while retaining error/slow traces.
10. Do not declare the bottleneck resolved until all latency and integrity gates pass
    together at 500-answer scale.

## Final recommendation

Implement P0 through P4 as one measured optimization program. The likely largest
software gain is eliminating the per-answer auto-end counts; the likely largest
deployment gain is removing the 0.75-CPU constraints and separating database and
observability contention. Neither should be accepted on intuition alone: the next
burst run must show query-call reduction, absence of pool/lock/WAL queues, and the
required client-visible p95 while preserving zero lost or duplicate answers.
