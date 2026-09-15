# API and Database Performance

## Purpose

Define response-time, error-rate, and data-access requirements for REST and
database work, especially the answer-submission hot path.

## NFR-1.4: API Performance

- Normal REST API requests must complete within **300 ms at p95** under expected
  production load.
- Unexpected request failures must remain below **1%**. Expected business-rule
  rejections are measured separately and must not be hidden as successful work.
- API payloads must contain only the data needed by the caller and must remain
  bounded.
- Expensive or unbounded operations must not run on request threads.
- Async, non-blocking I/O must be used throughout, with cancellation and timeouts
  propagated to supported dependencies.

## NFR-1.5: Hot-Path Performance

- The answer path must remain correct and responsive during approximately 500
  near-simultaneous submissions.
- Transactions must be short, and database round trips must be minimized.
- The answer path must not use N+1 queries or load full entity graphs when a
  projection is sufficient.
- Rate limiting must distinguish abusive repetition from a legitimate burst of
  distinct players sharing a classroom or venue IP address.
- Performance optimizations must not weaken deadline checks, duplicate-answer
  prevention, authorization, scoring correctness, or durable persistence.

## NFR-11: EF Core and PostgreSQL Rules

- Use `AsNoTracking()` for read-only queries and project only the required
  columns.
- Avoid long `Include` chains on hot paths.
- Use asynchronous database APIs with `CancellationToken`; do not use
  sync-over-async.
- Call `SaveChangesAsync` at intentional use-case boundaries.
- Use explicit transactions only when multiple writes must succeed atomically.
- Expected unique-constraint and optimistic-concurrency failures must become the
  platform's normal `Result` and `ProblemDetails` responses; provider messages
  must never leak to clients.
- Database indexes must correspond to observed query patterns and be documented
  with the schema.

## Verification

- Report p50, p95, and p99 latency for normal APIs and answer submissions.
- Correlate latency with CPU, memory, database connection-pool use, lock waits,
  slow queries, and network timing.
- A test that preserves data but exceeds a required latency percentile fails the
  performance requirement; a fast test with any integrity violation also fails.
