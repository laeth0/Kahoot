# Backend agent instructions

These instructions apply to `backend/`. Read the affected code and nearby dependencies before changing it. Keep changes focused, preserve existing contracts, and follow the conventions already present in the relevant project. Make this project production-ready: write clean code and apply industry best practices.

## Architecture and Core Principles

- Use Clean Architecture and Vertical Slice Architecture.
- Keep dependencies flowing in the existing direction. Put HTTP concerns in Api, application behavior, vertical feature slices, and contracts in Application, database and external-service implementations in Infrastructure, and core entities in Domain.
- Make this project production-ready: write clean code, handle edge cases gracefully, follow idiomatic C#/.NET design patterns, and ensure strict separation of concerns.

### Realtime Communication (SignalR)

> [!IMPORTANT]
> If you want to use realtime in the project, then use **SignalR**.

### Static File & Media Storage

> [!IMPORTANT]
> Store images and media files in the `wwwroot` directory in the backend (`src/Kahoot.Api/wwwroot`).

## Concurrency, MVCC, Security, and Performance Focus

- **Production-Ready Multi-Replica SaaS Scale:** This project is a production-grade SaaS platform engineered for large volumes of concurrent users and deployed across multiple horizontally scaled replicas (containers/Kubernetes pods). Every feature, background worker, and startup routine must be designed with multi-instance concurrency in mind. Never assume a single instance: in-memory synchronization (`lock`, `SemaphoreSlim`) is insufficient for cluster-wide coordination.
- **Concurrency & Race Condition Elimination:** Proactively design against race conditions (e.g., time-of-check to time-of-use / TOCTOU, double-submits, concurrent token refreshes, concurrent game sessions/joins, and answer submissions). 
- **Deadlock Avoidance & Locking Discipline:**
  - Enforce strict, consistent lock ordering across all operations and transactions to prevent circular wait deadlocks (`40P01 deadlock_detected`).
  - Keep transaction lifespans as short as possible; avoid slow network or compute operations inside database transactions.
  - Choose the appropriate locking strategy for the use case:
    - **Optimistic Concurrency Control (OCC):** Use PostgreSQL `xmin` row versioning or entity `Revision` for high-throughput, low-contention workflows.
    - **Pessimistic Concurrency (`SELECT FOR UPDATE`):** Use for strict serialization paths (e.g., inventory, seat allocation, token revocation).
    - **PostgreSQL Advisory Locks (`pg_advisory_xact_lock`):** Use transaction-scoped advisory locks for cluster-wide coordination, schema migrations, seeding, or singleton scheduled tasks.
- **PostgreSQL MVCC & Isolation:** Leverage Multi-Version Concurrency Control understanding (tuple versions, `xmin`/`xmax`, snapshot isolation). Proactively analyze and explain how queries interact with MVCC, table-level vs. row-level locks, and lock queues.
- **Security by Default:** Enforce fail-fast security configurations, least-privilege principles, strict input validation, server-side authorization boundaries, parameterized SQL statements, and safe telemetry/logging that never leaks secrets, credentials, or sensitive tokens.
- **Performance & Scalability:** Optimize critical execution paths: eliminate N+1 queries, minimize lock contention and lock hold durations, leverage PostgreSQL connection pooling, pass `CancellationToken` throughout all async paths, maintain low-cardinality telemetry metrics/spans, and avoid unnecessary allocations or blocking I/O.
- **Proactive Explanation:** Whenever implementing, reviewing, or modifying code touching MVCC, concurrency, security boundaries, or performance optimizations, proactively explain the underlying mechanisms, trade-offs, and guarantees to the user.

## Project layout

- `Kahoot.slnx` contains four .NET 10 projects under `src/` and seven test projects under `test/`.
- `Kahoot.Domain` holds entities and domain contracts. It has no project references.
- `Kahoot.Application` holds application contracts, vertical slice feature folders, MediatR command/query interfaces, results, and service registration. It references Domain.
- `Kahoot.Infrastructure` implements persistence. It references Application; its registration is in `DependencyInjection.cs`.
- `Kahoot.Api` is the HTTP entry point. It references Application and Infrastructure; `Program.cs` configures middleware, CORS, health checks, and endpoints.
- `test/` holds unit and integration test projects partitioned by architectural layer.

## Existing patterns

