# Kahoot Infrastructure Integration Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans, or superpowers:subagent-driven-development when that execution method is explicitly selected. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Handoff:** Implement one requested phase at a time. Gemini can follow these tasks directly if Superpowers is unavailable. This document authorizes no automatic commits, dependency upgrades, production fixes, deployment changes, or implementation of later phases.

**Goal:** Build maintainable tests in `backend/test/Kahoot.Infrastructure.IntegrationTests` that verify the existing Infrastructure implementations against real PostgreSQL, Redis, image codecs, files, and hosted-service lifecycles.

**Architecture:** Exercise Infrastructure services through their actual interfaces and public lifecycle methods, with production EF configuration and migrations. Give every test its own database, Redis namespace, service providers, clocks, and temporary storage root. Use independent providers to represent replicas, separate connections to establish SQL ordering, and a local SignalR delivery recorder only where the transport is outside the assertion boundary.

**Tech Stack:** Existing .NET 10; xUnit 2.9.3; Microsoft.NET.Test.Sdk 17.14.1; xunit.runner.visualstudio 3.1.4; Testcontainers.PostgreSql and Testcontainers.Redis 4.15.0; EF Core 10.0.12; Npgsql EF provider 10.0.3; SignalR.StackExchangeRedis 10.0.12; ImageSharp 3.1.12. Preserve installed dependencies and their resolved transitive versions.

**Spec:** The user's request for a detailed phased Infrastructure integration-testing plan, continuing the clean-code approach in the [Application integration plan](2026-10-01-application-integration-tests.md). Read the repository's [authentication](../../02-authentication.md), [images](../../05-image-management.md), [game lifecycle](../../06-game-lifecycle.md), [joining/lobby](../../07-joining-and-lobby.md), [reconnection](../../11-reconnection.md), [worker operations](../../12-platform-operations-and-health.md), [architecture/deployment](../../13-architecture-and-deployment.md), and [verification matrix](../../14-verification-and-testing.md). These documents describe intended contracts; current source determines whether a contract is implemented.

**Inspection date:** 2026-10-01. This project currently contains its `.csproj` and folder declarations, with no authored integration tests. Infrastructure unit tests already exist. Other agents have edited this checkout; inspect changed source before executing a phase.

## Global constraints

- This deliverable is a plan. Write no integration tests or production changes during the planning task.
- Read applicable `AGENTS.md` and OpenWolf guidance. The user's explicit testing request supersedes the historical default prohibition on backend tests within this scope; do not rewrite repository instructions.
- Keep the existing single project reference to `Kahoot.Infrastructure`. Application contracts and Domain entities are available through its normal transitive references. Do not reference Api, another test project, or create a shared test project.
- Keep the two existing Testcontainers references and inherited xUnit/VSTest settings. Add no mocking library, SQLite/InMemory provider, Respawn, fake-time package, ASP.NET testing host, SignalR client package, or speculative fixture framework.
- Use actual `AppDbContext` mappings, the production audit interceptor, and `Database.MigrateAsync`. Do not use `EnsureCreated`, mock DbSets, or replacement schema scripts.
- Production behavior, migrations, public contracts, `.env.*`, security policy, and deployment configuration remain outside implementation scope. Report defects and specification gaps with evidence.
- The only planned production testability edit is adding `[assembly: InternalsVisibleTo("Kahoot.Infrastructure.IntegrationTests")]` beside the existing unit-test attribute in `backend/src/Kahoot.Infrastructure/AssemblyReference.cs`, when Phase 6 first needs typed access to internal hosted services. This enables lifecycle testing without making workers public or invoking private methods. No other visibility change is authorized by this plan.
- Use disposable containers and generated configuration. Never connect to development/production databases, shared Redis, application upload volumes, or real environment files.
- Generate disposable passwords and signing keys at runtime. Do not render connection strings, credentials, raw refresh/player tokens, JWTs, or secret-bearing assertion values.
- Do not skip tests because Docker is unavailable, suppress assertions, retry whole tests, or accept additional outcomes merely to obtain a green run. A fixture startup failure means assertions were not exercised.
- Every concurrent operation owns its own scope/DbContext and connection. Every simulated replica owns independent singleton state and Redis subscriptions.
- Prefer explicit C# types, file-scoped namespaces, explicit constructors, focused helpers and meaningful names. Follow `.editorconfig`; avoid `var`, private reflection, mutable shared callers, and generic test frameworks.
- Build helpers only when a phase needs them. Do not copy another test project's infrastructure wholesale or take a dependency on its helpers.
- Run checks for the requested phase, review the diff, and stop at that phase's gate. Do not start implementing documented missing functionality.

## Review focus

1. **A caller waits behind a real database lock:** cancellation must release its resources and leave no partial write; prove the wait using PostgreSQL rather than a scheduling delay. Phases 5 and 6.
2. **Two replicas handle the same work:** uniqueness, row/advisory locks, and durable pending state must prevent conflicting outcomes. Phases 3, 6, 13, 15–17.
3. **A Redis message is missed:** PostgreSQL validity sweeps must evict invalid committed local connections; successful Pub/Sub tests alone cannot establish recovery. Phases 11 and 12.
4. **An image remains in a historical snapshot:** cleanup must preserve its row and bytes even if its orphan timestamp is stale. Phases 4 and 15.
5. **A worker loses a dependency after starting:** retain durable state, observe failure/recovery, and stop cleanly without claiming an API readiness response was tested. Phase 18.

## Layer boundaries and evidence

| Boundary | Evidence produced here | Evidence owned elsewhere |
| --- | --- | --- |
| EF/PostgreSQL | Production migrations, enum/type round trips, constraints, auditing, transactions and lock helpers | Application validation, command workflows and public errors: Application integration |
| Redis rate limits | Actual Lua execution, atomic admission, key isolation, TTLs and time semantics | Endpoint policy and request identity extraction: Api/Application integration |
| Presence and eviction | Shared Redis leases, real Pub/Sub subscribers, local abort callbacks, durable validity sweeps | Real socket closure, protocol frames and client reconnect: Api integration |
| Storage | Real image decoding/re-encoding, metadata removal, filesystem persistence and cleanup | Multipart binding, public download headers/authorization: Api integration; command/DB compensation: Application integration |
| Hosted services | Selected real worker loops, candidate predicates, bounded batches, transactions and recovery | Full API startup/readiness/shutdown, ingress and SIGTERM behavior: Api/system tests |
| Multiple providers | Independent in-process replicas sharing the same PostgreSQL/Redis | Separate-process crashes, Redis Cluster, network partitions and throughput/SLOs: distributed/system verification |

`GameHub.JoinAsHost`, `JoinGame`, `Reconnect`, `SubmitAnswer`, and connection lifecycle methods exist in Infrastructure. They need a real SignalR endpoint, authentication pipeline, groups and clients for convincing protocol evidence. Their full coverage belongs in `Kahoot.Api.IntegrationTests`; do not turn this project into a second API host or claim a local `IHubContext` recorder establishes backplane delivery. `GameNotificationService`, `GameHubFilter`, `UnauthenticatedSocketGuard`, JWT generation, password hashing, PIN generation, and in-process login throttling already have unit coverage; do not reproduce their pure matrices here.

## Current implementation inventory and limitations

All paths below are relative to `backend/src/Kahoot.Infrastructure/`.

