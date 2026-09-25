Act as a Senior .NET Backend and Security Engineer working inside my existing Kahoot ASP.NET Core solution.

Your task is to inspect the existing project first, then implement a production-quality `RefreshTokenCleanupWorker` that cleans obsolete refresh-token records safely and efficiently.

Do NOT redesign the authentication system. Extend the existing architecture and conventions already present in the repository.

## Project Context

Architecture:

```text
Kahoot.Domain
Kahoot.Application
Kahoot.Infrastructure
Kahoot.Api
Tests
```

Respect the existing dependency direction and folder conventions.

The cleanup worker is an infrastructure/operational persistence concern and should therefore live in `Kahoot.Infrastructure`, unless the current project structure clearly establishes another existing convention for background workers.

Do not create unnecessary repositories, generic abstractions, services, interfaces, or design patterns.

---

# Functional Requirement

Refresh-token records must NOT be deleted immediately when they expire, are revoked, or are rotated.

They are intentionally retained temporarily because previously consumed/revoked refresh tokens are needed for replay detection and security investigation.

A refresh-token record becomes permanently deletable only when:

```text
CurrentUtcTime >= Max(ExpiresAt, RevokedAt) + 7 days
```

Interpret nullable `RevokedAt` correctly.

If the token was never revoked, cleanup eligibility should effectively be based on:

```text
ExpiresAt + 7 days
```

If it was revoked after its expiration timestamp, use the later `RevokedAt`.

If it was revoked before expiration, use `ExpiresAt`.

The cleanup worker must never delete:

- active refresh tokens,
- unexpired refresh tokens,
- recently expired refresh tokens still inside the 7-day retention period,
- revoked refresh tokens still inside the 7-day retention period,
- records still required for refresh-token replay detection.

---

# Worker Requirements

Implement a background worker named:

```text
RefreshTokenCleanupWorker
```

Prefer `BackgroundService` unless the project already has a different established worker abstraction.

The worker should:

1. Run periodically every 10–15 minutes.
2. Use `TimeProvider` rather than `DateTime.UtcNow`.
3. Support graceful application cancellation via `CancellationToken`.
4. Never use `Thread.Sleep`.
5. Catch operational failures so a transient cleanup failure does not crash the ASP.NET Core process.
6. Log failures using structured logging without logging:
   - raw refresh tokens,
   - token hashes,
   - passwords,
   - JWT access tokens,
   - authorization headers,
   - other credentials.
7. Retry naturally on the next worker cycle.
8. Keep each deletion transaction bounded.

---

# Bounded Batch Cleanup

Delete at most:

```text
500 refresh-token records per database transaction
```

Do NOT issue one unbounded delete against the entire historical refresh-token table.

The cleanup process must be safe when the database contains a very large number of historical records.

The system should be capable of draining at least:

```text
20,000 cleanup-eligible records per hour
```

under normal operating conditions.

Avoid:

- loading thousands of EF entities into memory,
- tracking entities unnecessarily,
- one huge transaction,
- table-wide locks,
- long-running database transactions,
- unnecessary round trips.

Prefer efficient PostgreSQL/EF Core operations such as projections, bounded key selection, and `ExecuteDeleteAsync` where appropriate and compatible with concurrency requirements.

---

# PostgreSQL Multi-Instance Safety

The target architecture can run multiple backend replicas.

The cleanup worker must therefore be safe if multiple instances execute it simultaneously.

Use the simplest correct PostgreSQL concurrency strategy that fits the existing persistence architecture.

Where needed, use PostgreSQL row locking such as:

```sql
FOR UPDATE SKIP LOCKED
```

or an already-established advisory-lock strategy in the project.

Do not introduce Redis, distributed locks, Quartz, Hangfire, MassTransit, or another external dependency just for this worker.

The goal is:

```text
Worker A gets one cleanup batch
Worker B gets another cleanup batch

rather than:

Worker A and Worker B repeatedly processing the same rows
```