- Use the existing `ICommand`/`IQuery` and `Result`/`Error` types for application operations where they fit. MediatR handlers and FluentValidation validators are registered by assembly scanning in `Kahoot.Application/DependencyInjection.cs`.
- Preserve the existing ASP.NET Core Problem Details responses and centralized exception handling in `GlobalExceptionHandler`.
- EF Core uses PostgreSQL, snake_case names, `AppDbContext`, and entity configurations in `Persistence/Configurations`. The app applies pending migrations at startup.
- **Constructor and Dependency Injection Style:** Always use explicit constructor injection with `private readonly` backing fields (prefixed with `_`) and assignments inside the constructor body. Do not use C# primary constructors on classes for dependency injection.
- **Explicit Type Declarations (Avoid `var`):** Do not use the `var` keyword for variable declarations or loop iterations in new or modified code. Always use explicit, strongly-typed declarations (e.g., `DateTimeOffset utcNow = ...`, `User user = ...`, `EntityEntry<T> entry in ...`) to keep types clear and explicit. Do not perform repository-wide refactoring sweeps on existing code solely to replace `var`.
- **No Fully Qualified Type Names in Code (Use `using` Directives):** Never use long, fully qualified type names inside method bodies, type definitions, or service registrations (e.g., do not write `Kahoot.Application.Common.Interfaces.ISocketEvictionService`). Always add a clean `using` directive at the top of the file (e.g., `using Kahoot.Application.Common.Interfaces;`) and use the short type name (e.g., `ISocketEvictionService`) directly in the code.
- **Service Registration:** Register services explicitly without reflection or assembly scanning. Extension methods in `ServiceCollectionExtension` must be focused on a single responsibility (e.g., `AddPersistence`, `AddSecurity`, `AddCorsPolicy`, `AddJwtAuthentication`). Framework, hosting, and root service registrations (`AddApplication`, `AddInfrastructure`, `AddScoped<ICurrentUser, CurrentUser>`) belong directly in `Program.cs`.

## Educational Concept Comments and Query Annotations

- **Source of Truth:** Derive concept names from [`docs/`](../docs/) only.
- **Placement:** Comment directly above the specific statement. Never at class or file level.
- **Format:** `// <Concept Name> - <concise explanation>`. No prefixes (`BACKEND CONCEPT:`, `NOTE:`, etc.).
- **Focus:** Security and performance first. Scalability, concurrency, and maintainability patterns are also valid — skip obvious code.
- **Query, Concurrency & Database Annotations:** Place a comment above every query or persistence statement using a notable performance, security, or MVCC mechanism:
  - `AsNoTracking()` — eliminates change-tracker overhead and snapshot memory for read-only queries.
  - `AnyAsync` / `SingleOrDefaultAsync` — short-circuits against a unique index; avoids full table scan.
  - `FOR UPDATE` / `GetUserForUpdateAsync` — acquires a pessimistic row-level lock; serializes concurrent writers and prevents TOCTOU races.
  - `FOR UPDATE SKIP LOCKED` — skips already-locked rows; enables non-blocking parallel batch processing across replicas without deadlocks.
  - `ExecuteUpdateAsync` / `ExecuteDeleteAsync` — single atomic SQL statement pushed to the database; no entity materialization or change-tracker allocation.
  - `Select(x => x.Field)` — column projection; reduces row fetch size, network payload, and allocations.
  - `RepeatableRead` / `Serializable` isolation — MVCC snapshot prevents phantom reads and non-repeatable reads; note the increased MVCC tuple churn and serialization failure risk.
  - `pg_advisory_xact_lock` — transaction-scoped advisory lock for cluster-wide singleton coordination (e.g., seeding, scheduled tasks) without a dedicated lock table.
  - `xmin` row version / `RowVersion` / OCC (`Revision`) — optimistic concurrency; detects concurrent modification without holding a lock; prefer for high-throughput, low-contention paths.
  - CTE (`WITH ... AS MATERIALIZED`) — materializes the subquery result once; prevents the optimizer from inlining and re-evaluating it inside `DELETE`/`UPDATE`.
  - `LIMIT` / `Take()` — bounds result set size; prevents unbounded memory growth and lock escalation on large tables.
  - Keyset pagination (`WHERE id > @cursor ORDER BY id`) — O(log n) index seek per page; avoids `OFFSET` full scan degradation on deep pages.
  - Parameterized queries / `ExecuteSqlInterpolatedAsync` — EF Core translates interpolated strings into parameters; eliminates SQL injection at the query boundary.
  - Connection pool (`Npgsql`) — annotate when explicitly controlling pool size, `MinPoolSize`, or `MaxPoolSize` for throughput-sensitive paths.
  - `CancellationToken` propagation — ensures long-running database commands are cancelled on client disconnect or application shutdown, releasing server resources immediately.
  - N+1 query prevention (`Include` / `Join` / split query) — annotate when eager-loading a collection to explain why a single join or split query replaces N round-trips to the database.
  - Two-phase unique re-indexing (Negative staging) — temporarily offsets entity sequence ordinals into negative space before assigning target positions, avoiding unique constraint collisions (`(QuizId, OrderIndex)`) during bulk reordering in a single transaction.
  - Immutable aggregate snapshotting (`RepeatableRead`) — creates deep, point-in-time domain copies (quizzes, questions, choices) during game session creation under MVCC snapshot isolation, completely isolating active games from subsequent author edits or deletions.
  - Permanent tombstones / Reservation slots — preserves soft-deleted or removed participant nicknames to prevent race conditions, replay attacks, or unauthorized re-joins within active game sessions.
  - Periodic batch cleanup pacing (`ExecuteDeleteAsync` + sleep) — processes large-volume deletions (expired refresh tokens, orphan images) in bounded batches with pacing intervals, mitigating database lock contention and WAL write spikes.