| Source | Current behavior to exercise |
| --- | --- |
| `Persistence/AppDbContext.cs`, `Persistence/Configurations/` | Fourteen entity sets, PostgreSQL enums, tenant-matched composite foreign keys, unique indexes, restrictive/cascading deletion, lock helpers, sync/async unique/FK exception translation |
| `Persistence/AuditableEntityInterceptor.cs` | SaveChanges stamps added/modified auditable entities; creation fields are protected on update; direct SQL and ExecuteUpdate bypass it |
| `Persistence/DatabaseMigrationService.cs` | Advisory key `0x4B41484F4F545F4D`; coordination transaction uses one connection, migrations use another; DDL lock timeout 5 seconds; retries only eligible transient failures before migration begins |
| `Persistence/DatabaseSeeder.cs` | Advisory key `0x4B41484F4F545F53`; invokes registered `ISeeder` instances sequentially while holding the coordination lock |
| `Services/GameCommandIdempotencyService.cs` | SHA-256 request bytes, `bytea` hash, `jsonb` cached response, `(GameId, CommandId)` key; RecordAsync adds an entity but does not save it |
| `Security/AnswerRateLimiter.cs` | Redis TIME; five socket admissions per rolling 3 seconds; ten participant/question attempts per 600-second counter; limited socket attempts still increment the participant counter |
| `Security/LobbyJoinRateLimiter.cs` | Redis TIME token bucket; capacity 1200, refill 60/second, 30-second idle TTL; SHA-256 IP key; blank IP becomes `unknown` |
| `Realtime/HostPresenceService.cs`, `PlayerPresenceService.cs` | Instance-qualified sorted-set members, 45-second lease scores, 10-minute key TTL, local registries, renewal/removal, player generation fencing |
| `Realtime/SocketEvictionService.cs`, both eviction subscribers | Prefix-scoped Redis Pub/Sub, host-wide abort, player kick/fence parsing; kick requests ParticipantRemoved delivery before abort |
| Both `PresenceHeartbeatWorker` classes | 10-second ticks renew leases, then read PostgreSQL to abort invalid local connections; player validity snapshots include only MarkCommitted connections |
| `Persistence/RefreshTokenCleanupWorker.cs` | First pass after 10-minute tick; seven-day evidence retention; batches 500, maximum 40/pass; SKIP LOCKED; 100ms yields; retries 10/30/60 seconds |
| `Storage/ImageStorageService.cs` | Quarantine, 5 MiB stream cap, magic/codec checks, first-frame decode, orientation, EXIF/IPTC/XMP stripping, canonical encoding, staged atomic rename, path resolution and compensation |
| `Persistence/QuestionImageCleanupWorker.cs` | First pass after configured tick; database-first orphan deletion, current/snapshot reference checks, SKIP LOCKED, staging sweep, bounded final-file reconciliation |
| `Persistence/SuspensionFinalizerWorker.cs` | Startup sweep and channel/timer wakeups; 50-host pages, ten games/batch; Host row SKIP LOCKED; persisted ranks/results; finalization uses suspended Host UpdatedAt |
| `Persistence/GameAbandonmentWorker.cs` | 5-second ticks; recover missing grace to clock+300 seconds; finish up to 50 expired games with SKIP LOCKED; check real Host presence; stamp unset player-token expiry +24h |
| `Services/CriticalWorkerFailureTracker.cs`, `DependencyInjection.cs` | Critical worker failure escalation at 15 minutes and recovery; full composition; fail-closed rejection of DR reconciliation startup flag |

Keep these distinctions visible in the implementation report:

1. **Pool defaults:** PersistenceInstaller tests for absent pool keywords with `NpgsqlConnectionStringBuilder.ContainsKey`. The builder recognizes supported keywords even when omitted from the input; intended defaults of minimum 10/maximum 80 are therefore not reliably selected. Explicitly bound test pools, record current behavior, and report the gap. Do not fix the registrar as incidental testing work.
2. **Migration scope:** The existing history is a fresh baseline, `20260929135314_InitialSchema`. A fresh database and repeated migration test do not prove an upgrade path for an older deployed schema. The outer advisory transaction does not contain the second connection's migration transaction.
3. **Seeding scope:** The coordinator's transaction owns the lock, not every seeder's writes. A failing later seeder does not imply all previous independently committed seeder changes roll back. Actual bootstrap business behavior belongs in Application tests.
4. **Critical health:** The tracker records continuous reported failures, not the age of all outstanding work. A skipped locked Host can leave pending work without throwing. Do not equate this with comprehensive backlog-age monitoring or `/health/ready` verification.
5. **Presence time:** Lease scores/pruning use the injected application TimeProvider; Redis key expiry uses Redis time. Rate-limit scripts use Redis TIME. PostgreSQL `clock_timestamp()` is a third clock. Altering one does not alter the others.
6. **Pub/Sub:** Redis Pub/Sub is not a durable queue. Subscriber startup acknowledgement and PostgreSQL fallback sweeps matter; successful publish does not prove a client received an event.
7. **Cancellation:** Rate limiters call ScriptEvaluateAsync before awaiting with the caller token. Cancelling the wait can leave applied Redis side effects. SocketEvictionService checks pre-cancellation but its publish is not awaited with that token and ordinary publish errors are logged/suppressed. Test these actual boundaries.
8. **Suspension expiry:** SuspensionFinalizerWorker finishes games but does not stamp ParticipantSessionTokens.ExpiresAt as GameAbandonmentWorker does. Finished-game reconnect has its own FinishedAt-based validation. Record the distinction; do not infer identical token lifecycle or silently add expiry logic.
9. **Storage contract:** GetPhysicalFilePath rejects traversal and accepts simple filenames under `/uploads/`; it does not validate GUIDs or an extension allowlist. Those stricter download guards are in Api. Safe alternate filenames/MIME can still be accepted based on actual image bytes; do not invent exact MIME/extension matching.
10. **Maintenance naming/cadence:** Operations documentation uses conceptual OrphanImageCleanupWorker/AbandonedGameFinalizer/SuspensionGameFinalizer names. The actual classes are QuestionImageCleanupWorker/GameAbandonmentWorker/SuspensionFinalizerWorker. Image cleanup defaults to ten minutes, whereas the operations overview says daily. Record this source/spec difference.
11. **Unimplemented operating evidence:** `DR_RECONCILIATION_ON_STARTUP=true` fails closed because an independent security ledger/reconciliation workflow is absent. JWT previous-key rotation, ingress redaction, real WebSocket 1001 shutdown frames, power-loss filesystem durability and load/chaos guarantees remain separate gaps or verification work.

## Harness design and clean-code rules

### Ownership and isolation

Use one project-local xUnit collection fixture implementing **xUnit v2** `IAsyncLifetime` with `Task InitializeAsync()` and `Task DisposeAsync()`. Set its collection definition `DisableParallelization = true`; deliberate concurrency remains inside individual tests. The fixture owns PostgreSQL and Redis containers plus an administrative Redis connection used only for setup/inspection/cleanup.

Compose currently uses `postgres:17` and `redis:7.4.11-alpine`. Resolve immutable image digests for those versions during Phase 1 and record the server versions. Do not invent a PostgreSQL patch tag, use an unreviewed `latest` image, or accept Testcontainers' different default PostgreSQL major. Testcontainers 4.15 supports `new PostgreSqlBuilder(image)` and `new RedisBuilder(image)`; its parameterless builders are obsolete. Use generated connection strings, mapped ports and hostname, normal module readiness, enabled resource reaping and disabled reuse. See the tagged [PostgreSQL builder](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.15.0/src/Testcontainers.PostgreSql/PostgreSqlBuilder.cs), [Redis builder](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.15.0/src/Testcontainers.Redis/RedisBuilder.cs), and [lifecycle practices](https://dotnet.testcontainers.org/api/best_practices/).

Every harness owns:

- A generated database `kahoot_infra_it_<32 lowercase hex characters>`; created outside a transaction through the container administrative connection.
- A Redis prefix `kahoot-infra-it-<32 lowercase hex characters>` shared only by replicas within that test.
- A generated temporary root with `wwwroot/uploads` and `wwwroot/uploads/staging`, used only when the phase touches storage.
- Its service providers, individual provider-owned Redis multiplexers, current-user scopes, observations, gates and running hosted services.

Use `NpgsqlConnectionStringBuilder` to derive each replica connection string. Set its generated database, identifiable ApplicationName, minimum pool 0, maximum pool 20, acquisition timeout 15 seconds and sensitive error detail off. Validate generated database names and quote their identifiers with Npgsql before CREATE/DROP. Parameterize all data values. Never use an outer rollback transaction as test isolation: services/workers open other connections and commit independently.

Apply migrations through a temporary provider using AddPersistence and the configured migration command timeout. Dispose that bootstrap provider/data source before building normal providers, to avoid carrying pre-migration enum metadata in a pool. Phase 6 intentionally exercises the actual startup migration coordinator against an empty database rather than bypassing it with this helper. Never repair a failed production startup test by silently replacing its data source mid-startup.

Set generated configuration in memory:

| Setting | Value |
| --- | --- |
| Database:CommandTimeoutSeconds / MigrationCommandTimeoutSeconds | 30 / 120; sensitive-data logging and detailed errors false |
| Realtime:RedisConnectionString / ChannelPrefix | Owned container connection / owned test prefix |
| Jwt | Synthetic issuer/audience; runtime-generated Base64 signing key of at least 32 bytes; AccessTokenMinutes=15 |
| RefreshToken | LifetimeDays=14; FamilyMaxLifetimeDays=90; these are valid fixture choices, not a claim about the documented family cap |
| BootstrapAdmin:Enabled | false |
| GameJoin:ClientBaseUrl | `https://client.kahoot.test` |
| ImageStorage | Production fixed limits/paths/retention; phases may lower only validated positive batch limits and alter the owned content root |
| DR_RECONCILIATION_ON_STARTUP | false except isolated fail-closed composition checks |

