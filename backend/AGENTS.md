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

- `Kahoot.slnx` contains four .NET 10 projects under `src/`.
- `Kahoot.Domain` holds entities and domain contracts. It has no project references.
- `Kahoot.Application` holds application contracts, vertical slice feature folders, MediatR command/query interfaces, results, and service registration. It references Domain.
- `Kahoot.Infrastructure` implements persistence. It references Application; its registration is in `DependencyInjection.cs`.
- `Kahoot.Api` is the HTTP entry point. It references Application and Infrastructure; `Program.cs` configures middleware, CORS, health checks, and endpoints.
- `test/` directory must remain completely empty, containing only the `.gitkeep` file.

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
- **Query Annotations:** Place a comment above every query that uses a notable performance, security, or MVCC mechanism:
  - `AsNoTracking()` — eliminates change-tracker overhead and snapshot memory for read-only queries.
  - `AnyAsync` / `SingleOrDefaultAsync` — short-circuits against a unique index; avoids full table scan.
  - `FOR UPDATE` / `GetUserForUpdateAsync` — acquires a pessimistic row-level lock; serializes concurrent writers and prevents TOCTOU races.
  - `FOR UPDATE SKIP LOCKED` — skips already-locked rows; enables non-blocking parallel batch processing across replicas without deadlocks.
  - `ExecuteUpdateAsync` / `ExecuteDeleteAsync` — single atomic SQL statement pushed to the database; no entity materialization or change-tracker allocation.
  - `Select(x => x.Field)` — column projection; reduces row fetch size, network payload, and allocations.
  - `RepeatableRead` / `Serializable` isolation — MVCC snapshot prevents phantom reads and non-repeatable reads; note the increased MVCC tuple churn and serialization failure risk.
  - `pg_advisory_xact_lock` — transaction-scoped advisory lock for cluster-wide singleton coordination (e.g., seeding, scheduled tasks) without a dedicated lock table.
  - `xmin` row version / `RowVersion` / OCC — optimistic concurrency; detects concurrent modification without holding a lock; prefer for high-throughput, low-contention paths.
  - CTE (`WITH ... AS MATERIALIZED`) — materializes the subquery result once; prevents the optimizer from inlining and re-evaluating it inside `DELETE`/`UPDATE`.
  - `LIMIT` / `Take()` — bounds result set size; prevents unbounded memory growth and lock escalation on large tables.
  - Keyset pagination (`WHERE id > @cursor ORDER BY id`) — O(log n) index seek per page; avoids `OFFSET` full scan degradation on deep pages.
  - Parameterized queries / `ExecuteSqlInterpolatedAsync` — EF Core translates interpolated strings into parameters; eliminates SQL injection at the query boundary.
  - Connection pool (`Npgsql`) — annotate when explicitly controlling pool size, `MinPoolSize`, or `MaxPoolSize` for throughput-sensitive paths.
  - `CancellationToken` propagation — ensures long-running database commands are cancelled on client disconnect or application shutdown, releasing server resources immediately.
  - Redis pub/sub (`ISubscriber.PublishAsync`) — fan-out to all replicas over a shared channel; annotate channel name and message contract so the reader understands the cluster-wide delivery scope.
  - Redis distributed lease (`SET NX PX`) — atomic `SET key value NX PX ttl`; only one replica wins the lock; others skip; prevents duplicated background work across pods.
  - SignalR Redis Backplane — all hub messages pass through Redis so any replica can push to any connected client; annotate when a `SendAsync` or group call relies on the backplane being healthy.
  - Post-commit broadcast pattern — `SaveChangesAsync` first, then fan-out; guarantees the database row is durable before any replica receives the realtime event; prevents phantom pushes on rollback.
  - Idempotency key / `JoinOperationId` SHA-256 — deduplicate retried client requests at the database boundary; a unique index on the key makes duplicate execution a no-op instead of a double-write.
  - N+1 query prevention (`Include` / `Join` / split query) — annotate when eager-loading a collection to explain why a single join or split query replaces N round-trips to the database.
  - `IMemoryCache` / `IDistributedCache` (Redis) — annotate the cache key, TTL, and invalidation strategy; note whether stale reads are acceptable or whether cache-aside with write-through is required.
  - Retry + exponential backoff (`Polly`) — annotate transient failure policies on network calls to external services; document max attempts, jitter, and which exceptions are considered transient.


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

## Testing constraints

- Do not create or add any test code, test projects, unit tests, integration tests, or end-to-end tests to the backend.
- The `backend/test/` directory must remain completely empty, containing only the `.gitkeep` file.

Before finishing, review the changed files for unintended edits, unused code, contract changes, security effects, and consistency with the existing project structure.