- **System Design & Distributed Patterns:** Place a comment above statements and architectural boundaries implementing notable distributed system or resiliency patterns:
  - Redis pub/sub (`ISubscriber.PublishAsync`) — fan-out to all replicas over a shared channel; annotate channel name and message contract so the reader understands the cluster-wide delivery scope.
  - Redis distributed lease (`SET NX PX`) — atomic `SET key value NX PX ttl`; only one replica wins the lock; others skip; prevents duplicated background work across pods.
  - SignalR Redis Backplane — all hub messages pass through Redis so any replica can push to any connected client; annotate when a `SendAsync` or group call relies on the backplane being healthy.
  - Post-commit broadcast pattern — `SaveChangesAsync` first, then fan-out; guarantees the database row is durable before any replica receives the realtime event; prevents phantom pushes on rollback.
  - Idempotency key / `JoinOperationId` SHA-256 — deduplicate retried client requests at the database boundary; a unique index on the key makes duplicate execution a no-op instead of a double-write.
  - `IMemoryCache` / `IDistributedCache` (Redis) — annotate the cache key, TTL, and invalidation strategy; note whether stale reads are acceptable or whether cache-aside with write-through is required.
  - Retry + exponential backoff (`Polly`) — annotate transient failure policies on network calls to external services; document max attempts, jitter, and which exceptions are considered transient.
  - `Channel<T>` / In-Memory Producer-Consumer — decouples hot synchronous request paths from asynchronous background processing (e.g., host socket evictions, session finalization) without blocking thread pool threads.
  - Token Bucket / Sliding Window rate limiting (`ILobbyJoinRateLimiter` / `LoginRateLimiter`) — bounds traffic bursts and protects against credential stuffing or join flood DDoS across replicas using Redis atomic counters and TTLs.
  - Concurrency throttling / Backpressure (`SemaphoreSlim`) — caps concurrent execution of CPU/memory-heavy operations (e.g., Argon2id hashing) to physical core limits, rejecting excess load with HTTP 429 instead of degrading server latency or inducing OOM crashes.
  - Cache stampede shield / Mutex locking (Single-flight) — serializes concurrent cache refreshes or external dependency health probes using semaphores to protect downstream databases from thundering herd spikes.
  - Circuit Breaker / Graceful degradation (`Degraded` health status) — isolates failing auxiliary dependencies (e.g., image disk storage) to keep critical path workflows (live game lobbies and question countdowns) operational.
  - State machine version fencing (`StateVersion`) — authoritative monotonic sequence numbers broadcast across SignalR and verified in mutations to detect and reject out-of-order frames, network replays, or stale socket events.
  - Distributed heartbeat & lease renewals (`HostPresenceService` / `PlayerPresenceService`) — background workers maintain active session presence keys in Redis with short TTLs, enabling automatic room cleanup and seat reclamation when hosts or players disconnect.
  - Cluster socket eviction (`ISocketEvictionService`) — publishes cluster-wide eviction frames over Redis pub/sub to force-close active WebSocket connections across all server pods upon user suspension or game termination.
  - Reverse proxy header validation (`KnownIPNetworks`) — restricts `X-Forwarded-For` and `X-Forwarded-Proto` trust strictly to configured CIDR blocks, preventing IP spoofing attacks that bypass rate limiters or compromise audit logs.
  - Telemetry PII scrubbing & span filtering — strips query strings, auth tokens, client headers, and `/health` probe traces at the OpenTelemetry boundary, protecting sensitive data and preventing trace storage bloat.
  - Zero clock skew token validation (`ClockSkew = TimeSpan.Zero`) — eliminates the default 5-minute leeway on JWT validation, strictly enforcing the 15-minute access token lifetime and security version revocation boundaries.