Use named budgets: container startup 180 seconds; migration 120 seconds; normal operation/worker stop 30 seconds; gate arrival 5 seconds; PostgreSQL blocking observation 2 seconds; cleanup 30 seconds. These prevent hangs and do not assert a production SLO. Poll only an observable condition with a bounded cancellation token; do not use fixed sleeps to choose a transaction order.

Cleanup order is explicit: release/cancel gates; stop and await selected workers/subscribers; observe their ExecuteTask failures; await outstanding operations; dispose scopes/readers/streams; dispose each provider and its data source/multiplexer; remove only keys matching this prefix; delete only the owned root; drop only this database. Fault environments restore/unpause dependencies in finally before subscriber shutdown, then dispose those dedicated containers. Dispose shared fixture resources last. No FLUSHDB/FLUSHALL, global pool clearing, unrelated-session termination, fixed container names or fixed ports. Preserve the original test exception when cleanup also fails, while reporting cleanup failure as additional evidence.

### Composition and interfaces

The standard provider uses real `AddInfrastructure(configuration)` without starting a host. Supply logging, IConfiguration, a scoped TestCurrentUser and a local IHostEnvironment. After the registrar, replace its TimeProvider registration with the harness-selected instance and its `IHubContext<GameHub>` with a provider-local RecordingHubContext. Keep production AppDbContext, auditing, security services, presence, idempotency, critical tracker, finalizer channel and GameNotificationService. The recorder implements only required Group/Clients delivery branches; unsupported branches throw. It observes requests at the transport boundary and makes no network-delivery claim.

Build with scope/build validation and explicitly resolve validated options. Do not resolve all IHostedService instances to select one: this instantiates unrelated workers. Typed `ActivatorUtilities.CreateInstance<TService>` constructs the selected actual worker with the provider's dependencies. The selected service is owned by HostedServiceSession and is stopped/disposed there. Phase 18 separately checks the full registration and Generic Host lifecycle, where every registered worker intentionally starts.

Keep these exact project-local helper interfaces small:

| Helper/file under `TestSupport/` | Interface and responsibility |
| --- | --- |
| `InfrastructureCollection.cs`, `InfrastructureFixture.cs` | Collection definition; `Task<InfrastructureHarness> CreateHarnessAsync(SchemaMode schemaMode = SchemaMode.Migrated, CancellationToken cancellationToken = default)` |
| `InfrastructureHarness.cs`, `SchemaMode.cs` | SchemaMode has Migrated/Empty; harness implements IAsyncDisposable and owns resources; `ServiceProvider CreateProvider(string replicaName, TimeProvider timeProvider, Action<IServiceCollection>? configureServices = null)` |
| `InfrastructureConfiguration.cs`, `TestHostEnvironment.cs` | Generated, non-secret-rendered in-memory settings and owned ContentRootPath; no production config loading |
| `TestCurrentUser.cs` | Implements actual ICurrentUser; one-time identity initialization per scope, anonymous by default; workers do not inherit a request's caller |
| `PersistenceData.cs` | Small named valid entity graphs such as `Task<GameData> SeedGameAsync(AppDbContext context, Guid hostAccountId, CancellationToken cancellationToken)`; GameData contains IDs, not live contexts/scopes |
| `RecordingHubContext.cs`, `RecordedDelivery.cs`, `RecordingLogger.cs` | Thread-safe snapshots/awaitable observations for delivery and selected structured event names; never log parameter values or payload bodies |
| `AsyncGate.cs`, `PostgresLockProbe.cs`, `SqlObservationInterceptor.cs` | Explicit gate arrival/release, bounded pg_blocking_pids observations, narrow provider-local EF command/transaction observations; always execute real SQL |
| `ControlledTimeProvider.cs` | `void SetUtcNow(DateTimeOffset value)` and `Task PulseAsync(TimeSpan scheduledDelay, CancellationToken cancellationToken)`; implements actual ITimer Change/Dispose semantics; pulses one scheduled callback without implicitly advancing UTC time |
| `HostedServiceSession.cs` | `Task<HostedServiceSession<TService>> StartAsync<TService>(IServiceProvider provider, CancellationToken cancellationToken) where TService : class, IHostedService`; owns selected service and awaited shutdown |
| `ImageSamples.cs`, `GatedReadStream.cs` | Small legitimate codec fixtures and a cancellation-aware read gate; source streams remain caller-owned |
| `FaultTestEnvironment.cs` | Creates/disposes dedicated containers/providers for dependency stop/restart tests; never stops the suite's shared containers |

Introduce the last helpers in the phase that first uses them. An implementation may split PersistenceData into focused media/token files when its responsibilities grow, without creating a generic entity builder. Test names describe outcomes; each test has clear arrange/act/assert sections and independently readable expected state. The EF [provider testing guidance](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy) supports exercising the production provider rather than substituting one.

### Reliable time and concurrency

ControlledTimeProvider separates UTC values from timer wakeups. Tests set retention/grace timestamps explicitly, wait until a production timer is registered, then pulse it; do not call private ExecuteCleanupPass/FinalizeHostBatch methods. A pulse waits for timer registration and invokes callbacks outside the helper's lock. Periodic callbacks remain available until disposed; one-shot delay registrations are consumed once. Test the minimal helper contracts for registration, Change, disposal and cancellation in Phase 12. It is a worker scheduler, not an alternative Redis/PostgreSQL clock.

In .NET 10, all of BackgroundService.ExecuteAsync runs in the background. StartAsync returning does not establish timer/subscription/initial-sweep readiness. Wait for explicit timer registration, a real subscriber's startup acknowledgement, a database effect, or a selected structured event. See the [.NET 10 lifecycle change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task).

SQL contention uses separate physical connections and explicit transactions. Identify PIDs through pg_backend_pid/connection-open observations and prove a blocker with pg_blocking_pids, filtered to this database and replica ApplicationName. Gate only the named command/transaction for this test. Release gates in finally; cancellation must release a gate as well. SKIP LOCKED tests hold eligible rows externally, observe the worker's real candidate query finish, and verify progress on unlocked rows. Transaction locks last until transaction completion; see [PostgreSQL 17 locking](https://www.postgresql.org/docs/17/explicit-locking.html).

