# Kahoot Application Integration Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans, or superpowers:subagent-driven-development when that execution method is explicitly selected. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Handoff:** Implement one requested phase at a time. This document authorizes no automatic commits, dependency changes, deployment, production fixes or later-phase implementation. Gemini can follow the concrete tasks directly when Superpowers is unavailable.

**Goal:** Build a maintainable integration suite in `backend/test/Kahoot.Application.IntegrationTests` that demonstrates how actual Application slices behave with MediatR, validation, EF Core, PostgreSQL transactions and relevant Redis services.

**Architecture:** Invoke commands and queries through the real `ISender` in separate DI scopes. Use the real `AppDbContext`, migrations, audit interceptor, hashing, JWT generation, database idempotency service and Redis rate limiters. Replace only caller identity and external delivery/presence boundaries with explicit local collaborators; use real image storage in its dedicated phase. Assert results and committed state through fresh scopes/connections.

**Tech Stack:** Existing .NET 10; xUnit 2.9.3; Microsoft.NET.Test.Sdk 17.14.1; xunit.runner.visualstudio 3.1.4; Testcontainers.PostgreSql and Testcontainers.Redis 4.15.0; MediatR 14.2.0; FluentValidation 12.1.1; EF Core 10.0.12; Npgsql EF provider 10.0.3; existing StackExchange.Redis and ImageSharp dependencies. Preserve installed versions.

**Spec:** The user's request for detailed, phased Application integration testing with clean code and professional testing practices. Read [authentication](../../02-authentication.md), [account management](../../03-account-management.md), [quiz management](../../04-quiz-and-question-management.md), [images](../../05-image-management.md), [game lifecycle](../../06-game-lifecycle.md), [joining/lobby](../../07-joining-and-lobby.md), [gameplay](../../08-live-gameplay.md), [scoring](../../09-scoring-and-leaderboards.md), and the [verification/race matrix](../../14-verification-and-testing.md). Use requirements as intended contracts; inspect implementation before assigning an expected outcome.

**Inspection date:** 2026-10-01. Application integration tests currently have project scaffolding and a `Features` folder entry, with no authored tests. Unit suites exist and other agents are changing this checkout; reread changed source before implementation.

## Global constraints

- Planning only in the current task. Do not implement tests until the user requests execution or a phase.
- Read applicable `AGENTS.md`, `.wolf/OPENWOLF.md`, `.wolf/STATUS.md` and relevant cerebrum guidance. The historical prohibition on backend tests is overridden by the user's explicit testing request within its scope; do not rewrite repository instructions.
- Keep the existing project references to Application and Infrastructure. Do not reference Api, another test project, or introduce a new shared testing project.
- Keep the two existing Testcontainers package references and shared xUnit/VSTest configuration. Add no EF InMemory/SQLite provider, mocking framework, Respawn, TestServer, WebApplicationFactory, coverage package, fixture framework or new dependency.
- Exercise implemented slices, not invented controllers, repositories, publish states, maintenance endpoints or documented functionality that does not exist.
- Use production migration history through `Database.MigrateAsync`; never use `EnsureCreated`, hand-maintained replacement DDL or mock DbSets to establish the test schema.
- Do not modify production behavior, migrations, options defaults, environment files, auth policies, private visibility or assembly attributes under this plan. Public registrars and interfaces are sufficient.
- Use only newly created container resources and test-owned temporary directories. Do not use local development databases, shared Redis, real `.env.*`, production configuration or bind-mounted application data.
- Configure fresh test values in memory. Generate disposable database credentials and signing keys at runtime; never print connection strings, passwords, raw refresh/player tokens or JWTs in diagnostics.
- Do not suppress failing assertions, skip integration tests when Docker is unavailable, add test-level retries, or widen allowed outcomes to hide a defect. Infrastructure startup failure means the test bodies were not verified.
- Each request owns its scope, caller and DbContext. Never dispatch concurrent requests through one scope/DbContext or a shared mutable current-user singleton.
- Each test owns a fresh database and Redis prefix. No test depends on test ordering, IDs or entities from another test, reusable containers from an earlier run, global Redis flushes or an outer rollback transaction.
- Prefer explicit C# types, file-scoped namespaces, explicit constructors, meaningful names and the repository's `.editorconfig`. Avoid `var`, private reflection and speculative abstractions.
- Do not change CI or deployment infrastructure as part of these phases. The final phase supplies reproducible commands and a CI handoff; adding a CI job needs its own requested scope.

## What this suite proves

| Boundary | This project verifies | Coverage belongs elsewhere |
| --- | --- | --- |
| Application dispatch | Real MediatR handler discovery, validation behavior and Result/Error contracts | Controller mapping, model binding, routes and HTTP ProblemDetails: Api integration |
| Persistence | Actual SQL translation, transactions, feature-owned constraints, locks and committed aggregate state | Exhaustive mapping/migration compatibility and migration coordination: Infrastructure integration |
| Authentication slices | Persisted users, credential verification, token rotation/revocation and security-version changes | Cookie/CSRF transport, JWT request authentication and authorization middleware: Api integration |
| Game workflows | Ownership, snapshots, transitions, counters, command/answer idempotency, results and ranks | SignalR groups, delivery, reconnect sockets and Redis backplane: Infrastructure/Api integration |
| Redis usage | Real Application-path lobby/answer admission and server-derived rate keys | Exhaustive Lua/TTL/cluster behavior and actual presence leases: Infrastructure integration |
| Image upload | Real command, sanitization/storage, metadata persistence and compensation | HTTP multipart limits/download responses: Api integration; cleanup workers: Infrastructure integration |
| Side effects | Delivery requested after commit, event payloads and committed state when delivery fails | Actual delivery guarantees, crash recovery, latency, SLOs and multi-process failover |

Calling a handler directly can help diagnose a failing test; it is not the primary integration entry point. Most tests use `ISender.Send`. A bootstrap seeder test calls its registered `ISeeder` because no corresponding command exists.

## Current implementation inventory and gaps

Actual production targets are under `backend/src/Kahoot.Application/` unless otherwise stated.

| Source family | Implemented work to exercise | Important implementation detail |
| --- | --- | --- |
| `DependencyInjection.cs`, `Common/Behaviors/ValidationBehavior.cs` | Assembly-scanned MediatR handlers/validators and validation exceptions | Validation failures throw `FluentValidation.ValidationException`; they are not returned as failed Results |
| `Features/Auth/{Register,Login,Refresh,Logout,LogoutAll,ChangePassword}` | Account creation, credential checks, refresh families and credential mutation | Credential mutations serialize through `GetUserForUpdateAsync`; refresh grace is strictly less than 10 seconds |
| `Features/Auth/Bootstrap/SystemAdminSeeder.cs` | Disabled/enabled bootstrap and existing-role collision handling | Resolve `ISeeder`; do not expose the internal implementation |
| `Features/Quizzes/{CreateQuiz,UpdateQuiz,DeleteQuiz,GetQuizById,ListQuizzes}` | Owned metadata, reads, pagination and session deletion guards | Creation/read guards differ from mutation handlers; do not invent a uniform authorization rule |
| `Features/Quizzes/Questions/{AddQuestion,UpdateQuestion,DeleteQuestion}`, `ReorderQuestions` | Choice replacement, ordering and single-owner image references | Mutations lock the Host row; authoring question/choice order starts at zero |
| `Features/Games/CreateGame` | Deep snapshots and active-PIN collision retries | Current transaction uses default isolation plus a Host row lock; snapshot order starts at one |
| `Features/Games/{JoinGame,GetJoinInfo,GetGameParticipants}` | Seat allocation, recovery, nickname tombstones and bounded queries | Recovery lasts 15 minutes; same operation with an active socket returns an empty raw token |
| `Features/Games/{StartGame,EndQuestion,ShowLeaderboard,AdvanceQuestion,EndGame}` | State machine and database-backed command idempotency | StateVersion and CommandId are actual input fields; activation uses PostgreSQL `clock_timestamp()` |
| `Features/Games/SubmitAnswer`, `QuestionResultsMaterializer.cs`, `ParticipantRankMaterializer.cs` | Accepted answers, scoring persistence, automatic close and SQL ranks | Shared Host/game locks; atomic accepted-count increment; named unique-answer collision retry |
| `Features/Games/{RemoveParticipant,GetGame,GetGameReport}` | Removal/revocation, queries and finished reports | Removal keeps nickname/seat tombstones and excludes removed players from rankings |
| `Features/Admin/Users/*`, `Features/Admin/Administrators/*` | Revision-controlled status mutations and administration projections | Host suspension sets a durable TerminationPending flag; a worker performs later game termination |
| `Features/Images/UploadImage` | Sanitized file plus QuestionImage metadata | Known persistence rejection compensates the file; an uncertain exception retains it |

Record these distinctions before execution:

1. **Quiz stale-edit rejection is a gap:** `UpdateQuizCommand`, `UpdateQuestionCommand`, `ReorderQuestionsCommand` and other quiz mutation inputs have no expected revision. `QuizConfiguration` does not mark Revision as a concurrency token. Host-row serialization is implemented, but the stale-edit rejection promised by `QUIZ-RISK-002`, `QUIZ-TEST-008` and `RACE-QZ-01` is not demonstrable from this contract. Report it; do not add a Revision argument or treat sequential success as optimistic conflict enforcement.
2. **Snapshot isolation wording:** Current CreateGame uses `BeginTransactionAsync(cancellationToken)`, not an explicit RepeatableRead overload. Verify atomic snapshots through its actual shared Host mutation barrier; do not silently change isolation to match old comments or handoff notes.
3. **Authorization scope:** Admin User read handlers do not perform a role check themselves; some other handlers trust ICurrentUser, while mutations also validate the persisted Host. Test guards that exist and reserve HTTP role enforcement for Api integration. Synthetic principals do not prove JWT or middleware enforcement.
4. **Suspension phase boundary:** SuspendUser persists account revocation and TerminationPending, then issues a finalizer hint and eviction request. It does not itself finish games or revoke all player session tokens. Worker finalization is implemented in Infrastructure and is outside these handler tests.
5. **Post-commit cancellation differs:** Game broadcasts explicitly use CancellationToken.None. Auth/account eviction calls currently receive the caller token. Report their observable behavior separately; do not assume all post-commit side effects ignore request cancellation.
6. **Player expiry distinction:** SubmitAnswer checks token hash, participant/game ownership and RevokedAt, but its token query has no ExpiresAt predicate. EndGame stamps a 24-hour expiry. Do not claim expiry enforcement from that stamping or invent a reject assertion without tracking the applicable contract gap.
7. **Broader gaps:** Previous-key JWT rotation and startup DR reconciliation need separate implementation/evidence. They are not reasons to implement new production features in test setup.

## Clean-code design for the test harness

### Resource ownership and isolation

Use one xUnit collection fixture for this project, implementing **xUnit v2** `IAsyncLifetime` with `Task InitializeAsync()` and `Task DisposeAsync()`. It owns one PostgreSQL container, one Redis container and a Redis multiplexer. All suite classes join this collection with `DisableParallelization = true`; deliberate races still run separate requests concurrently inside a test. This controls Argon2 pressure and the real AddApplication registrar's global Mapster scanning without disabling production concurrency.

The Compose database image is currently `postgres:17`; Redis is `redis:7.4.11-alpine`. For PostgreSQL, choose an available immutable digest for the Compose major version during Phase 1 and commit that test image constant. Record the digest and server version. Do not invent a patch tag, use Testcontainers' PostgreSQL 15 default, or silently upgrade the engine. Resolve the Redis tag to a digest as well when available. Images are test configuration, not a dependency upgrade.