## Database and EF Core Migrations

- **Migration Immutability:** Existing migrations and generated designer files are immutable. Never modify, rename, or delete an existing migration; always add a new migration for subsequent schema changes.
- **`projectSchema.dbml` Sync:** Whenever database models or relationships change, you MUST update `projectSchema.dbml` and generate the new EF Core migration together in the same task.
- **Separation of Concerns:** Keep schema migrations, development seed data, and production reference data strictly separated. Do not combine them in the same initialization workflow or service.

## Configuration and local verification

- Review and keep all three configuration files consistent with their intended environments whenever adding, modifying, or removing configuration values:
  - `src/Kahoot.Api/appsettings.json`: Keep only safe shared defaults that make sense across environments. Do not store secrets here. Avoid production-specific values unless they are truly shared defaults.
  - `src/Kahoot.Api/appsettings.Development.json`: Use development-friendly values. It may contain local non-sensitive defaults that simplify development (e.g., local CORS origins, local JWT signing key). Do not place real production secrets here. Keep behavior close enough to production that configuration mistakes are detectable.
  - `src/Kahoot.Api/appsettings.Production.json`: Use production-safe settings. Do not hardcode secrets such as JWT signing keys, database passwords, API keys, or credentials. Sensitive values must come from environment variables, secret stores, or the deployment platform. Prefer fail-fast validation for required production configuration instead of silent fallback values. Do not weaken security just to make startup succeed.
- **No .NET User Secrets (`secrets.json`):** Never use or initialize .NET User Secrets (`dotnet user-secrets`). Put all configuration exclusively across the three appsettings files (`appsettings.json`, `appsettings.Development.json`, and `appsettings.Production.json`). Local development configuration belongs in `appsettings.Development.json`, shared defaults belong in `appsettings.json`, and production settings belong in `appsettings.Production.json` (with production secrets supplied via environment variables / deployment platform).
- Keep PostgreSQL connection settings in `src/Kahoot.Api/appsettings.json` and `src/Kahoot.Api/appsettings.Development.json`. In `appsettings.Production.json`, keep `DefaultConnection` empty so missing production connection strings fail fast. Do not commit production passwords. In production or custom environments, `ConnectionStrings__DefaultConnection` supplies a password-bearing connection string via environment variables.
- Use the .NET Options Pattern for grouped runtime configuration when it improves type safety, validation, and maintainability:
  - Bind from the correct configuration section.
  - Validate important values with `ValidateOnStart()` where appropriate.
  - Keep validation rules consistent with the real application requirements.
  - Do not duplicate the same configuration value in multiple places.
  - Keep one source of truth for values such as token lifetimes, limits, and security settings.
  - Do not move normal implementation constants into configuration unless they genuinely need to vary by environment.
  - Verify that environment-variable overrides work correctly with ASP.NET Core configuration conventions (e.g., `Section__Key`).
- Development CORS origins are in `src/Kahoot.Api/appsettings.Development.json`. OpenAPI and Scalar are exposed only in Development. The database-aware health endpoint is `/health`.
- From `backend/`, run `dotnet build Kahoot.slnx` after code changes. Run `dotnet format Kahoot.slnx --verify-no-changes` when formatting is relevant. Report any verification that could not run.

## Testing

- Tests are part of the project and must follow the integration-testing and clean-code rules below.
- Unit, integration, concurrency, and end-to-end tests may be added when they provide meaningful coverage.
- Do not add tests merely for code coverage; prioritize business invariants, security boundaries, persistence correctness, concurrency, and integration behavior.
- Keep test infrastructure focused and avoid unnecessary abstractions.

## Integration Testing — Code Quality and Best Practices

When integration testing is enabled for the project, test code must follow the same clean-code and engineering standards as production code. Tests are part of the codebase and must be maintainable, readable, deterministic, and production-quality.