Atomic Redis admission can be tested with concurrent service calls and final counter invariants without choosing a winner. Do not demand an exact 1200-admission lobby burst while real time replenishes tokens. Use Redis TIME to bound legitimate refill. Inspect production TTL ranges and allow elapsed execution time; shortening a test-owned TTL can establish expiry recovery, but label that manipulation separately from verification of the configured lifetime. [Redis TIME](https://redis.io/docs/latest/commands/time/) is server time; [Pub/Sub](https://redis.io/docs/latest/develop/pubsub/) has at-most-once delivery.

## Execution and verification convention

Run commands from `backend/`. On WSL without Linux dotnet, replace `dotnet` with `'/mnt/c/Program Files/dotnet/dotnet.exe'`. Require a working Docker daemon for container tests; Windows `docker.exe` is acceptable when it reaches the correct engine. Use the tools available in the execution environment rather than changing project requirements.

For each phase:

1. Read its exact production targets and current implementation; inspect local changes.
2. Write one small test group with its stated outcome, using the real dependency. A test against existing correct behavior may pass immediately; do not sabotage production to manufacture a red result.
3. Run the phase filter. Treat discovery/compiler/fixture failures separately from assertion failures.
4. If an implemented contract fails, keep the smallest useful regression and report the defect. Do not weaken it or fix production within this test-only task. Document unimplemented contracts without adding permanently skipped placeholder tests.
5. Review helpers and resource ownership, run focused formatting/diff checks, report evidence, and stop.

Common command: `dotnet test test/Kahoot.Infrastructure.IntegrationTests/Kahoot.Infrastructure.IntegrationTests.csproj -c Release --filter "<phase filter>"`. Each phase below specifies the replacement filter and its gate.

## Phase 1 — Isolated environment and migration smoke tests

**Files:** Create `TestSupport/{InfrastructureCollection,InfrastructureFixture,InfrastructureHarness,SchemaMode,InfrastructureConfiguration,TestHostEnvironment,TestCurrentUser,RecordingHubContext,RecordedDelivery}.cs` and `Persistence/EnvironmentSmokeTests.cs` beneath this test project. The small delivery recorder is needed by the standard provider from its first phase; Phase 11 extends its fault observations. No production edit.

**Consumes:** Existing project/packages, AddInfrastructure, AddPersistence, AppDbContext, DatabaseOptions, container builders. **Produces:** The fixture/harness/provider interfaces above; generated isolated configuration; cleanup ownership.

- [ ] Validate Docker connectivity and resolve/record the chosen engine image digests before allocating resources. Generate a disposable PostgreSQL password rather than committing one.
- [ ] Implement the fixture and harness. Use SchemaMode.Empty only for coordinator tests; all ordinary tests migrate with production configuration and dispose the bootstrap pool first.
- [ ] Implement `MigratedDatabase_HasExpectedBaselineAndNoPendingMigrations`: assert the actual baseline is applied once, the provider is Npgsql, and the database answers a real query.
- [ ] Implement `IndependentHarnesses_DoNotShareDatabaseOrRedisKeys`: create two harnesses, write a user and a marker in one, verify absence in the other, then dispose the first and verify the second still works.
- [ ] Implement `ProviderScopes_UseIndependentContextsAndScopedCurrentUsers`: IAppDbContext and AppDbContext alias in one scope; different scopes have different contexts/callers; providers have different presence singletons.
- [ ] Verify cleanup also runs if provider creation or migration fails; retain original diagnostics without printing connection strings.

**Filter/gate:** `FullyQualifiedName~EnvironmentSmokeTests`. All smoke assertions pass against owned PostgreSQL/Redis, and explicit disposal leaves no owned database/marker behind. A build alone is insufficient.

## Phase 2 — Persisted mappings, enums and auditing

**Files:** Create `TestSupport/PersistenceData.cs`, `Persistence/MappingRoundTripTests.cs`, `Persistence/AuditingPersistenceTests.cs`. Introduce ControlledTimeProvider's UTC support here; add its timer support in Phase 12.

**Targets:** AppDbContext, all fourteen configurations, AuditableEntityInterceptor and PersistenceInstaller. **Produces:** Valid graph seeding and fresh-scope read conventions.

- [ ] Seed/read each entity family through the configured context: User, RefreshToken, Quiz, QuestionImage, Question, Choice, Game, GameQuestionSnapshot, GameChoiceSnapshot, Participant, ParticipantSessionToken, AnswerSubmission, AnswerSubmissionChoice and GameCommandIdempotency. Assert representative IDs/ownership, nullable fields, byte arrays and non-default counters; avoid a reflection-driven property-copy test.
- [ ] Parameterize all actual UserRole/UserStatus/GameStatus values. Read their PostgreSQL enum labels through SQL and assert actual string labels, not integer storage.
- [ ] `BinaryHashesAndJsonResponse_RoundTripWithoutTextReinterpretation`: bytea survives unchanged; jsonb retains response properties semantically, not the original whitespace/property order.
- [ ] `AddedAuditableEntity_UsesClockAndActor`: persisted User/Quiz CreatedAt and UpdatedAt use fixed UTC, null actor fields acquire the scoped caller, explicitly supplied creation actors remain intact.
- [ ] `ModifiedAuditableEntity_PreservesCreationFields`: use another actor and UTC time; attempt to change CreatedAt/CreatedBy; verify original creation values and new update values through another context. Cover anonymous worker updates preserving an existing UpdatedBy.
- [ ] Exercise one synchronous SaveChanges path as well as async; show ExecuteUpdate/direct SQL do not invoke the audit interceptor. Do not expect a scalar revision/state version to be an EF optimistic concurrency token.

**Filter/gate:** `FullyQualifiedName~MappingRoundTripTests|FullyQualifiedName~AuditingPersistenceTests`. Assertions verify committed rows through fresh scopes and actual PostgreSQL types.

## Phase 3 — Unique constraints and provider exception translation

**Files:** Create `Persistence/UniqueConstraintTests.cs` and the needed `TestSupport/AsyncGate.cs`. **Targets:** Configurations, initial migration and AppDbContext sync/async SaveChanges overrides.

- [ ] Create focused constraint cases; each starts with a valid graph and changes only the constrained key. Assert UniqueConstraintViolationException, exact ConstraintName, inner PostgreSQL SQLSTATE 23505, and unchanged original row count.

| Constraint | Assertion |
| --- | --- |
| `ux_users_normalized_username` | Same normalized username conflicts across accounts |
| `ux_refresh_tokens_token_hash`, `ux_participant_session_tokens_token_hash` | Same binary hash conflicts; unrelated hashes work |
| `ux_games_active_pin` | Duplicate non-null PIN conflicts; multiple null PINs work; index is not a status-filtered index |
| `ux_questions_quiz_order`, `ux_choices_question_order` | Repeated order conflicts within its parent, works across different parents |
| `ux_game_questions_game_order`, `ux_game_choices_question_order` | Snapshot ordering has the same parent scope |
| `ux_participants_game_nickname`, `ux_participants_game_seat` | Duplicate normalized nickname/seat in a game fails; a removed participant still reserves its nickname |
| `ux_participants_join_operation` | The join hash is globally unique, including across different games |
| `ux_answer_submissions_once_per_question` | One submission per game/question/participant |
| `ux_question_images_storage_path` | Different image rows cannot own one storage path |
| `ix_questions_image_id_host_account_id` | At most one current question uses an image; multiple null image IDs are valid |
| `pk_game_command_idempotency`, `pk_answer_submission_choices` | Duplicate composite keys fail; a different game/choice remains distinct |

- [ ] `ConcurrentDuplicateUsername_StoresExactlyOneRow`: two scopes released from an arrival gate submit the same normalized name; exactly one save succeeds and one receives the translated unique violation. Which task wins is immaterial.
- [ ] Include a synchronous duplicate case to verify the actual sync override, without duplicating the whole matrix. Roll back/dispose failed transactions and contexts before verification or another operation.
- [ ] Assert oversized required string/not-null violations retain their actual PostgreSQL category rather than being translated into uniqueness/FK errors; do not assert localized database message text.

**Filter/gate:** `FullyQualifiedName~UniqueConstraintTests`. Duplicate/FK prerequisites are valid, so each failure identifies the intended database invariant.

## Phase 4 — Tenant foreign keys, ownership and deletion behavior

**Files:** Create `Persistence/TenantForeignKeyTests.cs`, `Persistence/DeleteBehaviorTests.cs`. **Targets:** The actual composite FK/principal keys and migration-generated constraint names, including PostgreSQL-truncated names.

- [ ] Build two valid Host graphs. For Quiz→Question→Choice, Game→snapshot/participant, session token→participant, answer→game/question/participant and answer-choice→submission/snapshot-choice, deliberately combine IDs from different owners/parents. Assert translated ForeignKeyConstraintViolationException with SQLSTATE 23503 and the exact migration/catalog constraint name.
- [ ] Test image owner mismatch for both current questions and snapshots. Attach the same image to two different current questions to verify the single-owner unique constraint; retain it in multiple matching historical snapshots successfully.
- [ ] Delete an otherwise unused Quiz and verify Question/Choice cascade. Delete a user with only refresh tokens and verify those cascade; a user with restrictive quiz/image/game dependents must fail. Do not assume every child graph cascades.
- [ ] Attempt to delete images referenced only by a current question, only by a snapshot, and by both; each remains present with its references. A truly unreferenced row is deletable.
- [ ] Verify games, snapshots, participants and answers with restrictive historical children cannot be casually removed. Read the dependent rows after the failed operation.
- [ ] Prove NULL optional image relationships round-trip and do not invent a database check for role, score range, game status or quiz eligibility where no check exists.

**Filter/gate:** `FullyQualifiedName~TenantForeignKeyTests|FullyQualifiedName~DeleteBehaviorTests`. Cross-owner corruption is rejected by the real database even without an Application handler.

## Phase 5 — Transaction atomicity and lock-helper behavior

**Files:** Create `TestSupport/{PostgresLockProbe,SqlObservationInterceptor}.cs`, `Persistence/TransactionTests.cs`, `Persistence/RowLockTests.cs`. **Consumes:** Phases 1–4 isolation/seeding/gates. **Produces:** Real blocking observations reused by later worker tests.

- [ ] `Rollback_DiscardsAllGraphChanges` and `Commit_MakesChangesVisibleToAnotherConnection`: write related rows inside an explicit transaction; verify visibility before/after completion through independent readers. Test disposal rollback after a deliberate exception.
- [ ] Exercise GetUserForUpdateAsync, GetGameForUpdateAsync, GetGameByPinForUpdateAsync and GetActiveAdministratorsForUpdateAsync inside explicit transactions. Assert missing/mismatched-owner/finished-PIN behavior and actual admin selection/order.
- [ ] For each lock family, hold the matching row on connection A, start the production helper on B, observe B blocked by A through pg_blocking_pids, release A and verify B's authoritative row. Confirm a different Host/game can progress while A is locked.
- [ ] `CancellingBlockedLock_ReleasesRequestConnectionWithoutWrite`: cancel only after PostgreSQL proves the wait; await cancellation, dispose its scope/transaction, then acquire the row through a fresh connection. Do not assert a particular task wins from Task.WhenAll alone.
- [ ] GetUser/GetActiveAdministrators use AsNoTracking; Game helpers are tracked. Verify ClearTrackedChanges prevents a later SaveChanges from persisting a discarded modification.
- [ ] Verify passing a PIN containing quote/SQL-like characters is a parameter value and produces no match/schema modification. Do not print submitted values or SQL parameters.

**Filter/gate:** `FullyQualifiedName~TransactionTests|FullyQualifiedName~RowLockTests`. SQL ordering is observed, cancellation completes within its diagnostic budget, and fresh reads establish atomicity.

## Phase 6 — Actual startup migration and seeding coordination

**Files:** Create `Persistence/{DatabaseMigrationServiceTests,DatabaseSeederCoordinationTests}.cs`, `TestSupport/{HostedServiceSession,RecordingLogger,CoordinatedSeeder}.cs`. Modify only `backend/src/Kahoot.Infrastructure/AssemblyReference.cs` to add the narrow integration friend attribute.

**Targets/interfaces:** DatabaseMigrationService.StartAsync/StopAsync; DatabaseSeeder.StartAsync/StopAsync; public ISeeder.SeedAsync(CancellationToken). CoordinatedSeeder is a local collaborator that writes real test data through its own scope and exposes an arrival/release gate; it is not a replacement production bootstrap feature.

- [ ] `ConcurrentMigrationStarts_ApplyBaselineOnce`: use SchemaMode.Empty and two independent providers targeting that database. Start both actual coordinators, verify the advisory lock serializes them and both succeed, then read one history row and real schema through a fresh post-startup provider.
- [ ] Hold the migration advisory key externally. Verify a coordinator blocks, cancel it, prove no schema changes and no retained lock, then let another coordinator succeed.
- [ ] Run migrations again after seeding a user; verify the existing data survives and no pending migrations remain. Observe `SET lock_timeout = '5s'` and SHOW lock_timeout on that migration connection with a narrow command observation. Do not invent a second migration to force a DDL upgrade test.
- [ ] Test normal queries from the actual startup provider after migration, including enum-bearing reads. If pre-migration pool metadata prevents them, retain/report the production startup defect; fresh harness migration is not a fix for that path.
- [ ] `SeederCoordination_ExcludesAnotherReplicaUntilFirstCompletes`: hold the first real seeder at its gate while its coordinator owns the advisory key. Verify replica B blocks, release A, and verify non-overlapping seeder visits plus persisted data.
- [ ] Use a later failing seeder to assert startup propagates the failure and releases the advisory lock. Verify earlier independently committed seeder data persists rather than claiming cross-seeder rollback. Cancellation while acquiring the lock must not invoke seeders.
- [ ] Keep full connection-outage retry characterization for Phase 18. Do not wait through all retries in routine coordinator tests or change hard-coded production delays to speed them up.

**Filter/gate:** `FullyQualifiedName~DatabaseMigrationServiceTests|FullyQualifiedName~DatabaseSeederCoordinationTests`. Both coordination families use real database locks; the only production diff is the friend attribute.

## Phase 7 — Persisted command idempotency and JSON responses

**Files:** Create `Services/GameCommandIdempotencyPersistenceTests.cs`; use small typed request/response records declared with these tests. **Target:** Real GameCommandIdempotencyService with IAppDbContext and PostgreSQL jsonb/bytea.

- [ ] `RecordWithoutSave_IsNotVisibleInAnotherScope`: call RecordAsync, verify absence elsewhere, then SaveChanges/commit and verify CheckAsync returns a replay with the typed cached response and stored result version.
- [ ] Assert a missing key is not a replay; changing command name or request payload for an existing key returns replay=true and `GameErrors.ValidationFailed`; use meaningful response properties rather than serialized whitespace.
- [ ] `Rollback_RemovesOperationAndIdempotencyRecordTogether`: mutate a game and add its record in the same caller transaction; rollback leaves both unchanged. Commit makes both durable.
- [ ] Duplicate game/command records race in separate scopes; one row persists and the other receives the database's composite-key unique violation. Do not claim CheckAsync+RecordAsync independently serialize callers; the service relies on caller transactions/locks and constraints.
- [ ] Use real response serialization/deserialization, changed provider scope and fixed UTC; never persist or print raw session secrets merely as convenient payload examples.

**Filter/gate:** `FullyQualifiedName~GameCommandIdempotencyPersistenceTests`. Replay evidence comes from a committed row read through the actual service.

## Phase 8 — Answer limiter Lua, atomic counts and expiry

**Files:** Create `Security/AnswerRateLimiterRedisTests.cs`. **Target:** Production IAnswerRateLimiter/AnswerRateLimiter; actual Redis scripts, never mock IDatabase/ScriptEvaluateAsync.

- [ ] `SocketWindow_AllowsFiveThenLimitsSixth`: one socket, same game, different participants as needed to separate socket and participant rules. Inspect the zset and sequence-key TTL: positive and no greater than 3000ms plus a small observation tolerance.
- [ ] `ParticipantQuestionBudget_IsSharedAcrossReplicaServices`: use blank/unknown connection IDs to bypass the socket window, issue twenty concurrent attempts through two providers, assert exactly ten allowed and ten limited; the participant counter equals twenty and TTL is positive/no greater than 600 seconds plus observation tolerance.
- [ ] Reuse a participant/question on a new socket to verify the attempt budget persists across reconnect. A new question, participant, game or harness prefix has its own budget. Inspect that game hash tags agree across the three Lua keys; this does not establish Redis Cluster support.
- [ ] Limited socket requests still consume participant attempts. Blank and case-insensitive `unknown` bypass socket tracking but do not bypass participant limits; inspect absence of conn/seq writes for that branch.
- [ ] Set an owned socket score clearly older than Redis TIME minus 3000ms and one clearly recent; the next actual limiter call prunes only the stale member. This avoids an assertion requiring millisecond-perfect scheduling.
- [ ] Verify production TTL values first. Shorten a test-owned participant TTL, await actual key disappearance, and show its budget is restored; label this as expiry recovery with an accelerated key lifetime, not a ten-minute wall-clock test.
- [ ] Keep cancellation assertions for Phase 18's dedicated paused-Redis scenario, which establishes a pending operation before cancellation. Do not demand unchanged Redis state or duplicate calls to repair a cancelled operation.

**Filter/gate:** `FullyQualifiedName~AnswerRateLimiterRedisTests`. Exact participant counts and isolation pass; socket-window tests finish within their window or report a timing/setup failure without whole-test retries.

## Phase 9 — Lobby token bucket, server time and namespace privacy

**Files:** Create `Security/LobbyJoinRateLimiterRedisTests.cs`. **Target:** Real ILobbyJoinRateLimiter/LobbyJoinRateLimiter.

- [ ] `FirstAdmission_CreatesHashedBucketWithProductionTtl`: after one call, assert the key suffix matches SHA-256 of the synthetic IP, bucket tokens are 1199, and PTTL is positive/at most 30000ms plus observation tolerance. Assert no key contains the raw IP.
- [ ] Seed an owned bucket's tokens=0.5 and updated to a future Redis TIME value, then call once: no refill and rejected. Independently seed tokens=1 with the same future setup: accepted with zero tokens. These are single-call exact boundary tests.
- [ ] Seed an old depleted bucket, call once and verify real elapsed server time refills it; a far-old bucket is capped at 1200 before consuming one. Verify a future updated timestamp cannot subtract tokens.
- [ ] Exercise bounded concurrent calls from two independently constructed limiter services. Measure Redis TIME before/after; accepted count must not exceed initial tokens plus floor(elapsedMilliseconds×0.06), accounting for the 1200 capacity. Final tokens remain nonnegative/bounded. Do not assert exactly 1200 accepted across a wall-clock burst.
- [ ] Different IPs/prefixes are independent. Empty/whitespace IPs use `unknown`; nonblank values are hashed as supplied, including surrounding spaces. Do not borrow LoginRateLimiter's normalization semantics.
- [ ] Verify PTTL renewal, then accelerate only this owned bucket's expiry and observe a fresh full bucket after disappearance. Cancellation/failure semantics follow Phase 8, not a fabricated fail-open result.

**Filter/gate:** `FullyQualifiedName~LobbyJoinRateLimiterRedisTests`. Actual token-bucket state and Redis TIME establish the assertions; no application fake clock drives refill.

## Phase 10 — Distributed Host/player leases

**Files:** Create `Realtime/HostPresenceRedisTests.cs`, `Realtime/PlayerPresenceRedisTests.cs`. **Targets:** Real HostPresenceService and PlayerPresenceService with two provider-owned multiplexers and a common test prefix/UTC clock.

- [ ] Register matching connection IDs on two replicas for a game; assert two distinct instance-qualified members exist and cross-replica presence is visible. Removing one leaves the other; removing the last reports no presence.
- [ ] Set UTC to lease expiry minus 1ms and exactly expiry: real HasAny/HasActive/GetConnectedCount pruning follows strict active-score semantics. Inspect 10-minute key TTL separately; changing UTC must not be claimed to advance Redis expiry.
- [ ] Renewal extends the scores by 45 seconds using the chosen UTC and resets key TTL. Another game/participant/prefix remains isolated.
- [ ] Player registration writes both game and participant sets; removal deletes only its own member from both. Connected count counts connections, not distinct participants. Host duplicate registration refreshes the same identity; changing its game/Host/security version fails. Player duplicate connection registration fails.
- [ ] Renew/remove concurrently on the same local service and verify removed members are absent after both operations complete, without asserting a winner. Run a bounded player set exceeding 128 to exercise the renewal batching path; this is functionality evidence, not a 25,000-socket performance result.
- [ ] Include a real Redis-unavailability registration case in Phase 18 to inspect local rollback/recovery. Do not mock failed Redis calls here.

**Filter/gate:** `FullyQualifiedName~HostPresenceRedisTests|FullyQualifiedName~PlayerPresenceRedisTests`. Local identity and actual shared sorted sets agree after operations.

## Phase 11 — Real Pub/Sub eviction and generation fencing

**Files:** Extend `TestSupport/{RecordingHubContext,RecordedDelivery}.cs` with the observations/fault hooks these tests need; create `Realtime/{HostSocketEvictionRedisTests,PlayerSocketEvictionRedisTests}.cs`. **Targets:** SocketEvictionService, SocketEvictionSubscriber, PlayerSocketEvictionSubscriber and PlayerPresenceService publish methods.

- [ ] Start actual subscribers through HostedServiceSession on two providers, await subscription readiness, and register synthetic abort callbacks using RunContinuationsAsynchronously completion sources. Keep a second Host/participant to prove selectivity.
- [ ] Publish through real ISocketEvictionService from replica A; both Host and player callbacks belonging to that Host are invoked on both replicas; unrelated callbacks remain untouched. Use bounded eventual observations, not a 100ms CI latency assertion.
- [ ] Fence a participant at generation 2: generation 1 callbacks abort, generation 2 remains live. Repeated/older fences do not abort the current generation. Test isolation by participant and prefix.
- [ ] Kick through real EvictParticipantAsync: recorder sees ParticipantRemoved with GameId, ParticipantId and StateVersion, then local callbacks abort. The recorder implements Clients(connectionIds), not only Group.
- [ ] Make the recorder throw and separately await cancellation for a stalled send; actual subscriber still aborts the participant after its bounded send attempt. Do not assert client frame delivery or WebSocket close codes.
- [ ] Publish malformed GUID/command/arity/numeric frames; verify no unrelated abort, a selected warning observation, then a valid message succeeds. Stop the selected subscriber and verify its subscription is removed without shutting down another replica's multiplexer.
- [ ] Pub/Sub frames carry game IDs but callback lookup is by participant ID; use valid globally identified participants. Do not invent subscriber-level ownership validation for arbitrary internal forged frames.

**Filter/gate:** `FullyQualifiedName~HostSocketEvictionRedisTests|FullyQualifiedName~PlayerSocketEvictionRedisTests`. Messages traverse actual Redis; the reported assertion boundary ends at delivery requests/abort callbacks.

## Phase 12 — Timer-driven heartbeats and missed-message recovery

**Files:** Complete `TestSupport/ControlledTimeProvider.cs`; create `TestSupport/ControlledTimeProviderTests.cs`, `Realtime/{HostHeartbeatPersistenceTests,PlayerHeartbeatPersistenceTests}.cs`. **Targets:** Actual heartbeat workers and their real PostgreSQL/Redis dependencies.

- [ ] Verify the minimal scheduler contracts: periodic pulse remains scheduled, one-shot consumes once, Change updates scheduling, disposed timers do not invoke callbacks, cancelled wait exits. PulseAsync does not change GetUtcNow. Keep these helper checks small and independent of Docker where possible.
- [ ] Start Host heartbeat, wait for its ten-second timer, pulse it and observe lease renewal plus the actual user query. An active matching Host/version survives; missing/suspended/non-Host/mismatched-version accounts abort. Apply the database change without Pub/Sub to prove fallback recovery.
- [ ] Register player connections and call actual internal MarkCommitted for the committed ones, representing the post-transaction boundary used by GameHub. Mark a separate uncommitted registration; it is not part of validity snapshots.
- [ ] Change/remove participants, suspend their Host, or advance durable ConnectionGeneration without sending a frame. Pulse the real player heartbeat: removed/missing/invalid-Host/stale-generation committed connections abort; valid current generation remains.
- [ ] Exercise more than 500 committed connection snapshots with valid seeded rows to verify SQL validity batching, without asserting database reads occur in a particular dictionary order. Avoid giant parameter-count/load benchmarks.
- [ ] Renew/remove followed by a heartbeat does not resurrect a removed registry entry. Stop workers while waiting on the timer and ensure no new tick/observation occurs after disposal.

**Filter/gate:** `FullyQualifiedName~ControlledTimeProviderTests|FullyQualifiedName~HostHeartbeatPersistenceTests|FullyQualifiedName~PlayerHeartbeatPersistenceTests`. Recovery is established through a real durable change and worker pass, not a direct call to AbortInvalidConnections.

## Phase 13 — Refresh-token evidence retention and SKIP LOCKED cleanup

**Files:** Create `Persistence/RefreshTokenCleanupWorkerTests.cs`. **Targets:** Actual worker lifecycle/private-loop behavior reached through StartAsync, timer ticks and SQL observation; no private reflection.

- [ ] Seed expiry/revocation values relative to fixed `now-7 days`: expired exactly at cutoff + null revocation is deleted; expiry just newer is retained; old expiry with revocation just newer is retained; both at/before cutoff are deleted. For strict timestamp boundaries use PostgreSQL's microsecond precision, not .NET ticks that cannot round-trip.
- [ ] Assert no cleanup before the first ten-minute tick. Pulse it and observe fresh-context row counts plus RefreshTokenCleanupCompleted.
- [ ] Seed 501 eligible and several protected rows; observe 500 then 1 deleted through actual SQL, pulsing the 100ms delay after registration. Protected rows/families remain intact. A bounded 20,001-row case verifies maximum 40 batches/pass, with the remainder handled on the next tick; tag this larger case Category=Extended.
- [ ] Hold an eligible row in an external transaction; another row is deleted while it remains. Release the lock and tick again to collect it. Use this externally held row to establish SKIP LOCKED progress while two real workers run; do not assume a client-side reader callback still owns an implicit transaction.
- [ ] Two providers processing eligible rows must produce the correct union of deletions with no protected deletions/deadlocks. SQL candidate caps and fresh counts prove bounded progress; Task.WhenAll alone does not prove skip behavior.
- [ ] Cancellation at a registered batch-yield delay stops further batches. Start a new real worker and resume remaining eligible rows. Failure/backoff exhaustion is exercised with dedicated dependency faults in Phase 18.

**Filter/gate:** `FullyQualifiedName~RefreshTokenCleanupWorkerTests&Category!=Extended`. Run the Extended case separately when requested and in Phase 18; report any exclusion. Scheduling/drain throughput claims need separate measurement.

## Phase 14 — Image codecs, sanitization and owned filesystem persistence

**Files:** Create `TestSupport/{ImageSamples,GatedReadStream}.cs`, `Storage/{ImageSanitizationTests,ImageStorageFailureTests,ImageCompensationTests}.cs`. **Targets:** Real IImageStorageService/ImageStorageService and ImageSharp; no fake filesystem, unsafe disk-filling or production upload directory.

- [ ] Generate tiny legitimate JPEG/PNG/WebP inputs using installed codecs. Assert actual output format/MIME, UUID-based path, stored length/dimensions, independently decodable pixels, one final file and no remaining staging files. Different uploads produce different immutable paths.
- [ ] Add synthetic EXIF orientation/metadata and IPTC/XMP; verify pixels/dimensions reflect orientation and decoded output has no profiles. Assert lossless PNG pixels exactly; JPEG/WebP use format-appropriate checks rather than lossy-byte equality.
- [ ] Create a small animated supported format fixture; the existing first-frame path must produce one static frame with first-frame pixels. If the chosen codec cannot generate a correct multi-frame sample, use a reviewed tiny fixture with recorded provenance, not an unrelated new dependency.
- [ ] Table cases: zero bytes→Image.MissingFile; a valid tiny PNG with harmless trailing padding to exactly 5,242,880 bytes is accepted/re-encoded; 5,242,881-byte streamed input→Image.TooLarge; genuine unsupported GIF/SVG/BMP/TIFF/executable magic→Image.UnsupportedType; claimed image with arbitrary bytes/truncated supported payload→Image.InvalidImage. A one-byte claimed image is malformed, not a valid minimum-size upload. Assert every rejection leaves no new final or staging file.
- [ ] Verify valid alternate filename/MIME behavior follows sniffed bytes, while explicitly prohibited extension/MIME values reject even when bytes look valid. Do not assume all mismatches are rejected.
- [ ] Verify 4096×small valid dimensions and 4097 width/height rejection using modest allocations. For area/decode-limit branch guards use direct service options with smaller budgets and small images, explicitly labelled guard tests; fixed production options are validated elsewhere. Do not allocate unsafe decompression bombs or treat options reduction as proof of actual peak memory.
- [ ] Append a unique harmless ASCII trailer to a valid image and show canonical re-encoding removes it while preserving decoded content. Do not claim this proves removal of pixel steganography.
- [ ] Block a real input read after source staging has begun, cancel, release/await and verify cancellation plus staging cleanup; input stream remains caller-owned. A throwing read stream exercises actual staging I/O failure mapping.
- [ ] Make the owned uploads path a regular file to deterministically cause directory preparation failure→StorageUnavailable. This works without depending on OS permission behavior/root privileges. Real disk-reserve/power-loss/cross-volume tests remain system work.
- [ ] Compensation removes only the requested valid in-root file, is idempotent for a missing file, and leaves an outside-root sentinel intact for traversal/wrong-route inputs. Assert actual simple-filename acceptance separately from Api's GUID/extension guard.

**Filter/gate:** `FullyQualifiedName~ImageSanitizationTests|FullyQualifiedName~ImageStorageFailureTests|FullyQualifiedName~ImageCompensationTests`. Every test owns/disposes streams/images and inspects real disk state after the operation.

## Phase 15 — Database-first image cleanup and filesystem reconciliation

**Files:** Create `Persistence/{QuestionImageDatabaseCleanupTests,QuestionImageFilesystemCleanupTests,QuestionImageCleanupConcurrencyTests}.cs`. **Targets:** Actual QuestionImageCleanupWorker with real database/files, validated options and controlled timers.

- [ ] Use validated CleanupBatchSize=2, MaxBatchesPerPass=1 and ReconciliationBatchSize=2 for small deterministic pass-bound tests. Keep production seven-day/24-hour retention and paths unchanged.
- [ ] At fixed UTC, seed a truly orphaned row exactly at seven days, one newer by a PostgreSQL microsecond, null UnreferencedSince, and old rows referenced by a current question/snapshot/both. Only eligible unreferenced rows and their files disappear.
- [ ] Put an old referenced row with a deliberately stale orphan timestamp into the database; the worker's NOT EXISTS checks preserve it. Historical retention cannot rely merely on a null timestamp or stored ImageUrl.
- [ ] Gate the candidate DELETE query's return after its result is fully consumed, using EF's DataReaderClosingAsync interception and the real reader. Through a separate connection verify the row is already absent while its file still exists, then release the worker and observe unlink. Establish actual commit visibility rather than assuming ReaderExecutedAsync means an implicit transaction has ended. See the installed-version [interceptor API](https://raw.githubusercontent.com/dotnet/efcore/v10.0.12/src/EFCore.Relational/Diagnostics/DbCommandInterceptor.cs). Missing files remain idempotent success. An old final file without a row is reconciled; a recent final file, existing-row file, unsupported extension, and nested-directory sentinel are preserved.
- [ ] Verify staging `*.tmp` at/older than 24h are removed, recent/non-tmp files survive. Set LastWriteTimeUtc explicitly; file age is independent of the database timestamp/audit clock.
- [ ] Seed more files than one reconciliation inspection cap. Pulse successive ticks and show the retained enumerator eventually visits every old orphan; no dependence on filesystem enumeration order. Never assert that staging enumeration is bounded like final-file reconciliation: its current loop is not batch-limited.
- [ ] Hold one candidate image row with an external transaction; cleanup deletes unlocked eligible rows and leaves the locked row/file. Release, tick, and collect it. Two worker providers cannot delete referenced data or produce duplicate destructive effects.
- [ ] Attachment-first order: hold a transaction creating the real FK reference, observe cleanup skips the locked image, commit and verify subsequent cleanup retains it. Cleanup-first order: observe committed deletion, then attempt attachment through a fresh context; FK rejection prevents a dangling reference. Label the latter as an ordered database boundary scenario, not proof of a simultaneously scheduled race.
- [ ] A directory at an expected image filename remains a directory sentinel after row deletion; File.Exists returns false, so this is path/type safety rather than an unlink exception test. Separately seed an old ordinary file representing failed unlink/crash and prove reconciliation recovers it. Actual unlink-error injection is deferred to a supported filesystem fault environment; do not use Windows file-sharing assumptions on Linux or claim a directory generated an exception it did not generate.
- [ ] Cancel during a controlled batch yield; later startup/ticks resume remaining orphans. A malformed stored path must never delete the outside-root sentinel, even after its database row is collected.

**Filter/gate:** `FullyQualifiedName~QuestionImageDatabaseCleanupTests|FullyQualifiedName~QuestionImageFilesystemCleanupTests|FullyQualifiedName~QuestionImageCleanupConcurrencyTests`. Durable references and actual files remain consistent across passes and contention.

## Phase 16 — Suspension finalization, resumable batches and critical state

**Files:** Create `Persistence/SuspensionFinalizerWorkerTests.cs`. **Targets:** Actual worker, real SuspensionFinalizerChannel, tracker, GameNotificationService and PostgreSQL results/rank materialization.

- [ ] Seed a suspended Host with TerminationPending=true, unfinished games and participant histories. No application suspension command is dispatched here; worker input state is explicit.
- [ ] Startup sweep without a channel hint finishes eligible games, clears PIN/grace, increments each StateVersion once, marks IsTerminatedBySuspension=true, sets FinishedAt=Host.UpdatedAt and eventually clears TerminationPending. Active/non-Host/not-pending accounts and already finished games are unchanged.
- [ ] Verify score ordering by total_score DESC, normalized_nickname COLLATE C ASC, id ASC; removed players have null rank and are excluded from podium. Include QuestionActive with real snapshots/submissions and assert materialized results are durable. Reuse seeding helpers, not copied scoring implementation.
- [ ] Seed eleven games and observe the ten-game first batch. Gate before the second batch query; after the first commit verify ten finished/one unfinished and pending=true. Stop/cancel while the gate is held, release it, then start a new worker without delivering the original hint; durable pending work resumes to completion.
- [ ] Hold one suspended Host row externally; another pending Host progresses due to SKIP LOCKED. Release and pulse the one-minute sweep to finish the skipped Host. With two worker providers, verify each game's terminal version changes once and no duplicate finalization row writes occur.
- [ ] Observe actual GameEnded delivery requests through the recorder only after fresh readers see committed games/ranks/pending state. Both audience sends are expected; count logical events by game/version rather than treating two audiences as duplicate finalization.
- [ ] Fault/cancel before commit leaves games/pending state unchanged. A recording transport failure after commit cannot undo durable finalization. Check current player token rows without asserting an expiry write absent from this worker.

**Filter/gate:** `FullyQualifiedName~SuspensionFinalizerWorkerTests`. Startup/channel/sweep entry points use the real lifecycle; resumability does not depend on retained in-memory hints.

## Phase 17 — Host abandonment recovery and terminal-state races

**Files:** Create `Persistence/GameAbandonmentWorkerTests.cs`. **Targets:** Actual worker, HostPresenceService, PostgreSQL finalization and real notification implementation/local delivery observation.

- [ ] No work occurs before a five-second tick. With no live Redis Host lease and null grace, the real recovery pass sets grace=chosen UTC+300 seconds. A live lease keeps grace unset; finished games remain untouched.
- [ ] With expired grace and no live presence, finish the game, clear PIN/grace, increment StateVersion, materialize active-question results, rank nonremoved participants and request GameEnded with reason HostAbandoned. Existing history is preserved.
- [ ] Grace exactly equal to UTC is eligible; future grace is not. Seed expiration explicitly and pulse the worker without pretending a fake clock expired Redis TTL.
- [ ] An expired-grace game with restored live Host presence has grace cleared and remains nonterminal. Actual Redis registration, not a stub HasAny result, establishes presence.
- [ ] Stamp ExpiresAt=finished UTC+24h only on participant tokens whose ExpiresAt is null; existing expiries remain unchanged. This is persistence evidence, not proof of request/reconnect expiry enforcement.
- [ ] Seed 51 expired games with non-null grace. Gate provider A before its first finalization commit, let B process the unlocked remainder, verify no more than 50 candidates in A and correct union/version increments after release. Add a held expired row case proving SKIP LOCKED directly.
- [ ] Race abandonment with the Phase 16 worker using actual transaction gates and two providers. Establish each order explicitly: whichever finalizes first makes the other skip the terminal game; StateVersion advances once, ranks/results remain consistent, no contradictory second terminal mutation. Do not assume a terminal game's suspension reason is overwritten.
- [ ] Force recovery failure separately from a sweep success in Phase 18; the tracker must not be falsely cleared for the combined pass. A post-commit transport failure preserves finished state.

**Filter/gate:** `FullyQualifiedName~GameAbandonmentWorkerTests`. Real presence, durable terminal state, batch limits and both selected finalization orders are demonstrated.

## Phase 18 — Dependency faults, composition and final handoff

**Files:** Create `TestSupport/FaultTestEnvironment.cs`, `Persistence/CriticalWorkerRecoveryTests.cs`, `Realtime/RedisDependencyFailureTests.cs`, `DependencyInjectionIntegrationTests.cs`. Update this plan's execution evidence only after running checks. No CI/deployment file changes.

- [ ] Allocate dedicated PostgreSQL/Redis containers for destructive outage scenarios; do not stop shared collection resources. Seed first, start the selected worker, then stop the owned dependency and await a real failed operation/pass.
- [ ] For abandonment/suspension, inspect actual tracker names AbandonedGameFinalizer/SuspensionGameFinalizer. Set UTC to first failure+15 minutes, execute another failed pass and assert degradation; restart the dependency, await real connectivity/pass completion, verify persisted work finishes and the tracker recovers. This does not test Api readiness caching/status codes.
- [ ] For refresh/image cleanup, observe 10/30/60-second retry timer registrations and pulse them in order; after the fourth failure assert the selected exhausted-retry event. Restart and tick again to verify later recovery. Assert partially completed prior batches remain committed rather than expecting pass-wide rollback.
- [ ] For Redis outage, actual rate-limit service calls fail/timeout instead of returning a successful admission. Failed presence registration leaves no registered local connection; after restart inspect Redis for stale partial members and eventual lease cleanup, then register a new connection successfully. Do not assume Redis/Pg rollback atomically with each other.
- [ ] In dedicated Redis, acknowledge a bounded CLIENT PAUSE WRITE, start a real limiter call, cancel its wait while script writes are paused, and assert OperationCanceledException. Unpause in finally and observe the queued script's eventual counter update; cancellation did not roll it back. Set the pause and linked cancellation budgets below the multiplexer timeout. These administrative commands affect only this test-owned server; see [CLIENT PAUSE](https://redis.io/docs/latest/commands/client-pause/) and [CLIENT UNPAUSE](https://redis.io/docs/latest/commands/client-unpause/).
- [ ] Verify a missed eviction message during subscriber downtime is recovered by Phase 12's real PostgreSQL sweep after restart. Pub/Sub does not replay earlier messages merely because a subscriber reconnects.
- [ ] Exercise migration pre-start connection outage/cancellation within budgets. Full six-attempt 1/2/4/8/16-second retry timing is an explicit Extended case with its own 180-second budget, using real time because that service has no injected TimeProvider; do not retrofit one solely to speed this test. Ordinary coverage observes a real retry event then cancels promptly.
- [ ] Build one Generic Host using generated test configuration and real AddInfrastructure, with the same local delivery boundary. Intentionally start all registered IHostedService instances, prove migrations precede coordinated seeding and selected worker/subscriber readiness observations, then stop within budget and verify resources release. Bootstrap remains disabled; HTTP/API startup is not exercised. This is additional composition coverage, not the default harness path.
- [ ] Check malformed/true DR flags reject composition as implemented. Report intended pool-default and documentation cadence gaps; keep production defaults unchanged. Do not present a green fail-closed test as implemented DR recovery.
- [ ] Run all ordinary tests, then explicitly run Category=Extended cases. Execute the ordinary suite a second time to detect resource/order leaks; further repetition requires new evidence rather than routine endless retries.
- [ ] Run build, focused format and whitespace checks; inspect the complete final diff for only requested tests and the narrow friend attribute. Record pass/fail counts, discovered/skipped counts, actual images/server versions, elapsed time and any environment limitations with secrets redacted.

**Filter/gate:** `FullyQualifiedName~CriticalWorkerRecoveryTests|FullyQualifiedName~RedisDependencyFailureTests|FullyQualifiedName~DependencyInjectionIntegrationTests`, followed by the final commands below. No claim of distributed production readiness follows from these tests alone.

## Final verification commands and completion criteria

Run from `backend/`:

```bash
dotnet test test/Kahoot.Infrastructure.IntegrationTests/Kahoot.Infrastructure.IntegrationTests.csproj -c Release --filter "Category!=Extended"
dotnet test test/Kahoot.Infrastructure.IntegrationTests/Kahoot.Infrastructure.IntegrationTests.csproj -c Release --filter "Category=Extended"
dotnet build Kahoot.slnx -c Release
dotnet format test/Kahoot.Infrastructure.IntegrationTests/Kahoot.Infrastructure.IntegrationTests.csproj --verify-no-changes --no-restore
git -c core.whitespace=cr-at-eol diff --check
```

Repeat the ordinary suite once as Phase 18 specifies. If AssemblyReference.cs changes, include it in focused formatting verification as well. Existing unit suites can be run for the touched Infrastructure assembly; do not claim unrelated suites ran because the solution built. An existing failure in another agent's changes must be reported separately without altering that work.

Completion requires the requested phases to have discovered/runnable tests, real-dependency assertion evidence, deterministic boundaries, bounded cleanup, no secret-bearing output, no placeholder skipped tests, and an honest gap report. A deliberately retained failing regression remains a defect; it is not a passing completion gate. Docker absence, codec/environment limitations and excluded Extended tests must be reported explicitly.

CI handoff: specify the .NET 10 SDK, Docker engine privileges/connectivity, image pulls, resource reaper networking, dynamic ports, sufficient RAM/disk for PostgreSQL/Redis/image decoding and commands above. Keep results diagnostic but sanitized. Adding a CI job, coverage package, Redis Cluster topology, proxy, load generator or process-crash harness requires a separately requested scope.

## Ready-to-use Gemini prompt

```text
Implement Phase <number> of docs/superpowers/plans/2026-10-01-infrastructure-integration-tests.md only.

Read applicable repository instructions and the phase's actual source first. This is an explicitly requested testing task; preserve unrelated edits. Follow Clean Architecture: this project references only Kahoot.Infrastructure; do not reference Api or another test project.

Use the existing xUnit v2/Testcontainers packages, real PostgreSQL and Redis, production migrations/mappings/auditing, owned temporary files, fresh scopes, and independently owned replica providers. Follow the named helper interfaces and add only helpers this phase needs. Keep explicit C# types and focused readable tests.

Invoke actual internal workers through typed public hosted-service lifecycles after the one narrow friend-assembly edit in Phase 6. Never reflect into private passes. Wait for timer/subscription/SQL observations rather than fixed sleeps. Fake application time does not change Redis TIME, Redis TTL, or PostgreSQL time.

Verify test outcomes and committed state through fresh scopes/connections. Establish lock order with gates and pg_blocking_pids. Record SignalR delivery requests locally only where the plan explicitly places the transport outside scope; do not claim actual socket/backplane evidence from those records.

Do not add packages, modify production behavior/defaults/migrations, weaken assertions, skip missing-infrastructure failures, retry whole tests, auto-commit, or implement later phases. Report specification gaps and confirmed defects with evidence instead of changing production to make a test pass.

Run the phase filter, necessary formatting/diff checks, inspect ownership/cleanup, and report authored files, test discovery and pass/fail counts, actual dependency evidence, limitations and gaps. Stop at the phase gate.
```

## Plan validation performed during authoring

The plan was checked against the current test scaffold, Infrastructure source/configurations/workers, implemented unit-test boundaries, relevant requirements and official dependency/lifecycle documentation. Document structure, local links, source paths and whitespace are validated separately from execution. No Infrastructure integration tests, production friend attribute, dependency changes, containers, build or runtime tests were authored/executed by this planning task.