Testcontainers 4.15 provides image-taking builders; use those constructors rather than obsolete parameterless constructors. Let the modules assign ports/names and readiness strategies; use their generated connection strings. Keep the resource reaper enabled and container reuse off. See the exact [PostgreSqlBuilder](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.15.0/src/Testcontainers.PostgreSql/PostgreSqlBuilder.cs), [RedisBuilder](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.15.0/src/Testcontainers.Redis/RedisBuilder.cs), and [official lifecycle practices](https://dotnet.testcontainers.org/api/best_practices/).

Every test calls `await using ApplicationTestHarness harness = await fixture.CreateHarnessAsync(...)` and gets:

- A generated database name `kahoot_it_<32 lowercase hex characters>`, created through the fixture's administrative container connection outside a transaction.
- A unique ASCII Redis prefix `kahoot-it-<32 lowercase hex characters>`, bound to the actual RealtimeOptions used by both rate limiters.
- Fresh configuration, service provider, current-user instances, clocks, notification observations and other collaborators.
- Its own temporary directory only when the image phase requests real storage.

Derive the application connection string from the container string with `NpgsqlConnectionStringBuilder`, setting the generated Database, an identifiable ApplicationName, and explicit bounded pool values. Do not construct it by replacing substrings. Only generated database identifiers may be inserted into CREATE/DROP DATABASE statements; validate their exact format and quote identifiers using Npgsql's identifier quoting. Parameters remain mandatory for data values.

Use these test configuration decisions in ApplicationServices; all are local to the harness:

| Setting | Test value |
| --- | --- |
| ConnectionStrings:DefaultConnection | Derived container connection, generated database, Minimum Pool Size=0, Maximum Pool Size=20, acquisition Timeout=15 seconds; sensitive error detail disabled |
| Database:CommandTimeoutSeconds / MigrationCommandTimeoutSeconds | 30 / 120; EnableSensitiveDataLogging=false, EnableDetailedErrors=false |
| Jwt:Issuer / Audience / SigningKey / AccessTokenMinutes | Synthetic stable issuer/audience for this harness; runtime-generated Base64 key of at least 32 bytes; 15 minutes |
| RefreshToken:LifetimeDays / FamilyMaxLifetimeDays | 14 / 90 |
| BootstrapAdmin:Enabled | false, except explicit Phase 12 bootstrap scenarios |
| GameJoin:ClientBaseUrl | `https://client.kahoot.test` |
| Realtime:RedisConnectionString / ChannelPrefix | Fixture container string / unique harness prefix |
| ImageStorage | Production defaults; only IHostEnvironment.ContentRootPath changes to the owned root in Phase 13 |

Use named test constants for hang-protection budgets: container startup 180 seconds, database migration 120 seconds, ordinary request 30 seconds and cleanup 30 seconds. Gate arrival/release waits have a 5-second diagnostic budget; blocker polling has a 2-second budget so answer tests can release their blocker before the production 3-second lock timeout. Link caller cancellation with these budgets and report which stage timed out. They are harness limits, not production SLOs.

Apply migrations using a temporary provider built with the production persistence registrar, setting that bootstrap context's command timeout to the configured migration timeout; dispose it after migration. Build the request provider afterward so its NpgsqlDataSource discovers the newly created enum types from a fresh pool. This avoids stale pre-migration type metadata without changing production mappings. Verify no pending migrations remain.

Cleanup order: cancel/release all test gates; await request tasks; dispose request/read scopes and file streams; dispose the harness provider/data source; remove only its Redis-prefix keys; delete its owned temporary directory; drop only its generated database. The fixture disposes the multiplexer and containers last. Keep cleanup bounded and preserve the original test failure when cleanup also fails. Do not clear global pools, kill unrelated sessions or run global database/Redis reset commands.

Database-per-test migrations favor correctness and simple ownership over reset infrastructure. Measure the suite cost before considering a migration-template optimization or new dependency. Never replace migrations with EnsureCreated to save time. The [EF Core testing guidance](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy) explains why substituting a different provider cannot establish production database behavior.

### Dependency composition

`ApplicationServices` builds a plain ServiceCollection, not an ASP.NET host:

1. Add logging/options and an in-memory IConfiguration with generated settings.
2. Register TimeProvider.System by default and scoped TestCurrentUser. Auth boundary tests may explicitly select an adjustable clock for that harness.
3. Call the public `AddApplication`, `AddPersistence` and `AddSecurity` registrars.
4. Bind valid GameJoinOptions and RealtimeOptions from the in-memory configuration; use a synthetic HTTPS client origin and the harness Redis prefix.
5. Register the fixture-owned IConnectionMultiplexer instance, real PinGeneratorService and GameCommandIdempotencyService using their existing public interfaces and source lifetimes.
6. Register recording IGameNotificationService/ISocketEvictionService/ISuspensionFinalizerChannel, controlled IPlayerPresenceService and fail-on-use IImageStorageService. Resolve the real rate limiters registered by AddSecurity.
7. In Phase 13 only, provide a test IHostEnvironment rooted at the harness directory and use the public AddStorage registrar to replace the unused storage collaborator.
8. Build with ValidateScopes/ValidateOnBuild and dispose the provider asynchronously. Explicitly resolve required options in smoke tests; ValidateOnStart alone is not executed by a plain provider.

Do not call AddInfrastructure/AddRealtime or start IHostedService registrations here. Their workers, subscribers, hub/backplane and seed lifecycle would mutate fixture state or expand the boundary. AddPersistence's hosted migration registration can exist as a descriptor; migrate explicitly and do not resolve/start hosted services. Use actual public module registrations wherever available, and keep the few additional application collaborators in this single test composition file.

### Helper interfaces and responsibilities

The following are planned test APIs, not production abstractions. Keep them internal except xUnit-discovered fixture/collection classes. Add each helper only when a phase consumes it.

| Planned helper | Interface/responsibility |
| --- | --- |
| `ApplicationDependencyFixture` | `Task<ApplicationTestHarness> CreateHarnessAsync(Action<IServiceCollection>? configureServices = null, CancellationToken cancellationToken = default)`; owns containers, administrative connection access and Redis connection |
| `ApplicationTestHarness : IAsyncDisposable` | `Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, TestCaller caller, CancellationToken cancellationToken = default)`; one disposable request scope per call |
| `ApplicationTestHarness` | `Task<T> ReadDbAsync<T>(Func<AppDbContext, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default)`; fresh anonymous scope, no tracking, materialized results only |
| `ApplicationTestHarness` | `ApplicationRequestScope CreateRequestScope(TestCaller caller)`; the disposable scope exposes `ISender Sender`, `AppDbContext DbContext` and `IServiceProvider Services` for scoped collaborators; owns AsyncServiceScope |
| `TestCaller` | Immutable record `(Guid? UserId, string? Role, bool IsAuthenticated)` with Anonymous/Host/SystemAdmin factories |
| `TestCurrentUser` | Scoped implementation of ICurrentUser, initialized once before resolving handler/DbContext; never mutated during dispatch |
| `ApplicationServices` | Configuration and production-module composition above; no containers, seeding or assertions |
| `DatabaseSandbox` | Generated database creation/migration/drop; no feature fixtures or test assertions |
| `FeatureData` | Small named methods to persist valid users, quizzes, snapshot games or participant batches; returns IDs and deliberately needed fields |
| `RecordingGameNotificationService` | Implements all eight actual interface methods; records method, game/host/version, typed payloads and cancellation; optional awaited observation/failure callback |
| `RecordingSocketEvictionService`, `RecordingSuspensionFinalizerChannel` | Record actual requests; support specifically configured failure/cancellation observations; unused channel read/wait/try-read methods throw rather than simulating an unstarted worker |
| `ControlledPlayerPresenceService` | Explicit active-connection/count decisions and recorded evictions; no claim of real Redis presence/SignalR behavior |
| `FailOnUseImageStorageService` | All unused storage operations throw, so an unexpected call fails visibly |
| `AdjustableTimeProvider` | Explicit GetUtcNow/SetUtcNow/Advance support for auth/recovery boundaries; no global clock mutation or claim to control PostgreSQL/Redis TIME |
| `SequencePinGenerator` | Finite, explicit PIN sequence for collisions; fail if the sequence is unexpectedly exhausted |
| `Concurrency/{TransactionCommitGate,SaveChangesGate,PostgresLockObserver}` | Actual-transaction scheduling and blocker evidence; introduced only in Phase 14 |
| `Faults/CommitFailureInterceptor` | Throws a configured exception for a targeted context before transaction commit; introduced only in Phase 16 |
| `Images/{TemporaryImageStorage,TestHostEnvironment}` | Owned root and tiny real image input creation; introduced only in Phase 13 |

Do not create a generic repository, scenario DSL, configurable fake server, mega-builder, global ServiceProvider, universal mock registry, or large inheritance tree. Keep ordinary assertions in test classes. A shared assertion helper is justified only after meaningful repeated aggregate checks appear.

Recording collaborators are owned by one harness and use thread-safe observations for its deliberately concurrent requests. Register concrete recorders and their interface aliases so tests can access observations through a request scope. Caller state remains scoped. Apply configureServices last so a scenario can replace a clock/PIN service or append a targeted interceptor; it must not replace the real persistence/validation/handler boundary being tested.

### Test-writing standards

- Name tests `Operation_WhenCondition_ProducesObservableOutcome`. Each test arranges one meaningful scenario and invokes the relevant boundary.
- Assert Result success before reading Value; on failure assert exact Error.Code/type and absence of unwanted committed mutations.
- Successful mutation tests assert both the response and fresh persisted state. Never rely on tracked entities or ClearTrackedChanges as a substitute for a fresh verification context.
- Return materialized records/values from ReadDbAsync; never leak DbContext, IQueryable, EntityEntry or lazy navigation into a disposed scope.
- Seed through EF with real mappings; use the actual handler when its setup behavior is part of the scenario. Directly seed large valid populations when testing a capacity boundary, not 499 unrelated join commands.
- Keep direct seed names simple ASCII and supply explicit valid normalized values. Internal UsernameNormalization/PlayerNickname helpers need no new friend assembly for fixture setup; exercise their real output through Register/Join when normalization is the behavior being tested.
- Generate a valid, distinctive synthetic password for credentials and seed users; use the real hasher. Do not reuse a static secret or weaken Argon2 settings. Keep concurrent hashing bounded.
- Assert token format/uniqueness with boolean predicates and safe diagnostics; avoid assertions whose failure rendering prints raw tokens, JWTs or passwords. Do not dump whole credential-bearing responses.
- Account for audit interception: it overwrites CreatedAt/UpdatedAt on added auditable entities. For pagination ties, update persisted fixture timestamps explicitly after insertion using a test setup operation, with UTC microsecond precision.
- Use system time in game-flow tests. Auth/recovery tests can freeze an adjustable clock; clock_timestamp/Redis TIME remain actual server clocks. Use clear past/future deadline margins instead of waiting for real expiration.
- Keep pure validator/scoring matrices in Application unit tests. Here use a few cases that establish discovery, SQL/persistence effects and the interaction between components.
- Compare ordered sequences only when the production query supplies ordering. Compare membership otherwise. For random GUIDs/tokens assert format, uniqueness and relationships rather than exact values.
- Use bounded cancellation/timeouts for startup, dispatch, gates and cleanup. Timeouts are hang protection, not response-time or SLO assertions.
- Add `[Trait("Category", "Integration")]`, `[Trait("Feature", "...")]` and `[Trait("Phase", "NN")]` consistently. Do not introduce test-count quotas or coverage percentages as a substitute for scenario quality.

## Planned files

Paths below are relative to `backend/test/Kahoot.Application.IntegrationTests/`. Create only files consumed by the selected phase.

```text
TestSupport/
  ApplicationIntegrationCollection.cs
  ApplicationDependencyFixture.cs
  ApplicationTestHarness.cs
  ApplicationRequestScope.cs
  ApplicationServices.cs
  DatabaseSandbox.cs
  TestCaller.cs
  TestCurrentUser.cs
  FeatureData.cs
  AdjustableTimeProvider.cs
  SequencePinGenerator.cs
  RecordingGameNotificationService.cs
  RecordingSocketEvictionService.cs
  RecordingSuspensionFinalizerChannel.cs
  ControlledPlayerPresenceService.cs
  FailOnUseImageStorageService.cs
  Concurrency/TransactionCommitGate.cs
  Concurrency/SaveChangesGate.cs
  Concurrency/PostgresLockObserver.cs
  Faults/CommitFailureInterceptor.cs
  Images/TemporaryImageStorage.cs
  Images/TestHostEnvironment.cs
Features/
  Composition/ApplicationCompositionTests.cs
  Composition/ValidationPipelineTests.cs
  Auth/RegistrationTests.cs
  Auth/LoginTests.cs
  Auth/RefreshTests.cs
  Auth/LogoutTests.cs
  Auth/PasswordChangeTests.cs
  Auth/AuthConcurrencyTests.cs
  Quizzes/QuizMutationTests.cs
  Quizzes/QuizQueryTests.cs
  Quizzes/Questions/QuestionMutationTests.cs
  Quizzes/Questions/QuestionOrderingTests.cs
  Quizzes/Questions/QuestionImageReferenceTests.cs
  Quizzes/QuizSnapshotConcurrencyTests.cs
  Games/Creation/GameCreationTests.cs
  Games/Creation/GamePinCollisionTests.cs
  Games/Lobby/GameJoinTests.cs
  Games/Lobby/JoinRecoveryTests.cs
  Games/Lobby/LobbyQueryTests.cs
  Games/Lifecycle/GameTransitionTests.cs
  Games/Lifecycle/GameCommandReplayTests.cs
  Games/Gameplay/AnswerSubmissionTests.cs
  Games/Gameplay/AnswerAdmissionTests.cs
  Games/Gameplay/QuestionResultsTests.cs
  Games/Gameplay/LeaderboardAndReportTests.cs
  Games/Participants/ParticipantRemovalTests.cs
  Games/Concurrency/LobbyConcurrencyTests.cs
  Games/Concurrency/GameplayConcurrencyTests.cs
  Admin/HostAccountManagementTests.cs
  Admin/AdministratorManagementTests.cs
  Admin/AdministrationQueryTests.cs
  Admin/BootstrapSeederTests.cs
  Admin/AdministratorConcurrencyTests.cs
  Images/ImageUploadPersistenceTests.cs
  Images/ImageUploadCompensationTests.cs
  Reliability/TransactionRollbackTests.cs
  Reliability/CancellationTests.cs
  Reliability/PostCommitFailureTests.cs
```

Feature folders follow the implemented Application slices. A combined file may be split by one responsibility when it becomes difficult to review; do not create one file per trivial assertion or empty placeholder classes.

## Verification convention

From `backend/`, with .NET 10 and a running Docker Linux engine:

```bash
dotnet test test/Kahoot.Application.IntegrationTests/Kahoot.Application.IntegrationTests.csproj --configuration Release --filter 'Category=Integration&Phase=01'
dotnet test test/Kahoot.Application.IntegrationTests/Kahoot.Application.IntegrationTests.csproj --configuration Release --logger 'trx;LogFileName=application-integration.trx'
dotnet build Kahoot.slnx --configuration Release
dotnet format test/Kahoot.Application.IntegrationTests/Kahoot.Application.IntegrationTests.csproj --verify-no-changes
```

Replace Phase=01 with the selected phase. Each phase runs its focused tests and all completed Application integration tests. Do not run another project's integration tests until their own infrastructure exists. Once dependencies have been restored, `--no-restore` is appropriate for repeated checks.

In this WSL checkout, replace `dotnet` with `'/mnt/c/Program Files/dotnet/dotnet.exe'` when the Linux SDK is unavailable. Use the Docker endpoint available to the process that actually launches Testcontainers; Windows and WSL endpoint discovery can differ. Diagnose that environment rather than hardcoding a shared connection-string fallback.

Record baseline status/build failures before editing. If Docker/image pull/startup/migration fails, report the exact stage and redact secrets. Never describe a build, empty scaffold, skipped suite or container PING as passing feature integration tests.

## Phase 1 — Prove the dependency harness and resource isolation

**Read:** Application `DependencyInjection.cs`, `Common/Persistence/IAppDbContext.cs`; Infrastructure `ServiceCollectionExtension/{PersistenceInstaller,SecurityInstaller}.cs`, `Persistence/AppDbContext.cs`, `Persistence/Migrations/20260929135314_InitialSchema.cs`; existing test project/props and Compose image versions.

**Create:** Collection/fixture, harness/request scope, composition, sandbox, caller helpers, required recording/fail-on-use collaborators and `Features/Composition/ApplicationCompositionTests.cs`.

**Interfaces:** Produce the helper APIs specified above. No feature tests may depend on a shared mutable database or actor.

- [ ] Resolve and record immutable PostgreSQL 17/Redis image references; construct the 4.15 builders with those explicit images. Start containers with bounded cancellation and verify service readiness.
- [ ] Populate the exact in-memory settings/budgets above and document image digests in the fixture constants; do not load environment/application configuration.
- [ ] Implement container fixture cleanup even if only one container successfully starts.
- [ ] Implement fresh database creation, production migrations and post-migration provider reconstruction. Assert applied migration history includes the current baseline and pending migration list is empty.
- [ ] Make harness disposal idempotent so explicit cleanup verification and await-using cannot dispose a resource twice; partial initialization must clean only resources that were successfully acquired.
- [ ] Implement per-test service providers/Redis prefixes and scoped actor initialization. Keep fixture-owned Redis lifetime out of harness disposal.
- [ ] Implement `Composition_UsesProductionContextAndScopedRequestIdentity`: IAppDbContext is the scoped AppDbContext; contexts differ between request scopes; Host A/Host B/Anonymous identities do not leak.
- [ ] Implement `MigratedDatabase_RoundTripsNativeEnums`: persist valid User role/status and a valid Game status through the real model, then read them through a fresh context. This proves enum/type metadata setup, not exhaustive mapping coverage.
- [ ] Implement `SeparateHarnesses_DoNotShareDatabaseRowsOrRedisKeys`: create two harnesses, persist a valid user and set one prefixed Redis marker in A; B sees no corresponding data. Use actual prefixes and never FLUSHDB.
- [ ] Implement `Harness_DisposalRemovesOwnedResources`: explicitly dispose a harness and verify its database no longer exists and its prefixed marker is gone; the second harness still works.
- [ ] Run Phase 01, the project suite, Release build, scoped format and diff checks.

**Exit:** An actual migrated database request/read works; isolation and cleanup are demonstrated. No production edits or new packages are required.

## Phase 2 — Establish realistic setup and MediatR validation evidence

**Read:** `Common/Behaviors/ValidationBehavior.cs`, Register/CreateQuiz validators and Infrastructure `Persistence/AuditableEntityInterceptor.cs`. Inspect Domain entities required by seed helpers.

**Create:** Minimal `FeatureData.cs`, `Features/Composition/ValidationPipelineTests.cs`. Add helpers only for users and quizzes now; extend feature setup later.

- [ ] Implement small user setup methods using real EF and a real password hash. Accept explicit role/status only when the scenario needs them; return IDs and the generated credential separately from logs.
- [ ] Implement `Send_InvalidRegister_ThrowsValidationExceptionWithoutPersistingUser` using an invalid current contract, such as a too-short password. Assert expected property failures and zero users/refresh tokens.
- [ ] Implement `Send_InvalidCreateQuiz_ThrowsBeforePersistence`: authenticated seeded Host, empty title, no quiz inserted and no sender bypass.
- [ ] Implement `Send_ValidCreateQuiz_PersistsAuditedOwnership`: real pipeline returns Revision=1; fresh row has HostAccountId, CreatedBy/UpdatedBy and UTC audit fields from the request scope.
- [ ] Implement `SequentialRequests_UseFreshTrackingState`: change quiz metadata through a later request and observe it through a fresh read, proving the harness does not retain a stale identity map.
- [ ] Keep helpers free of validation assertions, broad exception conversion and automatic fixture-wide state reset.
- [ ] Run Phase 02 and completed suite/build/format checks.

**Exit:** Tests verify production registration and pipeline invocation. Detailed pure validation rules remain unit-test responsibilities.

## Phase 3 — Integrate registration and login with real cryptography/persistence

**Read:** `Features/Auth/Register/{RegisterCommand,RegisterCommandHandler}.cs`, Login equivalents, UsernameNormalization, AuthErrors, User/RefreshToken mappings and the real PasswordHasher/JwtTokenGenerator.

**Create:** `Features/Auth/RegistrationTests.cs`, `LoginTests.cs`.

| Scenario/test | Exact evidence |
| --- | --- |
| `Register_NewAccount_PersistsActiveHostWithAuditedDefaults` | Result ID matches one persisted Host; display/normalized names follow actual normalization; Status=Active, Revision=1, TokenSecurityVersion=1, TerminationPending=false; actual hash verifies the supplied password |
| `Register_NormalizedDuplicate_ReturnsUsernameUnavailable` | Existing equivalent username, including a deliberate supported normalization variant => Auth.UsernameUnavailable; no second user or refresh token |
| `Register_UsernameHeldByAdministrator_IsUnavailable` | Username uniqueness crosses roles; no Host shadow identity |
| `Login_ValidCredentials_PersistsHashedRefreshFamily` | Response UserId/name/role match row; nonempty JWT and raw refresh token; persisted SHA-256 token hash matches raw secret; FamilyCreatedAt/CreatedAt/expiry follow options; secrets are never logged |
| `Login_Twice_CreatesIndependentFamilies` | Two valid logins for one user produce distinct family IDs and token hashes; both remain unrevoked |
| `Login_UnknownWrongPasswordOrSuspended_ConcealsAccountState` | Separate tests for each condition => Auth.InvalidCredentials and no new refresh rows; do not infer statistical timing equality |
| `Login_AtIpCap_RejectsBeforeCreatingTokens` | Consume the real limiter's 30 in-process admissions with no DB side effect; next Login => Request.RateLimited. Avoid an expensive repeated Argon2/backoff loop |

- [ ] Write the scenarios above through ISender and verify fresh persisted state.
- [ ] Verify issued JWT signature with the generated harness key and assert current sub/role/token_security_version/user relationships; do not claim Api lifetime/revocation enforcement from parsing a token.
- [ ] Keep username backoff, hashing saturation and timing indistinguishability/load tests out of this phase; unit/Infrastructure/load checks own those.
- [ ] Run Phase 03 and completed suite/build/format checks.

**Exit:** Registration/login are verified across their actual persistence and cryptographic dependencies. Concurrent duplicate insertion is deferred to Phase 14.

## Phase 4 — Verify refresh families, logout and password changes

**Read:** `Features/Auth/{Refresh,Logout,LogoutAll,ChangePassword}/*Command*.cs`, RefreshTokenOptions, RefreshToken mapping and eviction interface.

**Create:** `Features/Auth/{RefreshTests,LogoutTests,PasswordChangeTests}.cs`; `AdjustableTimeProvider.cs` only for explicit time-boundary cases.

| Scenario/test | Exact evidence |
| --- | --- |
| `Refresh_ValidToken_RotatesWithinSameFamily` | Original RotatedAt set; one replacement hash and raw token; original family ID and FamilyCreatedAt retained; user/version unchanged |
| `Refresh_RotatedTokenBeforeTenSeconds_ReturnsRefreshRace` | Freeze clock at RotatedAt+9 seconds => Auth.RefreshRace; no additional token/version/revocation/eviction |
| `Refresh_RotatedTokenAtTenSeconds_RevokesOnlyItsFamily` | Freeze at exactly RotatedAt+10 seconds => Auth.RefreshTokenReuse; every unrevoked token in that family revoked, security version +1, one eviction; another family remains unrevoked |
| `Refresh_ExpiredOrFamilyLifetimeReached_IsInvalid` | Exact expiry/max-family-lifetime boundaries => Auth.InvalidRefreshToken; no replacement token |
| `Refresh_UnknownRevokedOrSuspendedUser_IsInvalid` | Distinct inputs => Auth.InvalidRefreshToken under current source; no invented Auth.InvalidCredentials expectation from a conflicting race-table entry |
| `Logout_NullUnknownOrAlreadyRevokedToken_IsIdempotent` | Success, no unrelated family/user changes |
| `Logout_OldRotatedToken_RevokesItsReplacementFamily` | Present original raw token after rotation; whole same family revoked, other family unaffected |
| `LogoutAll_ActiveUser_RevokesAllFamiliesAndIncrementsSecurityVersion` | All its unrevoked refresh rows revoked, user version +1 and audit time updated; current source does not increment Revision; one recorded eviction |
| `ChangePassword_ValidCurrentPassword_CommitsHashAndRevocation` | New password verifies, old fails; version +1, Revision +1, all user families revoked; recorded eviction after commit |
| `ChangePassword_WrongCurrentPassword_DoesNotMutate` | Auth.InvalidCredentials; original hash/version/revision/token state retained |

- [ ] Use the adjustable clock without sleeping through grace/lifetime periods; align expected persisted timestamps to PostgreSQL microseconds.
- [ ] Implement anonymous/inactive current-user rejection for LogoutAll/ChangePassword using their actual Auth.Unauthorized branch.
- [ ] Observe the target user/token state through a separate context from inside an awaited eviction callback to establish that it is committed before eviction.
- [ ] Keep one request scope per operation; avoid an ambient test transaction around handler-owned transactions.
- [ ] Run Phase 04 and completed suite/build/format checks.

**Exit:** Persisted family semantics and credential revocation are demonstrated. Cookie transport and actual socket closure remain outside this suite.

## Phase 5 — Verify owned quiz metadata, deletion and queries

**Read:** `Features/Quizzes/{CreateQuiz,UpdateQuiz,DeleteQuiz,GetQuizById,ListQuizzes}/*Command*.cs` and `*Query*.cs`, QuizErrors and QuizConfiguration.

**Create:** `Features/Quizzes/{QuizMutationTests,QuizQueryTests}.cs`.

- [ ] Implement creation trimming/null description, Revision=1 and actor-owned audit evidence using real saved rows.
- [ ] Implement UpdateQuiz success: metadata changed, Revision +1, CreatedAt/CreatedBy preserved, UpdatedBy correct; response question count matches persisted questions.
- [ ] For owned reads/mutations, test Host B accessing Host A's ID => Quiz.NotFound and no mutation. Test anonymous caller against actual guards. Mutation handlers also reject a persisted inactive/non-Host actor.
- [ ] Implement `DeleteQuiz_WithoutSessions_CascadesQuestionsAndChoices` using actual related data; image metadata remains and receives an orphan timestamp where applicable.
- [ ] Implement active game => Quiz.InUse and finished-session-only => Quiz.HasSessions. Both preserve the full quiz graph.
- [ ] Implement detail reads with ordered questions/choices and image URL, proving production batch queries materialize correct ownership.
- [ ] Implement list pagination with PageSize=2 over five owned quizzes, deliberate identical CreatedAt values and explicit GUID tie cases; assert descending timestamp/ID traversal, no duplicates/omissions, HasMore/NextCursor and no foreign quizzes.
- [ ] Let actual ListQuizzes validator reject malformed cursors. Do not invoke its handler directly to turn an invalid cursor into a successful first page.
- [ ] Report the absent expected-revision/EF concurrency contract; do not create fictitious stale-revision commands.
- [ ] Run Phase 05 and completed suite/build/format checks.

**Exit:** Actual ownership, query translation and delete guards are covered. Serialized simultaneous edits are not reported as optimistic concurrency enforcement.

## Phase 6 — Verify question mutations, ordering and image references

**Read:** `Features/Quizzes/Questions/{AddQuestion,UpdateQuestion,DeleteQuestion}/*Command*.cs`, `ReorderQuestions/*Command*.cs`, Question/Choice/QuestionImage mappings.

**Create:** `Features/Quizzes/Questions/{QuestionMutationTests,QuestionOrderingTests,QuestionImageReferenceTests}.cs`.

| Scenario/test | Persisted assertion |
| --- | --- |
| `AddQuestion_PersistsOwnedOrderedChoicesAndIncrementsQuizRevision` | Question appended at zero-based count; trimmed text/choices, flags, duration/base points; tenant keys and revision correct |
| `AddQuestion_AtTwoHundredQuestions_RejectsWithoutPartialRows` | Valid 200-question setup => Quiz.QuestionLimitExceeded; existing graph/revision unchanged |
| `UpdateQuestion_ReplacesChoicesAtomically` | Old choice IDs gone, replacements have fresh IDs and contiguous order; question identity/order retained; quiz revision increments |
| `DeleteQuestion_CompactsMiddleOrdering` | Delete middle of three; dependent choices removed; surviving order is 0,1 and no negative staging index remains; revision increments |
| `ReorderQuestions_ReversesExistingSetWithoutUniqueCollision` | Full reversed set commits against real unique order index; exact new zero-based order and one revision increment |
| `ReorderQuestions_MissingDuplicateOrForeignIds_DoesNotMutate` | Nonnull invalid permutations => Quiz.QuestionSetMismatch; a null list/empty quiz ID is a pipeline validation exception; order/revision unchanged |
| `QuestionMutation_WithActiveSession_ReturnsQuizInUse` | Add/update/delete/reorder each reject; complete graph unchanged |
| `AttachImage_OwnUnusedImage_ClearsOrphanTimestamp` | Link retained and URL returned; UnreferencedSince=null |
| `AttachImage_AbsentForeignOrAlreadyAttachedImage_Rejects` | Quiz.InvalidImageReference; no choices/question/revision/image timestamp changed |
| `ReplaceOrRemoveImage_PreservesSnapshotReferencedImage` | Old image gets an orphan timestamp only when no snapshot still references it; finished-session snapshots remain intact |

- [ ] Implement real graph setup; these image-reference tests need metadata rows, not physical files.
- [ ] Include foreign quiz/question combinations so a caller cannot attach a same-owner question to another quiz's operation by ID alone.
- [ ] Check same-image updates preserve the original image relationship and produce the actual URL.
- [ ] Assert all rows after a rejected operation via a fresh context; rollback fault injection is deferred to Phase 16.
- [ ] Run Phase 06 and completed suite/build/format checks.

**Exit:** Negative-staging ordering and relational image ownership work against actual PostgreSQL constraints.

## Phase 7 — Verify game creation, snapshots and PIN collision recovery

**Read:** `Features/Games/CreateGame/*Command*.cs`, GameJoinOptions; Infrastructure PinGeneratorService, Game/GameQuestionSnapshot/GameChoiceSnapshot mappings and active-PIN index.

**Create:** `Features/Games/Creation/{GameCreationTests,GamePinCollisionTests}.cs`, `SequencePinGenerator.cs`.

- [ ] Implement `CreateGame_PopulatedOwnedQuiz_PersistsCompleteLobbySnapshot`: LOBBY, StateVersion=1, PresenceVersion=0, no current question, NextSeatNumber=1, zero reserved count; title/PIN/join URL correct; all snapshot text/image URLs/timing/points/correctness copied; snapshot orders start at one and snapshot IDs differ from source IDs.
- [ ] Implement empty quiz => Validation.Failed, foreign quiz => Quiz.NotFound, inactive/non-Host caller => Auth.Unauthorized. Assert zero partial games/snapshots on every failure.
- [ ] Prove snapshot independence after ending the created game and then editing the source quiz/questions: original game title, snapshot content, choice IDs and flags remain unchanged. Do not attempt an allowed source edit during an active game.
- [ ] Override only IPinGeneratorService with an explicit sequence: seed another Host's active game using PIN A; new creation tries A then B => one valid new graph with B and no duplicate snapshots.
- [ ] Supply A five times => Game.PinUnavailable, no new graph, existing active game unchanged. Assert the actual number of generator calls rather than test-level retries.
- [ ] Verify a finished game releases its PIN and an otherwise valid later game can acquire that value.
- [ ] Keep real production PinGenerator in ordinary tests. Randomness distribution belongs to unit/statistical testing.
- [ ] Run Phase 07 and completed suite/build/format checks.

**Exit:** Snapshots and SaveChanges collision recovery are exercised through real migrations/constraints. The Host lock race is deferred to Phase 14.

## Phase 8 — Verify lobby admission, join recovery and player queries

**Read:** `Features/Games/{JoinGame,GetJoinInfo,GetGameParticipants}/*Command*.cs`/`*Query*.cs`, PlayerNickname, Participant/ParticipantSessionToken mappings and the real LobbyJoinRateLimiter.

**Create:** `Features/Games/Lobby/{GameJoinTests,JoinRecoveryTests,LobbyQueryTests}.cs`.

| Scenario/test | Required evidence |
| --- | --- |
| `Join_NewPlayer_PersistsSeatAndHashedSessionToken` | Unique UUIDv4 JoinOperationId; normalized nickname, first seat=1, generation=1, 15-minute recovery; raw pst_ token length=68; SHA-256 bytes match one stored token; game counters/presence version change once |
| `Join_NormalizedNicknameCollision_IncludingTombstone_Rejects` | Game.NicknameTaken; no seat reuse/token/participant creation |
| `Join_AtCapacity_RejectsWithoutChangingCounters` | Seed 500 valid nonremoved participants => Game.Full; state and next seat unchanged |
| `Join_NonLobbyFinishedInvalidOrInactiveHost_Rejects` | Assert source-specific Game.NotJoinable/Game.InvalidPin, not a universal code |
| `Recover_SameOperationWithoutSocket_ReturnsSameParticipantAndNewToken` | Same normalized nickname/PIN within window; participant/seat/counters unchanged; another valid stored token hash created, prior token not silently assumed revoked |
| `Recover_WithActiveSocket_ReturnsEmptyTokenWithoutNewRows` | Controlled presence reports active; same participant/seat, empty raw token, no new token row |
| `Recover_ChangedNicknameOrDifferentGame_IsValidationFailure` | Validation.Failed; no mutation |
| `Recover_AtFifteenMinutesOrRemoved_DoesNotRestoreSeat` | At exact JoinRecoveryExpiresAt => Game.NotJoinable; removed participant => Game.NicknameTaken under actual guard order |
| `GetJoinInfo_ReturnsCapacityWithoutParticipantSecrets` | LOBBY title/count/500/full data; inactive Host uses Game.InvalidPin in this query |
| `GetParticipants_PagesBySeatWithOptionalRemovedRows` | Default limit=100; explicit small page; IncludeRemoved, integer next cursor and seat ordering; ownership enforced, no raw token/hash fields in response |

- [ ] Use real Redis admission and unique harness prefixes for every ordinary Join.
- [ ] Prove one accepted join creates the expected prefixed lobby-rate state. Full limiter refill/TTL timing remains Infrastructure integration coverage.
- [ ] In a notification observation callback, read committed participant/token/counter state from another context; assert post-commit cancellation is None.
- [ ] Force a presence/notification callback failure after commit; Join still succeeds and the participant/token remain durable, following the current catch branch.
- [ ] Run Phase 08 and completed suite/build/format checks.

**Exit:** Real seat persistence/recovery are verified. Controlled presence does not establish actual socket ownership, Redis leases or reconnect behavior.

## Phase 9 — Verify lifecycle transitions and command replay

**Read:** `Features/Games/{StartGame,EndQuestion,ShowLeaderboard,AdvanceQuestion,EndGame}/*Command*.cs`, models, materializers and Infrastructure `Services/GameCommandIdempotencyService.cs`.

**Create:** `Features/Games/Lifecycle/{GameTransitionTests,GameCommandReplayTests}.cs`.

- [ ] Follow an actual two-question workflow: LOBBY -> QUESTION_ACTIVE -> QUESTION_RESULTS -> LEADERBOARD -> QUESTION_ACTIVE -> QUESTION_RESULTS -> FINISHED. Assert current index, state strings, StateVersion increments and persisted question bounds at every step.
- [ ] For Start/Advance, bracket execution with PostgreSQL clock reads and assert StartedAt falls within those server bounds and EndsAt-StartedAt equals DurationSeconds. A frozen application clock must not be asserted as the activation clock.
- [ ] Verify zero eligible participants do not cause premature auto-close; disconnected-but-not-removed participants are still eligible under the current count query.
- [ ] Test each command's legal source state, wrong state, stale ExpectedStateVersion, foreign game, inactive Host and expired Host grace according to actual branch order.
- [ ] Implement same CommandId+same request replay: same cached response, one idempotency row, one state change and no second notification, even after later state changes where its replay branch is reached.
- [ ] Implement same CommandId with altered ExpectedStateVersion or different command name => Validation.Failed; no mutation. A fresh CommandId with stale version => Game.ConcurrentModification.
- [ ] Assert EndGame materializes an active question, sets FinishedAt, clears PIN/grace, ranks survivors and stamps previously unbounded player token ExpiresAt to FinishedAt+24 hours; do not claim these tokens are revoked.
- [ ] Validate host versus player QuestionStarted payloads: player choices contain IDs/order/text and the producer does not include correct-choice flags/IDs; host payload contains its actual correctness data.
- [ ] Observe persisted game/idempotency rows through a fresh context during publication and assert CancellationToken.None for game notifications.
- [ ] Run Phase 09 and completed suite/build/format checks.

**Exit:** Actual state machine and database idempotency are demonstrated. Timer workers, live delivery and archived HTTP response codes remain outside this phase.

## Phase 10 — Verify answers, result materialization, ranks and reports

**Read:** `Features/Games/SubmitAnswer/*Command*.cs`, QuestionResultsMaterializer, ParticipantRankMaterializer, ScoringEngine, GetGame/GetGameReport handlers and AnswerSubmission/AnswerSubmissionChoice mappings.

**Create:** `Features/Games/Gameplay/{AnswerSubmissionTests,AnswerAdmissionTests,QuestionResultsTests,LeaderboardAndReportTests}.cs`.

**Actual command:** `SubmitAnswerCommand(Guid GameId, Guid ParticipantId, Guid QuestionId, List<Guid> ChoiceIds, string ConnectionId, byte[]? SessionTokenHash, long? ConnectionGeneration)`.

| Scenario/test | Required evidence |
| --- | --- |
| `Submit_ValidTokenHash_PersistsOneAnswerAndDistinctSelections` | Result Accepted=true, AlreadyAnswered=false; correct relationships, server SubmittedAt, distinct selections, score and accepted count; no raw token persisted |
| `Submit_ValidGeneration_UsesCurrentParticipantBinding` | SessionTokenHash=null plus current generation passes; stale generation => Game.InvalidSessionToken; both session hash and generation absent => pipeline ValidationException |
| `Submit_ExactCorrectSet_GradesAndPersistsScore` | Single/multiple correct cases; partial/superset => IsCorrect=false/points=0; duplicates are deduplicated before persistence when raw list is within six items |
| `Submit_UnknownOrForeignChoiceOrQuestion_Rejects` | Game.InvalidChoices/Game.NotCurrentQuestion as implemented; no answer, selections, score or accepted-count change |
| `Submit_RevokedTokenWrongParticipantOrRemovedParticipant_Rejects` | Actual code/precedence, including Game.ParticipantRemoved before token validation for a known removed participant |
| `Submit_AfterDeadline_RejectsWithoutClosingQuestion` | Set valid StartedAt/EndsAt in the past with setup SQL/EF; Game.AnswerTooLate, question remains QUESTION_ACTIVE and graph unchanged |
| `Submit_LastEligibleAnswer_MaterializesAndClosesExactlyOnce` | Accepted count equals effective eligibility; state -> QUESTION_RESULTS once, StateVersion +1, distributions/personal cards committed before one publication |
| `Submit_AlreadyCommittedAnswer_IsRecoverableReplay` | AlreadyAnswered=true; no new rows/points/counters; repeat after EndQuestion and after burning admission attempts still returns the committed answer where current guards allow |
| `EndQuestion_WithPartialAnswers_ProducesNonResponderCards` | Selection counts equal persisted distinct selections; nonresponders receive answered=false/zero points; removed players are excluded from personal cards |
| `Leaderboard_UsesDatabaseOrderingAndExcludesRemovedPlayers` | Fixture scores, normalized names including ASCII/non-ASCII ordering; SQL COLLATE C behavior, contiguous row_number ranks, top five and personal ranks agree |
| `EndGameAndReport_UseCommittedSnapshotAndAnswerAggregates` | Top three podium, finished report question order/counts, persisted score totals, surviving participants only; nonfinished reports fail Game.InvalidStateTransition and foreign reports fail Game.NotFound |

- [ ] Use real AnswerRateLimiter. For participant-attempt admission, send ten well-formed requests with nonexistent choice IDs, then a valid eleventh request => Game.TooManyAnswerAttempts without an answer. Use no connection ID to isolate the participant limit from the socket window.
- [ ] To verify server-derived rate keys, use ten different syntactically valid stale question IDs while one question is active; assert the single active-question Redis counter reaches ten and no per-client-question reset bypass succeeds.
- [ ] Do not require six slow database calls to fit a 3-second socket window. Verify socket exhaustion with server-time state arrangement in a dedicated integration case, or reserve exhaustive limiter timing for Infrastructure integration.
- [ ] Compute one scoring expectation independently from persisted SubmittedAt-StartedAt and the documented formula `round(basePoints * (1 - 0.5 * clampedElapsed/duration), AwayFromZero)`. Use small normal points/duration and ticks or decimal arithmetic, not millisecond-truncated ResponseTimeMs or a call to the same production scoring method. Pure extreme arithmetic matrices remain unit tests.
- [ ] Assert participant TotalScore equals the sum of its committed PointsAwarded across questions and AcceptedAnswerCount equals committed answer rows; these are separate integrity checks.
- [ ] Use broad safe future bounds for accepted-answer tests; exact database-deadline equality cannot be forced with an application fake clock. Report that precise boundary as remaining specialized evidence.
- [ ] Run Phase 10 and completed suite/build/format checks.

**Exit:** Persisted grading/aggregation and real admission behavior are verified. Performance/500-player timing and multi-replica delivery are not established.

## Phase 11 — Verify removal, token revocation and eligibility changes

**Read:** `Features/Games/RemoveParticipant/*Command*.cs`, materializers, participant/presence models.

**Create:** `Features/Games/Participants/ParticipantRemovalTests.cs`.

- [ ] In LOBBY, assert participant tombstone/time, all its unrevoked session tokens revoked, reserved count decremented, PresenceVersion +1; NextSeatNumber is not reused and nickname remains reserved.
- [ ] Repeated removal succeeds without another counter/version mutation but still requests committed eviction under the current implementation.
- [ ] During QUESTION_ACTIVE, removing an unanswered player reduces EffectiveEligibleParticipantCount; removing an answered player preserves its answer/score and does not decrement that eligibility value.
- [ ] Removing the remaining unanswered player when accepted==new effective eligible>0 closes the question once and produces survivor personal results. Removing to zero eligibility does not cause zero-equals-zero close.
- [ ] During LEADERBOARD, removed Rank becomes null, survivors are reranked and updated aggregate/personal events contain no removed player.
- [ ] Reject foreign game/participant and FINISHED mutations with actual errors and no changed graph.
- [ ] Force the recorded presence eviction to fail; tombstone/token revocation stay committed and current best-effort branch returns success. Assert game-side cancellation tokens are None.
- [ ] Run Phase 11 and completed suite/build/format checks.

**Exit:** Durable removal and eligibility/rank integrity are verified. Actual socket closure is an Infrastructure/Api concern.

## Phase 12 — Verify account administration and explicit bootstrap execution

**Read:** `Features/Admin/Users/*/*Command*.cs`, user query handlers, administrator equivalents, AccountErrors, `Features/Auth/Bootstrap/SystemAdminSeeder.cs`, BootstrapAdminOptions and `Common/Seeding/ISeeder.cs`.

**Create:** `Features/Admin/{HostAccountManagementTests,AdministratorManagementTests,AdministrationQueryTests,BootstrapSeederTests}.cs`.

Use these existing request types; user and administrator IDs have distinct parameter names:

| Actual dispatch request | Actual input |
| --- | --- |
| `CreateAdministratorCommand` | string Username, string Password |
| `ListAdministratorsQuery` | No arguments |
| `SuspendAdministratorCommand`, `ReactivateAdministratorCommand` | Guid AdministratorId, long Revision |
| `GetUserByIdQuery` | Guid AccountId |
| `ListUsersQuery` | string? Cursor=null, int PageSize=50, string? Username=null, UserStatus? Status=null |
| `SuspendUserCommand`, `ReactivateUserCommand` | Guid AccountId, long Revision |

- [ ] Suspend a valid Host with the current revision: status suspended, Revision/security version +1, all refresh families revoked, UpdatedBy admin; no unfinished games => TerminationPending=false/no hint, unfinished game => true/one hint.
- [ ] Assert unfinished games and player tokens are still present after this command; the finalizer hint is not completion evidence. Reactivation while TerminationPending => Account.TerminationPending with no mutation.
- [ ] Test stale revision => Account.ConcurrentModification, wrong role/missing target => Account.NotFound and non-admin caller => Auth.Forbidden where the handler checks it.
- [ ] Test documented current idempotent revision+1 status retry branches: repeated suspension/reactivation does not increment again or issue duplicate first-transition side effects.
- [ ] Reactivate an eligible suspended Host: status/revision/audit updated; previously revoked refresh tokens remain revoked and prior password remains unchanged.
- [ ] Create administrator with real hashing; uniqueness against either role => Account.Conflict. Suspend one of two active administrators atomically revokes its refresh families and preserves the other.
- [ ] Attempt to suspend the sole active administrator => Account.LastAdministrator with no mutation. Do not seed only the target when testing successful suspension.
- [ ] Implement admin lists/get-user projections, Host-only filters, normalized prefix/status filtering and cursor traversal with timestamp/GUID ties; assert secrets absent from DTO contracts. Do not claim these read handlers enforce HTTP admin authorization themselves.
- [ ] Bootstrap disabled => no administrator; enabled valid credentials => one; repeat => same account; username held by Host => InvalidOperationException and no takeover. Resolve the registered ISeeder with an explicit harness configuration; never start the seeding worker.
- [ ] Run Phase 12 and completed suite/build/format checks.

**Exit:** Phase-one account state and administration persistence are covered. Finalizer workers, old-JWT middleware revocation and socket SLA evidence remain separate.

## Phase 13 — Verify image upload across real storage and database boundaries

**Read:** `Features/Images/UploadImage/*Command*.cs`, ImageErrors, QuestionImage mapping; Infrastructure `Storage/ImageStorageService.cs`, `ServiceCollectionExtension/StorageInstaller.cs` and ImageStorageOptions.

**Create:** `Features/Images/{ImageUploadPersistenceTests,ImageUploadCompensationTests}.cs` and the image support files.

- [ ] Create a test-owned root and IHostEnvironment; use actual AddStorage, directories and storage reserve rules. Do not disable the 10% reserve, lower validation limits or write into backend/src/Kahoot.Api/wwwroot.
- [ ] Generate a tiny valid PNG/JPEG using existing ImageSharp, open the upload stream and send UploadImageCommand; assert `/uploads/{id}.extension`, actual sanitized file/size/dimensions/type and one correctly owned QuestionImage row with UnreferencedSince set.
- [ ] Validate one real metadata-bearing image is re-encoded with metadata removed. Keep the exhaustive malicious-format/decompression/large-file matrix in Infrastructure/Api integration coverage.
- [ ] Unsupported/malformed payload => exact Image error, no metadata row or final file; staging files cleaned. Caller stream ownership remains with the test and is disposed explicitly.
- [ ] For definite persistence rejection, use a nonexisting but nonnull caller ID: valid sanitization succeeds, real Host foreign key rejects metadata insertion, translated ForeignKeyConstraintViolationException propagates and the new final file is compensated. Assert no row/final file remains.
- [ ] Verify the uncertain-error branch with a narrowly configured SaveChanges exception after real sanitization: the handler retains the file and propagates the original exception. Label this injected branch coverage; it does not prove a network failure after an actual ambiguous commit. Track this file until owned fixture cleanup.
- [ ] Dispose streams/providers before recursively deleting the exact generated root; preserve diagnostic context if cleanup fails.
- [ ] Run Phase 13 and completed suite/build/format checks on an environment satisfying the real disk reserve.

**Exit:** Cross-resource persistence/compensation are demonstrated. Crash reconciliation, cleanup races, HTTP streaming and disk fault/SLO profiles remain separate.

## Phase 14 — Deterministically verify auth, quiz/snapshot and admin races

**Read:** Relevant handlers from Phases 3–7/12, their transaction boundaries, the [verification matrix](../../14-verification-and-testing.md) race IDs, and current PostgreSQL locking behavior.

**Create:** Concurrency support plus `Features/Auth/AuthConcurrencyTests.cs`, `Quizzes/QuizSnapshotConcurrencyTests.cs`, `Admin/AdministratorConcurrencyTests.cs`.

### Coordination contract

Use test-only EF transaction/save interception through additional test registrations, preserving the production context/provider/audit interceptor. Target a specific request context; do not pause every seed/read request. A TransactionCommitGate signals after the first operation has written and is about to commit, then awaits a release TaskCompletionSource with RunContinuationsAsynchronously. A SaveChangesGate is for registration's implicit save, where no handler-owned transaction exists.

Produce `Task Reached`, `void Release()` and bounded awaited completion. Construct each operation's separate ApplicationRequestScope before launch. Open its database connection and query `SELECT pg_backend_pid()` before dispatch to capture that backend's identity; do not issue another command on its DbContext while Send is active.

On an independent observer connection, wait for `pg_blocking_pids(secondPid)` to contain the first PID before releasing the first gate. Use bounded condition polling with cancellation and report observed blockers/activity on timeout. A short polling interval can yield between observations; fixed sleeps must never establish race ordering. PostgreSQL defines the blocker/PID evidence in its [session information documentation](https://www.postgresql.org/docs/17/functions-info.html).

Always release gates in finally, cancel requests on failure, await both tasks and dispose scopes before cleanup. Do not make CI wait for the handler's 3-second lock timeout merely to prove that a loser blocks. For registration, gate both saves after prechecks/hashing, release first and await commit, then release second; no blocker wait is needed before an insert has been attempted.

Freeze a shared adjustable clock for refresh-race requests so the scheduling test stays strictly within the 10-second grace independently of machine speed. Prepare both request connections/backend IDs before launching the gated operation. For duplicate registration, allow each real hash to finish before starting the next hash and keep the first request paused before persistence; this tests the database collision rather than Argon2 gate saturation.

| Controlled race | Required invariant/outcome |
| --- | --- |
| `ConcurrentRegistration_EquivalentUsernames_CommitOneAccount` | Both passed the initial existence lookup; one persisted account and one Auth.UsernameUnavailable; real named unique constraint is exercised; hashing is not allowed to saturate and obscure the race |
| `ConcurrentRefresh_SameToken_ReturnsOneReplacementAndOneRace` | First commit wins; other returns Auth.RefreshRace inside controlled grace; one replacement, family unrevoked, security version unchanged |
| `RefreshVsLogout_RespectsBothCommitOrders` | Refresh-first => replacement subsequently revoked; logout-first => Auth.InvalidRefreshToken; no active token in the logged-out family |
| `RefreshVsLogoutAllOrPasswordChange_LeavesNoActiveOldFamily` | Both controlled orders; appropriate current error, security/revision changes exact and no new surviving old-family refresh token |
| `LoginOrRefreshVsHostSuspension_ValidatesPersistedAccount` | Credential command first can return credentials later revoked; suspension first => current InvalidCredentials for Login / InvalidRefreshToken for Refresh; durable suspended account with no unrevoked refresh family |
| `QuizEditVsCreateGame_SerializesThroughHostRow` | Edit-first snapshot contains the whole committed edit; creation-first causes Quiz.InUse for edit; no torn title/question/choice graph |
| `QuizDeleteVsCreateGame_RespectsBothCommitOrders` | Delete-first => Quiz.NotFound/no game; create-first => Quiz.InUse while active; use HasSessions only for a finished session |
| `TwoAdministratorsSuspendingOneAnother_KeepAnActiveAdministrator` | Real ordered active-admin locks; exactly one suspension and other Account.LastAdministrator; at least one active admin, no double revocation/deadlock hidden by retries |

- [ ] Implement/prove the targeted gate and independent blocker observer with a real locked-row test before reusing it.
- [ ] Write separate tests for the two specified serial orders, with explicit first/second actor/context IDs and fresh final assertions.
- [ ] Implement the race scenarios above. Compare documented outcomes to actual errors explicitly; do not translate Result errors into hypothetical HTTP statuses here.
- [ ] For concurrent ordinary quiz edits, record current serialized successful revision increments and the documented stale-edit gap separately. Do not claim RACE-QZ-01's conflict contract is met.
- [ ] Run Phase 14 and completed suite/build/format checks. Repeat a focused concurrency class once as a diagnostic confidence check; repetition is not a retry that turns failure into success.

**Exit:** Real lock/constraint interleavings are observed with a controlled ordering, rather than inferred from Task.WhenAll completion or wall-clock duration.

## Phase 15 — Verify capacity, lifecycle and answer/removal races

**Read:** Join/Start/Advance/EndQuestion/SubmitAnswer/RemoveParticipant handlers, actual retry filters and named unique indexes. Reuse Phase 14 coordination; do not create another race framework.

**Create:** `Features/Games/Concurrency/{LobbyConcurrencyTests,GameplayConcurrencyTests}.cs`.

| Race/test | Exact integrity assertion |
| --- | --- |
| `ConcurrentJoins_ForLastSeat_AdmitOne` | Seed 499 valid nonremoved participants with correct counters; two distinct operations compete => one success/one Game.Full, count=500, one new token/seat, counters agree |
| `ConcurrentJoins_SameNickname_ReserveOneTombstoneName` | Two operations with equivalent normalized nicknames => one success/one Game.NicknameTaken, one seat/token |
| `ConcurrentJoinReplay_SameOperation_DoesNotAllocateTwoSeats` | One participant and counter increment; with controlled absence of socket, two successful recoverable responses may yield two valid token rows. Do not assert one token where source explicitly mints a recovery replacement |
| `JoinVsStart_RespectsBothCommitOrders` | Join-first participant counted in first-question eligibility; Start-first Join => Game.NotJoinable and no new seat |
| `ConcurrentAdvance_SameId_ReplaysOneTransition` | Begin in first QUESTION_RESULTS of a two-question game; real idempotency response identical; one state/version/idempotency row/event |
| `ConcurrentAdvance_DifferentIdsSameVersion_RejectsStaleLoser` | Same initial game state, distinct CommandIds and identical expected version; one advance, other Game.ConcurrentModification; exact version increment once |
| `ConcurrentDuplicateAnswer_ScoresOnce` | Same participant/question through separate requests; one answer row/distinct selections, score once and accepted count once; retry returns AlreadyAnswered=true; actual named collision retry may be observed without requiring a flaky log path |
| `ConcurrentDistinctAnswers_PreserveEveryScoreAndCount` | Eight valid eligible participants, separate scopes and distinct connection IDs; all accepted distinct answers persist; aggregate score/count invariant exact; last answer closes once; no throughput/SLO claim |
| `AnswerVsEndQuestion_RespectsBothCommitOrders` | Answer-first is included in results; close-first returns actual Game.InvalidStateTransition (or already-answered replay for a separate retry scenario); no post-close new answer |
| `AnswerVsRemoval_RespectsBothCommitOrders` | Answer-first remains in distributions/history after removal; removal-first => Game.ParticipantRemoved; survivor scores/ranks and token revocation correct |
| `LastAnswerVsEligibilityReduction_ClosesOnce` | Equality transition to QUESTION_RESULTS at most once; accepted/effective counts consistent, no double event/version change |

- [ ] Use controlled lock ordering where applicable; for simultaneous distinct-answer pressure use an explicit start barrier and assert final invariants, without pretending it selects an exact SQL interleaving.
- [ ] Keep generated participants valid and rate keys distinct; use the real Redis limiters rather than bypassing them to obtain green race tests.
- [ ] Expect actual producer retry logic only. Any exhausted retry, deadlock escaping the handler, duplicate score, leaked seat or impossible counter is a failure to report; do not add harness retries.
- [ ] Inspect committed database state and recorded publication count after all operations complete. Publication counts do not prove delivery to remote clients.
- [ ] Run Phase 15 and completed suite/build/format checks.

**Exit:** Capacity/idempotency/data-integrity races are verified on real PostgreSQL connections. This is one-process orchestration against shared dependencies, not a multi-process deployment or load test.

## Phase 16 — Verify rollback/cancellation and perform final quality review

**Read:** Handler commit/disposal and post-commit branches, actual interceptors, the repository's final-review guidance and verification release gates.

**Create:** Reliability test classes and narrowly targeted failure interceptor. No production testability refactor is planned.

| Reliability scenario | Required evidence |
| --- | --- |
| `UpdateQuestion_BeforeCommitFailure_RestoresDeletedAndInsertedChoices` | Inject after real transactional writes but before commit; original choices/question/revision restored, no pending negative/order artifacts |
| `StartGame_BeforeCommitFailure_RollsBackStateAndIdempotency` | Exception propagates; original lobby/timer/version/idempotency state retained and no notification |
| `Request_CancelledWhileWaitingForHeldHostLock_LeavesNoChanges` | Independent request is observed blocked, then its token is cancelled; cancellation propagates, graph unchanged and a later request succeeds after blocker release |
| `Request_CancelledBeforeCommit_RollsBackGraph` | Gate stops before commit; cancel token/release gate; operation does not leave partial state/event. Dispose the aborted scope before retrying with a new one |
| `GameBroadcast_RequestCancelledAfterCommit_UsesIndependentToken` | Publication callback observes committed state, cancels original caller token and verifies its received token is None; normal recorded delivery completes and committed result remains |
| `GameBroadcast_ThrowsAfterCommit_StateAndReplayRemainDurable` | Configure recording notification to throw in a handler without a local swallow branch (e.g. StartGame); exception propagates after commit; state/idempotency remain, same command retry returns cached response without a second publication |
| `AuthEviction_CallerCancellationAfterCommit_DoesNotUndoRevocation` | Recording eviction can throw on the caller token, matching current source; family/version state remains committed. Report the post-commit cancellation limitation rather than forcing success |

- [ ] Configure fault injection for a single chosen request context and exact stage; leave setup/observers unaffected. Injected failure demonstrates transaction behavior, not actual network/crash uncertainty.
- [ ] Confirm every test/helper file has a concrete responsibility/consumer and no duplicated unit matrices, unbounded waits, permissive mocks, hidden global state or broad helper exception swallowing.
- [ ] Confirm project references/packages and production files are unchanged. Review all new files and intentional test registrations; preserve unrelated changes.
- [ ] Run the full Application integration suite, Release solution build and scoped formatting. Collect TRX/test counts and list any failure before claiming completion.
- [ ] Reconcile the scenario list with the named docs/race IDs; report passed coverage, documented gaps, remaining specialized boundaries and unavailable evidence separately.
- [ ] Provide CI handoff: existing pipeline currently restores/builds/formats but does not run this project; propose the exact test command and Docker Linux prerequisite as a follow-up. Do not edit its workflow in this phase without explicit scope.
- [ ] Update OpenWolf status/learning records under current repository instructions. Do not create a README or commit automatically.

**Exit:** All implemented phases have actual executed results, clean-code review and documented limits. A failing requirement remains a failing requirement; the suite is not declared complete by deleting or skipping it.

## Remaining integration and release evidence

| Work | Owner/follow-up |
| --- | --- |
| Cookies, CSRF, trusted proxies, HTTP roles, JWT validation/revocation, controller error serialization | Kahoot.Api.IntegrationTests |
| SignalR reconnect/catch-up, actual presence/backplane/eviction, multi-process delivery and slow-client behavior | Infrastructure/Api integration and dedicated realtime scenarios |
| Refresh/image/suspension/abandonment workers, competing SKIP LOCKED sweeps, startup advisory migration lock | Kahoot.Infrastructure.IntegrationTests |
| Exhaustive provider mappings/constraints, enum migration upgrades, least-privilege database permissions and existing-database compatibility | Infrastructure integration/migration verification; the sandbox administrative role does not prove production permissions |
| Exact clock-boundary sampling, Redis refill/TTL/server-fault/cluster profiles, ambiguous commit/crash recovery | Specialized infrastructure/chaos scenarios; no fake-clock claim for server TIME |
| 500-player throughput, latency percentiles, 25,000 sockets, soak, DR/backup restore and SLOs | Dedicated load/operations verification from docs/14 |
| Quiz stale-edit detection, previous JWT signing-key rotation, DR admission workflow | Separately authorized production implementation gaps |

## Phase completion report

```text
Phase: <number/name>
Files added/modified: <exact paths>
Boundary executed: <ISender/seeder, real database/Redis/storage, controlled collaborators>
Behavior and data invariants demonstrated: <specific results>
Commands actually run: <commands, counts, outcomes>
Image digests/server versions: <Phase 1 evidence, changes if any>
Failures/gaps: <source/spec references; no silent production fixes>
Remaining evidence: <HTTP/transport/workers/multi-process/load boundaries>
Production/dependency changes: <none under this plan>
Next phase: <number; do not execute until requested>
```

## Ready-to-use implementation prompt

```text
Implement Phase <N> of docs/superpowers/plans/2026-10-01-application-integration-tests.md.
Read the global constraints, harness interfaces, current implementation gaps and selected phase.
Inspect current source and working-tree changes; preserve every unrelated agent/user change.
Use real MediatR, production EF/Npgsql mappings and migrations, real PostgreSQL and the specified
Redis/storage dependencies. Isolate each test with its own database/prefix and each request with
its own caller/scope/DbContext. Assert results and committed state through fresh reads.
Keep helpers focused, explicit and small. Do not add generic testing frameworks or dependencies.
Do not start the API/SignalR/hosted workers, change production behavior, fake queries, use shared
development services, suppress failures, add harness retries or implement later phases.
Use observable database blockers/gates for ordered races; no scheduling by arbitrary sleeps.
Report implementation gaps separately from unavailable runtime evidence.
Run focused tests, all completed Application integration tests, Release build and scoped format.
Do not commit automatically. Finish with the phase completion report in the plan.
```