### Test Behavior, Not Implementation

- Tests must verify externally observable behavior and important business invariants.
- Prefer testing through the real application boundary (`HttpClient`, SignalR client, database) rather than directly invoking internal implementation details.
- Do not test private methods, internal implementation structure, EF Core tracking behavior, or specific service calls unless that behavior is itself an architectural requirement.
- A refactor that preserves behavior should not require rewriting large numbers of integration tests.
- Assert the smallest set of outcomes necessary to prove the behavior.

### Arrange, Act, Assert

Structure tests clearly:

```text
Arrange
Act
Assert
```

Keep each section easy to identify.

Avoid mixing setup, execution, and assertions throughout a test.

Tests should describe a single meaningful behavior. If a scenario genuinely contains multiple inseparable outcomes, keep them together; otherwise split it into focused tests.

### Test Naming

Use behavior-oriented names that explain:

```text
condition + action + expected result
```

Examples:

```csharp
ConcurrentJoins_WhenOnlyOneSeatRemains_CreateExactlyOneParticipant();

RefreshToken_WhenUsedConcurrently_AllowsOnlyOneSuccessfulRotation();

SubmitAnswer_WhenCalledTwice_DoesNotDoubleScore();

SuspendAccount_WhenRevisionIsStale_ReturnsConcurrentModification();
```

Do not use vague names such as:

```text
TestJoin();
Test1();
WorksCorrectly();
ShouldWork();
```

### Test Readability

Tests should be understandable without reading the implementation first.

Prefer:

```csharp
Host host = await TestDataFactory.CreateHostAsync();

HttpResponseMessage response = await client.PostAsJsonAsync(
    "/api/games",
    request,
    cancellationToken);

response.StatusCode.Should().Be(HttpStatusCode.Created);
```

over deeply nested helper calls that hide the actual scenario.

Do not create abstractions merely to reduce a few lines of test code.

### Test Helpers

Create helpers only when they solve real duplication or infrastructure complexity.

Good candidates include:

- application factory
- PostgreSQL container fixture
- database reset infrastructure
- authenticated client creation
- test data factories
- SignalR connection helpers
- common response assertions
- deterministic synchronization helpers

Avoid:

- generic test frameworks
- excessive builder hierarchies
- universal `TestHelper` classes
- helpers that hide important behavior
- abstractions that make tests harder to understand

A helper should have one clear responsibility.

### Test Data

Use deterministic test data.

- Do not depend on production seed data unless the test specifically verifies seed behavior.
- Prefer explicit test data factories for repeated entities.
- Do not generate random values unless randomness is the behavior being tested.
- If random values are required, make the source deterministic and reproducible.
- Do not use timestamps such as `DateTimeOffset.UtcNow` directly when the behavior depends on time. Use the project's `TimeProvider` abstraction where appropriate.
- Do not use real credentials, secrets, tokens, or production data.

Test data should make the scenario obvious.

### Database Testing

Integration tests that verify PostgreSQL behavior must use the real PostgreSQL provider.

Do not use:

```text
EF Core InMemory
mocked DbContext
mocked IQueryable
fake relational constraints
```

when testing database behavior.

Use PostgreSQL/Testcontainers where appropriate so tests exercise:

- PostgreSQL constraints
- transactions
- MVCC
- isolation levels
- row locking
- unique indexes
- foreign keys
- concurrency behavior
- PostgreSQL-specific SQL
- EF Core migrations

Do not duplicate the application's persistence logic inside the test to determine the expected result.

### Database State Verification

When a test changes persistent state, verify the important durable outcome.

For example:

```text
HTTP request
    ↓
application
    ↓
transaction
    ↓
PostgreSQL
    ↓
query database
    ↓
assert persisted state
```

Do not rely only on the HTTP response when the requirement also concerns database state.

Use a fresh DbContext when appropriate to ensure assertions are reading persisted state rather than accidentally reading tracked entities from the same context.

### Test Isolation

Tests must be independent.

- Never rely on test execution order.
- Never depend on another test creating data.
- Reset or isolate database state between tests.
- Do not leave background jobs, SignalR connections, timers, or containers running after a test.
- Dispose all resources correctly.
- Tests should be safe to run individually.

A test should pass whether it is executed:

```text
alone
first
last
in parallel
repeatedly
```

unless the test intentionally verifies concurrency.