Keep transactions short.

If raw SQL is required because EF Core cannot express the required `SKIP LOCKED` operation safely and clearly, use parameterized SQL and keep that implementation encapsulated in Infrastructure.

Before choosing raw SQL, inspect the existing project for established PostgreSQL/EF Core patterns.

---

# Important Cleanup Semantics

Assume the entity contains timestamps equivalent to:

```text
ExpiresAt
RevokedAt?
RotatedAt?
```

Do NOT assume exact property names without inspecting the repository.

Use the actual existing entity/model.

The cleanup criterion is based on:

```text
Max(ExpiresAt, RevokedAt) + 7 days
```

unless the existing model has an equivalent canonical invalidation timestamp.

Do not delete a record merely because:

```text
RotatedAt != null
```

A rotated token can be important for replay detection.

For example:

```text
Token expires: 2026-09-10
Token revoked: 2026-09-08

Cleanup eligible:
2026-09-17
```

because expiration is later.

Another example:

```text
Token expires: 2026-09-10
Token revoked: 2026-09-15

Cleanup eligible:
2026-09-22
```

because revocation is later.

Boundary behavior must be exact:

```text
CurrentUtcTime < CleanupAt
    => KEEP

CurrentUtcTime == CleanupAt
    => DELETE ELIGIBLE

CurrentUtcTime > CleanupAt
    => DELETE ELIGIBLE
```

---

# Worker Lifecycle

Use a structure similar conceptually to:

```text
Application starts
      ↓
Worker waits until scheduled cleanup
      ↓
Create DI scope
      ↓
Select <= 500 eligible tokens
      ↓
Delete batch transactionally
      ↓
Commit
      ↓
Continue bounded draining if appropriate
      ↓
Yield / delay
      ↓
Repeat
```

Do not inject a scoped EF Core `DbContext` directly into a singleton `BackgroundService`.

If the worker itself is singleton, use:

```text
IServiceScopeFactory
```

or the existing project convention to resolve scoped persistence dependencies for each cleanup pass/batch.

Dispose scopes correctly.

---

# Scheduling

The requirement is that a cleanup pass occurs at least once every 15 minutes.

Prefer a clear periodic implementation using `PeriodicTimer` or the project's existing scheduling convention.

Use `TimeProvider` where supported so scheduling/time-dependent tests remain deterministic.

Do not create overlapping cleanup executions.

If a cleanup pass takes longer than expected, the same worker instance should not start another overlapping pass.

---

# Failure Handling

The worker is a best-effort maintenance worker.

A cleanup failure must:

```text
be logged
not crash the API process
leave existing refresh-token security behavior correct
retry on a later pass
```

Database transaction failure must not partially delete a batch.

Support graceful shutdown.

If cancellation occurs:

```text
OperationCanceledException
```

caused by application shutdown should not be logged as an error.

Unexpected exceptions should be logged with structured information such as:

```text
EventName
DeletedCount if available
ElapsedMilliseconds
```

but never token data.

---

# Database Indexing

Inspect the existing refresh-token schema and EF Core configuration.

Determine whether the cleanup query has an appropriate index for its eligibility predicate.

Do NOT blindly add an index.

If an index is genuinely necessary, explain:

- what cleanup query it supports,
- why existing indexes are insufficient,
- PostgreSQL implications,
- write overhead introduced by the index.

Avoid adding speculative indexes.

---

# Registration

Register the worker through the existing Infrastructure/DI registration mechanism.

For example, conceptually:

```csharp
services.AddHostedService<RefreshTokenCleanupWorker>();
```

but follow the project's existing installer/service-registration conventions.

Do not put registration randomly into `Program.cs` if the project already centralizes Infrastructure registrations.

---

# Tests

Don't write any testing code, but make the code stable in the future. 


## 1. Cleanup eligibility tests

Verify:

```text
Expired > 7 days ago
=> deleted
```

```text
Expired exactly 7 days ago
=> deleted
```

```text
Expired 6 days 23:59:59 ago
=> retained
```

```text
Revoked later than expiration
=> retention calculated from RevokedAt
```

```text
Expiration later than RevokedAt
=> retention calculated from ExpiresAt
```

```text
Active/unexpired token
=> retained
```

```text
Recently rotated token
=> retained until evidence-retention cutoff
```

Use `TimeProvider` / fake time rather than `Thread.Sleep`.

## 2. Batch test

Create:

```text
1,200 eligible records
```

Verify cleanup uses bounded batches of at most:

```text
500
```

and can eventually remove all eligible records without touching ineligible records.

If the implementation naturally drains multiple batches during one pass, verify transaction/batch boundaries where practical without coupling tests to unimportant implementation details.

## 3. Failure test

Simulate a database failure if the existing test infrastructure permits it.

Verify:

- worker process logic survives,
- no invalid partial state is committed,
- future cleanup execution can succeed.

## 4. Concurrency / PostgreSQL integration test

Where practical using the existing PostgreSQL integration-test infrastructure, execute cleanup from two worker-like operations concurrently.

Verify:

- no deadlock,
- no corruption,
- no duplicate-processing problem,
- all eligible rows are eventually removed.

Prefer testing against PostgreSQL rather than EF Core InMemory for locking/SQL/concurrency behavior.

---

# Architecture Expectations

Explain before implementation:

1. Why the worker belongs in `Kahoot.Infrastructure`.
2. Why this is a background worker instead of cleanup performed during login/refresh/logout requests.
3. Why records are retained for 7 days instead of deleted immediately.
4. Why bounded batches are required.
5. How the implementation remains safe with multiple API replicas.
6. Why no additional repository/library/distributed scheduler is necessary.

Keep Domain and Application free of worker scheduling/infrastructure concerns.

Do not modify unrelated authentication code unless a small change is genuinely required for the cleanup implementation.

---

# Security Requirements

Never:

- store or log raw refresh tokens,
- output token hashes in logs,
- expose cleanup details through an HTTP endpoint,
- delete active credentials accidentally,
- use client-provided timestamps for cleanup decisions.

All cleanup timing must use trusted server time.

PostgreSQL remains the durable source of truth.

---

# Implementation Quality

Code must follow modern .NET practices:

- constructor injection,
- async/await,
- `CancellationToken`,
- `TimeProvider`,
- bounded transactions,
- structured logging,
- proper service lifetimes,
- clean naming,
- small focused methods,
- no hidden static mutable state,
- no unnecessary interfaces,
- no unnecessary design patterns.

Do not introduce a repository only for this worker if `IAppDbContext` or the existing Infrastructure persistence abstraction already provides everything needed.

Do not introduce MediatR commands for periodic infrastructure cleanup unless the project already deliberately models maintenance workers that way.

---

# Before Coding

First inspect:

- refresh-token entity/model,
- EF Core configuration,
- `IAppDbContext`,
- existing authentication handlers,
- refresh-token rotation logic,
- logout/logout-all/password-change code,
- existing background workers,
- Infrastructure DI registration,
- testing infrastructure,
- PostgreSQL conventions.

Then briefly report:

```text
Existing relevant architecture
Files that will be changed/added
Any assumptions
Chosen concurrency strategy
```

After that, implement the worker.

At the end, provide:

1. List of files created/modified.
2. Explanation of the cleanup algorithm.
3. Explanation of transaction and multi-instance behavior.
4. Explanation of DI lifetime choices.
5. Tests added.
6. Any index/migration added and why.
7. Any requirements you could not implement because the current codebase lacks the necessary model/state.

Do not invent missing project structures. Adapt the implementation to the repository that actually exists.

The most important thing is that you don't write any unit testing or integration testing, but make the code testable for the future. 