### Parallel Test Execution

Do not globally disable test parallelization simply because shared infrastructure exists.

Prefer proper isolation.

Use:

- test collections when shared infrastructure genuinely requires serialization;
- independent database state;
- isolated test data;
- explicit synchronization for concurrency scenarios.

Concurrency tests must intentionally create concurrency and must not depend on xUnit's test-level parallelism to reproduce races.

### Concurrency Tests

Concurrency tests must be deterministic.

Do not use:

```csharp
await Task.Delay(100);
```

to try to create a race condition.

Use explicit synchronization such as:

- `TaskCompletionSource`
- `Barrier`
- `CountdownEvent`
- controlled database locks
- explicit coordination points

The test must control when competing operations proceed.

A concurrency test should clearly document:

```text
Operation A
Operation B
Synchronization point
Expected final state
Expected winner/loser behavior
```

Verify both:

- the result returned to each concurrent operation;
- the final authoritative PostgreSQL state.

Do not assert only that "one request failed."

Assert the exact invariant that must hold.

### Idempotency Tests

For every important idempotent operation, test:

1. first request succeeds;
2. identical retry produces the documented idempotent result;
3. the operation is not executed twice;
4. persistent state contains exactly one logical operation;
5. the same idempotency key with different request data is rejected.

Examples include:

```text
JoinOperationId
commandId
refresh-token rotation
logout-all
game commands
```

### Transactions and Rollbacks

When testing transactional behavior, verify atomicity.

For an operation that modifies multiple pieces of state:

```text
Either everything commits
or
everything rolls back
```

Do not test transactions by mocking `SaveChangesAsync`.

Use real PostgreSQL transactions.

Where a failure must occur between two database operations, create a deterministic failure point rather than using timing-based failures.

### Time-Dependent Tests

Do not use arbitrary sleeps to wait for expiration or worker execution.

Prefer the project's `TimeProvider` abstraction.

Examples:

```text
token expiration
refresh-token retention
game deadlines
abandonment grace periods
worker scheduling
temporary reservations
```

Tests should advance controlled time rather than waiting in real time whenever possible.

### Background Worker Tests

Background workers must be testable without making the suite slow or flaky.

Prefer:

```text
trigger worker behavior
↓
wait for a deterministic completion signal
↓
assert database state
```

over:

```text
start worker
↓
Task.Delay(...)
↓
hope worker ran
```

If the production worker is periodic, provide an appropriate test seam such as a controlled `TimeProvider` or explicit execution method where that fits the architecture.

Do not redesign production services only to make tests convenient.

### SignalR Integration Tests

Use a real SignalR client when testing SignalR behavior.

Do not mock the Hub to test:

- authentication
- group membership
- event delivery
- reconnection
- audience isolation
- connection lifecycle

Verify that the correct client receives the event and that unauthorized clients do not.

For security-sensitive realtime tests, explicitly verify that sensitive fields are absent from the payload, not merely that the expected fields exist.

### Authentication in Tests

Do not bypass authentication for tests that are specifically testing:

- authentication
- authorization
- tenant isolation
- role restrictions
- token invalidation
- suspension
- refresh-token behavior
- SignalR authentication

Test through the real authentication mechanism where practical.

Test helpers may create authenticated clients for scenarios where authentication itself is not the subject of the test, but the helper must still produce realistic authenticated requests.

Do not create fake claims that cannot exist in production.

### Assertions

Prefer precise assertions.

Bad:

```csharp
response.Should().NotBeNull();
```

Better:

```csharp
response.StatusCode.Should().Be(HttpStatusCode.Conflict);
```

And when relevant:

```csharp
problemDetails.Error.Code.Should().Be("Auth.RefreshRace");
```

Assertions should explain why the behavior is required.

Do not assert irrelevant implementation details.

Avoid huge assertion blocks that make it difficult to identify the actual failure.

### Failure Diagnostics

Integration tests must produce useful diagnostics when they fail.

Include enough context to identify:

- request
- relevant entity IDs
- expected result
- actual result
- database state when relevant

Do not log secrets or sensitive authentication material.

Do not dump entire database tables or entire HTTP responses unless the test specifically requires them.

### CancellationToken

Propagate `CancellationToken` through asynchronous test operations when the APIs support it.

Do not introduce arbitrary cancellation tokens everywhere merely for style.

Use cancellation to prevent hanging integration tests and to correctly model request cancellation where relevant.

### Async Code

Use asynchronous APIs consistently.

Do not use:

```csharp
.Result
.Wait()
Task.Run(...)
Thread.Sleep(...)
```

to coordinate normal integration tests.

Avoid blocking test threads.

### Resource Disposal

All externally allocated resources must be disposed correctly.

This includes:

- `WebApplicationFactory`
- `HttpClient`
- SignalR connections
- PostgreSQL containers
- database connections
- streams
- temporary files
- cancellation sources

Prefer `IAsyncLifetime`, `IAsyncDisposable`, or the appropriate framework lifecycle mechanism where asynchronous cleanup is required.

### Configuration

Integration tests must use explicit test configuration.

Do not modify production configuration files merely to make tests pass.

Do not hardcode:

- production connection strings
- passwords
- signing keys
- API keys
- external service credentials

Test-specific configuration belongs in the test infrastructure.

### External Dependencies

Mock external dependencies only when the test is specifically testing application behavior around that dependency and a real integration environment is impractical.

Do not mock PostgreSQL to test persistence.

Do not mock SignalR to test realtime communication.

Do not mock EF Core to test EF Core behavior.

For infrastructure such as object storage, Redis, or message brokers, prefer real containerized dependencies when the purpose of the test is to verify their integration.

### Flaky Tests

Never "fix" a flaky test by:

- adding arbitrary delays;
- increasing retry counts;
- disabling parallel execution globally;
- weakening assertions;
- swallowing exceptions;
- adding large timeouts.

Find the actual race, lifecycle, isolation, or synchronization problem.

Integration tests must be deterministic and reproducible.

### Test Performance

Integration tests are more expensive than unit tests, so keep them focused.

- Reuse expensive infrastructure such as a PostgreSQL container when safe.
- Do not recreate containers for every test.
- Reset database state efficiently.
- Avoid unnecessary HTTP requests.
- Avoid unnecessary database queries.
- Do not perform unrelated setup.
- Keep expensive concurrency scenarios limited to the cases that actually require them.

Do not optimize the test suite by sacrificing test isolation or correctness.

### Unit vs Integration Test Responsibility

Do not duplicate every unit test as an integration test.

Use unit tests for:

- pure domain logic
- deterministic calculations
- validation rules
- scoring algorithms
- state-transition logic where appropriate

Use integration tests for:

- API + application + infrastructure behavior
- PostgreSQL persistence
- transactions
- constraints
- authentication pipeline
- authorization
- tenant isolation
- SignalR
- background workers
- real external infrastructure
- concurrency involving shared persistent state

Use load/soak/chaos testing for:

- throughput
- latency under load
- connection saturation
- failure recovery
- system behavior under sustained concurrency

Each test type should have a clear purpose.

### Test Code Conventions

Integration-test code must follow the project's existing coding conventions:

- Use explicit type declarations; do not use `var`.
- Use explicit constructor injection where test fixtures require dependencies.
- Do not use primary constructors.
- Use `private readonly` fields for injected dependencies.
- Use clean `using` directives instead of fully qualified type names.
- Enable nullable reference types.
- Use `async`/`await` for asynchronous operations.
- Keep methods small and focused.
- Avoid duplicated setup logic.
- Use meaningful names.
- Avoid magic values; define meaningful constants or test-specific configuration when appropriate.
- Keep test classes focused on one feature or behavior group.
- Do not introduce unnecessary inheritance or abstractions.

### Test Architecture Principle

The integration-test infrastructure should be simpler than the production architecture it verifies.

Do not build a second application inside the test project.

Prefer:

```text
Real Application
       +
Real PostgreSQL
       +
Real ASP.NET Core pipeline
       +
Minimal Test Infrastructure
```

The tests should provide confidence in the production system rather than creating a parallel implementation of it.

### Final Quality Check

Before finishing any integration-test change, verify:

- tests are deterministic;
- tests can run independently;
- no arbitrary sleeps are used;
- no production secrets are used;
- PostgreSQL behavior is tested using real PostgreSQL;
- concurrency tests are explicitly synchronized;
- database state is isolated;
- resources are disposed;
- assertions verify behavior and invariants;
- test helpers do not hide important behavior;
- no unnecessary abstractions were introduced;
- no production behavior was changed solely to make a test pass;
- build and test results were actually verified.

Before finishing, review the changed files for unintended edits, unused code, contract changes, security effects, and consistency with the existing project structure.

