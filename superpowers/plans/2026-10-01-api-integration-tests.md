# Kahoot API Integration Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans, or superpowers:subagent-driven-development when that execution method is explicitly selected. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Handoff:** Gemini should implement one requested phase at a time. If Superpowers is unavailable, follow the tasks and gates directly. This plan authorizes no automatic commits, dependency upgrades, production fixes, deployment changes, or implementation of later phases.

**Goal:** Build clean, maintainable tests in `backend/test/Kahoot.Api.IntegrationTests` that verify the existing API through real HTTP requests, HTTPS cookies, WebSocket SignalR connections, PostgreSQL, Redis and local image storage.

**Architecture:** Boot the actual API entry point using WebApplicationFactory with Kestrel. Preserve production middleware, authentication, authorization, MediatR, persistence, Redis backplane, storage and hosted services. Each test owns a database, Redis namespace, temporary content root, clients, connections and application instances; setup and verification use separate scopes.

**Tech Stack:** Existing .NET 10; xUnit 2.9.3; Microsoft.NET.Test.Sdk 17.14.1; xunit.runner.visualstudio 3.1.4; Microsoft.AspNetCore.Mvc.Testing and Microsoft.AspNetCore.SignalR.Client 10.0.12; Testcontainers.PostgreSql and Testcontainers.Redis 4.15.0. Use the API's installed EF Core/Npgsql, ImageSharp, JWT and OpenTelemetry dependencies through existing references.

**Spec:** The user's request for the same detailed phased integration-testing plan, with clean code and professional verification practices. Read [authentication](../../02-authentication.md), [account management](../../03-account-management.md), [quiz management](../../04-quiz-and-question-management.md), [images](../../05-image-management.md), [game lifecycle](../../06-game-lifecycle.md), [joining/lobby](../../07-joining-and-lobby.md), [gameplay](../../08-live-gameplay.md), [scoring](../../09-scoring-and-leaderboards.md), [realtime protocol](../../10-realtime-and-protocol.md), [reconnection](../../11-reconnection.md), [operations/health](../../12-platform-operations-and-health.md) and [verification matrix](../../14-verification-and-testing.md). These describe intended contracts. Source inspection determines what exists and which requirements need gap reports.

**Companion plans:** [Application integration tests](2026-10-01-application-integration-tests.md) and [Infrastructure integration tests](2026-10-01-infrastructure-integration-tests.md). Align conventions, but do not reference their test projects or copy their entire harnesses.

**Inspection date:** 2026-10-01. The API integration project contains its scaffold and generated build output, with no authored C# tests. Existing API unit tests cover controller adaptation and framework regressions. Other work is active in this checkout: recheck affected source before executing each phase.

## Global constraints

- This delivery is documentation only. Do not implement tests, change production code, add packages or change project references during planning.
- Read applicable AGENTS.md and OpenWolf instructions. The explicit testing request supersedes the historical default prohibition on tests within this scope. Preserve unrelated edits.
- Keep the single project reference to `../../src/Kahoot.Api/Kahoot.Api.csproj`. Application, Domain and Infrastructure types are accessible through normal transitive references. Add no shared test project.
- Keep Microsoft.NET.Sdk.Web, OutputType=Library and the four existing project-local package references. Use inherited xUnit/VSTest settings. Add no mocking, Respawn, SQLite/InMemory, fake-time, assertion, snapshot, telemetry exporter or load-testing package.
- Use the real startup entry point and HTTP pipeline. Do not substitute authentication, authorization, CurrentUser, ISender, handlers, DbContexts, hubs, notification services, presence services, Redis backplane or image storage in ordinary scenarios.
- Keep production hosted services in the default host. Do not remove all IHostedService registrations or manually invoke private worker methods.
- Apply real EF migrations and mappings. Do not use EnsureCreated, replacement schema scripts or mocked DbSets.
- No production visibility changes are necessary: use the public non-static ApiController type as the factory's entry-assembly marker. Do not add public partial Program or integration friend attributes by habit.
- Generate disposable credentials, signing keys, TLS certificates, database names and Redis prefixes at runtime. Never read .env files, connect to shared infrastructure, use repository upload directories or print secret-bearing configuration.
- Keep production contracts and policies intact. Missing functionality and defects belong in explicit reports with failing evidence; do not implement them to make this suite green.
- Do not silently skip unavailable Docker, suppress regressions, retry whole tests, disable activities, weaken transport security or accept several incompatible statuses to obtain a passing run.
- Use explicit C# types, focused files, explicit constructors, file-scoped namespaces and intention-revealing test names. Follow .editorconfig; avoid private reflection, hidden caller mutation, generic scenario engines and redundant comments.
- Stop at the requested phase's gate. This plan does not authorize implementation of missing production requirements.

## Review focus

1. A valid JWT outlives a password/status change: the next protected request and affected live sockets must obey committed database security state. Phases 4, 6, 8 and 17.
2. A browser supplies both a refresh cookie and a body token: cookie precedence, CSRF, Origin and cookie paths must survive actual HTTP serialization. Phase 5.
3. A player watches another participant's game or personal results: real group membership and cross-instance delivery must preserve audience isolation and pre-reveal secrecy. Phases 14–17.
4. A request is cancelled while waiting for a database row: prove server cancellation, rollback and lock release without relying on a client-side exception alone. Phase 19.
5. A client reconnects after missing a broadcast or a replica stops: PostgreSQL catch-up must reflect committed state; close-code and rolling-restart guarantees remain explicit evidence gates. Phases 16, 17 and 19.

## What this project proves

| Suite | Main evidence boundary |
| --- | --- |
| Domain/Application/API unit tests | Deterministic rules, guards and adaptation in isolation. |
| Application integration tests | Actual handler pipelines and durable state through real persistence and selected infrastructure. |
| Infrastructure integration tests | Adapter SQL/Redis/files, selected worker lifecycles and failure recovery. |
| **API integration tests in this plan** | Actual routing, binding, middleware, JWT/role enforcement, cookies, HTTP serialization, uploads, health endpoints, SignalR protocol, backplane delivery and complete host lifecycle. |
| Separate system/load/browser verification | Browser CORS/SameSite enforcement, Nginx/proxy behavior, deployed TLS, mobile clients, large fan-out, latency percentiles, slow consumers, soak and disaster recovery. |

Database checks complement HTTP assertions; they do not replace a request to the endpoint under test. HTTP 200 alone does not prove atomic persistence or security state. Do not duplicate every handler race or codec fixture from companion suites; add API scenarios when identity, serialization, delivery or lifecycle changes the evidence.

## Actual implementation inventory

Start with these production files; paths are repository-relative:

- `backend/src/Kahoot.Api/Program.cs`: middleware order, registration, storage initialization, endpoints and shutdown admission.
- `backend/src/Kahoot.Api/Controllers/ApiController.cs`: business-error ProblemDetails mapping.
- `backend/src/Kahoot.Api/Controllers/{AuthController,QuizzesController,AdminUsersController,AdminAdministratorsController,GamesController,ImagesController}.cs`: routes and request adaptation.
- `backend/src/Kahoot.Api/Middleware/{GlobalExceptionHandler,AccessTokenScrubberMiddleware}.cs`: exception classification and query-token redaction.
- `backend/src/Kahoot.Api/ServiceCollectionExtension/{JwtAuthenticationInstaller,CorsInstaller,ObservabilityInstaller}.cs`: JWT database checks, CORS and telemetry.
- `backend/src/Kahoot.Api/Services/CurrentUser.cs`; `backend/src/Kahoot.Api/HealthChecks/{DatabaseHealthCheck,RedisHealthCheck,StorageHealthCheck,ReadinessHealthCheck}.cs`.
- `backend/src/Kahoot.Infrastructure/Realtime/{GameHub,GameHubFilter,GameNotificationService,HostPresenceService,PlayerPresenceService,SocketEvictionSubscriber,PlayerSocketEvictionSubscriber,HostPresenceHeartbeatWorker,PlayerPresenceHeartbeatWorker,UnauthenticatedSocketGuard}.cs`.
- `backend/src/Kahoot.Infrastructure/ServiceCollectionExtension/{PersistenceInstaller,SecurityInstaller,RealtimeInstaller,StorageInstaller}.cs`; `backend/src/Kahoot.Infrastructure/Persistence/{AppDbContext,DatabaseMigrationService,DatabaseSeeder}.cs`.
- `backend/src/Kahoot.Application/Features/`: actual records, validators, handlers and error registries; `Features/Games/Models/GameDtos.cs` defines event/catch-up payloads.

### HTTP route inventory

The controllers currently declare **39 HTTP actions**. Every method below needs a concrete contract scenario. Resource IDs have GUID route constraints where present in source.

| Methods and paths | Policy / implemented success contract | Phase |
| --- | --- | --- |
| POST /api/auth/register, POST /api/auth/login | Anonymous; 201 account / 200 access-token response plus cookies. | 3 |
| POST /api/auth/refresh, POST /api/auth/logout | Anonymous metadata; token/CSRF/Origin guards; 200 / 204. | 5–6 |
| POST /api/auth/logout-all, POST /api/auth/change-password | Host or SystemAdmin; 204 and cookie clearing. | 6 |
| POST, GET /api/quizzes | Host; 201 / 200 keyset page. | 7 |
| GET, PUT, DELETE /api/quizzes/{quizId} | Host plus ownership; 200 / 200 / 204. | 7 |
| POST /api/quizzes/{quizId}/questions | Host plus ownership; 201 with question Location. | 7 |
| PUT, DELETE /api/quizzes/{quizId}/questions/{questionId}; POST /api/quizzes/{quizId}/reorder | Host plus ownership; 200 / 204 / 200. | 7 |
| GET /api/admin/users, GET /api/admin/users/{accountId} | SystemAdmin; 200 page / details. | 8 |
| POST /api/admin/users/{accountId}/suspend, POST /api/admin/users/{accountId}/reactivate | SystemAdmin; 204 or suspend 202; reactivate 204. | 8 |
| GET, POST /api/admin/administrators | SystemAdmin; 200 list / 201 account. | 8 |
| POST /api/admin/administrators/{id}/suspend, POST /api/admin/administrators/{id}/reactivate | SystemAdmin; 204. | 8 |
| POST /api/games, GET /api/games/{id} | Host; 201 snapshot game / 200 overview. | 9 |
| POST /api/games/{id}/start, end-question, show-leaderboard, advance, end | Host; body {commandId, expectedStateVersion}; 200 operation-specific response. | 9 |
| GET /api/games/{id}/report, GET /api/games/{id}/participants, DELETE /api/games/{id}/participants/{participantId} | Host; 200 / 200 / 204. | 9, 11 |
| POST /api/games/join, GET /api/games/join/{pin} | Anonymous; 200 join/session response / join information. | 10 |
| POST /api/games/{id}/answers | Anonymous metadata; player-session-token guard; 200 {accepted, alreadyAnswered}. | 11 |
| POST /api/uploads/images | Host, multipart; 201 {imageId, url}. | 12 |
| GET /uploads, GET /uploads/{*filename} | Anonymous; missing-image business error / streamed file. | 12 |

Additional endpoints: anonymous HTML /; plain-text /health/live, /health/ready and /health; /hubs/game negotiation and WebSockets. OpenAPI and Scalar are mapped only in Development. There is **no single-question GET** matching AddQuestion's Location. Do not fabricate it or follow that Location expecting 200.

### Source/spec differences to preserve and report

1. **Business errors with an active Activity:** ApiController passes traceId into ControllerBase.Problem; the installed default MVC factory also creates traceId, and adding extensions can throw for a duplicate key. An existing API unit regression exercises this real factory. Reproduce over HTTP in Phase 2; do not replace ProblemDetailsFactory or turn off tracing.
2. **Trace format:** JWT/exception responses call Results.Problem with a 32-character TraceId, but the default writer can replace it with full W3C Activity.Id. Operations documentation requires 32 hexadecimal characters. Verify the wire value and keep the contract regression visible.
3. **Inactive-account bearer tokens:** OnTokenValidated rejects suspended/deleted users and role/security-version mismatches; protected requests challenge with **401 Auth.Unauthorized**. Record the difference from documented status language implying 403.
4. **Quiz stale edits:** commands have no expectedRevision; incremented Revision and Host-row serialization do not implement documented stale-edit rejection. Do not invent a request field and expect 409.
5. **Socket close codes:** Context.Abort and delayed abort exist. They do not establish documented 4403 group-auth rejection or 1001 Going Away during drain. Record actual transport behavior and keep separate explicit requirement regressions.
6. **Health:** responses are plain text. CriticalWorkerFailureTracker tracks reported continuous failure, not arbitrary backlog age. Readiness has a three-second cache and 450 ms probe budget.
7. **Clocks:** relevant acceptance/reconnect paths use PostgreSQL clock_timestamp; Redis has its own clock; active catch-up remainingSeconds uses application TimeProvider. An application-clock override cannot control all three.
8. **File delivery:** the action sets nosniff and immutable caching, but no explicit ETag/Last-Modified or enableRangeProcessing. Do not require invented range/caching semantics.
9. **Fresh-schema startup:** migration coordination opens connections before enum-creating DDL. Verify whether a fresh host subsequently seeds/reads enum-backed rows without metadata refresh. The default harness below uses a separately migrated DB and fresh pool; it does not prove empty-schema startup.

Treat source-derived risks as unverified until reproduced. Do not weaken valid expected contracts. Mark a gate blocked, show the failing scenario and await a separately authorized production fix. Independent happy-path phases can proceed when requested, with existing red regressions still reported.

## Clean-code harness design

### Actual entry point and early configuration

Use `KahootApiFactory : WebApplicationFactory<Kahoot.Api.Controllers.ApiController>`. ApiController is a public non-static class in the executable assembly; it is an assembly marker, not a replacement entry point. The factory locates that assembly's actual Program. The static AssemblyReference cannot be a generic type argument. See the installed [WebApplicationFactory 10.0.12 source](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Mvc/Mvc.Testing/src/WebApplicationFactory.cs).

JWT, CORS origins, Redis/backplane settings and telemetry sampling are read during registration. A late ConfigureAppConfiguration callback alone is insufficient. Override `protected override IHost CreateHost(IHostBuilder builder)`; add the complete generated configuration through ConfigureHostConfiguration **before** building/starting the host. Follow the factory's actual Build/Start sequence and retain ownership of the built host if Start throws, so failed startup can be disposed asynchronously by the sandbox. The deferred minimal-host builder forwards these settings as entry-point arguments; see its [10.0.12 implementation](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Mvc/Mvc.Testing/src/DeferredHostBuilder.cs).

Include environment=Testing, contentRoot=<owned absolute root>, TEST_CONTENTROOT_KAHOOT_API=<same root>, all validated sections and safe bootstrap overrides. ConfigureWebHost uses the same environment/root and scope validation; do not replace Program. The test-content-root setting avoids a fragile solution-file search in this .slnx repository. MvcTestingAppManifest.json is generated build output, not a file to commit.

The owned root has no repository appsettings. Do not mutate process environment variables or current directory. Verify early-captured values through actual login, prefix observations and CORS preflight, rather than IConfiguration reads after startup. Isolate ambient OTLP settings with the export boundary below.

### HTTPS and real WebSockets

Use one hosting model: loopback HTTPS Kestrel, including ordinary HTTP tests. Before Services/startup, call `UseKestrel(Action<KestrelServerOptions>)` and configure `Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(certificate))`. The existing testing package provides this API.

Create a short-lived certificate with BCL CertificateRequest, generated private key and loopback/localhost SANs. Pin that exact certificate in this fixture's HTTP/WebSocket handlers and reject any other certificate. Do not install global trust, use DangerousAcceptAnyServerCertificateValidator or change production HTTPS behavior.

After StartServer, get the assigned HTTPS address from `factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()`. In Kestrel mode do not access factory.Server, which is TestServer-only. Build owned HttpClients with redirects disabled. Cookie clients own separate CookieContainers; body-refresh/malformed-token clients use no automatic jar.

SignalR uses the same address, real negotiation, HttpTransportType.WebSockets, AccessTokenProvider for a Host JWT when needed and fixture pinning for both negotiation and upgrade. Keep SkipNegotiation false. Connect before actual JoinAsHost/JoinGame/Reconnect. Do not directly invoke GameHub or mock IHubContext to claim delivery.

### Isolation, lifetimes and setup

- One nonparallel xUnit collection owns disposable PostgreSQL 17 and Redis 7.4.11-alpine containers. Match deployment versions; use image-taking 4.15 builders, dynamic ports, module readiness, resource reaper and no reuse.
- Each ApiSandbox owns a generated database, ASCII Redis prefix under 64 characters, temporary root, certificate and security configuration. Related replicas share DB/prefix/root/signing settings but own independent factories, singleton state and subscriptions.
- Ordinary sandboxes migrate with a short-lived provider using actual AddPersistence, System TimeProvider, logging and a minimal anonymous MigrationCaller implementing ICurrentUser (UserId=null, Role=null, IsAuthenticated=false). Production CurrentUser is internal to Api and cannot be registered by type here without a visibility change. MigrationCaller supplies only the setup provider's audit dependency; it never replaces the API's real request-scoped CurrentUser. Dispose that provider and its NpgsqlDataSource before starting API pools. It starts no hosted services. The actual API starts every production service, including real migration/seeding. Empty mode is introduced in Phase 19.
- Default bootstrap is disabled through both BootstrapAdmin:Enabled=false and BOOTSTRAP_ADMIN_ENABLED=false, with generated safe fields where required. Admin setup seeds a real SystemAdmin with the real password hasher, then logs in through HTTP. Separate startup tests enable real bootstrap.
- Supply valid ConnectionStrings:DefaultConnection, Database timeouts/diagnostic flags, Jwt issuer/audience/generated Base64 key/lifetime, RefreshToken lifetime/family lifetime, GameJoin client origin, Realtime connection/prefix, ImageStorage defaults and Cors origins. Preserve ordinary size/rate/grace/security defaults.
- Default environment is Testing; Development is a separate host for documentation routes. Disable detailed/sensitive DB diagnostics. Use a suite-owned telemetry service name and always_on tracing for correlation/export scenarios; isolate every exporter.
- Use fresh setup/read scopes. Return IDs or immutable non-secret snapshots, not tracked entities, DbContexts or scopes. Real request identities come from JWT validation; no fake CurrentUser for requests.
- Keep actual Host connections for lifecycle cases where abandonment workers could change state. Use long-enough question durations and explicit transitions rather than racing deadlines.
- Cleanup closes clients/hubs, stops/disposes hosts/subscriptions/pools, removes only the sandbox Redis prefix, drops only its DB and deletes only its root. Keep shared containers to collection disposal. No FLUSHDB or global ClearAllPools.
- Cleanup attempts all owned resources and surfaces sanitized failures alongside original assertions. Never use empty catch blocks to make disposal appear successful.

See the [ASP.NET integration guide](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) and [EF provider-testing guidance](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy).

Use these concrete ordinary-host settings in ApiConfiguration; pass connection strings through typed builders, never formatted diagnostic output:

| Setting | Test-owned value |
| --- | --- |
| ConnectionStrings:DefaultConnection | Container connection targeting the generated sandbox database; Pooling=true, MinPoolSize=0, MaxPoolSize=20, Timeout=15, replica-specific ApplicationName. Fault profiles explicitly override pool/timeout values. |
| Database:CommandTimeoutSeconds / MigrationCommandTimeoutSeconds | 30 / 120; EnableDetailedErrors=false and EnableSensitiveDataLogging=false. |
| Jwt:Issuer / Audience / SigningKey / AccessTokenMinutes | kahoot.api.integration / kahoot.api.integration.client / runtime Base64 of 32 cryptographic random bytes / 15. |
| RefreshToken:LifetimeDays / FamilyMaxLifetimeDays | 14 / 30. |
| BootstrapAdmin:Enabled and BOOTSTRAP_ADMIN_ENABLED | Both false; when the dedicated bootstrap case enables them, supply runtime username and a generated password satisfying the installed validators. |
| GameJoin:ClientBaseUrl / Cors:AllowedOrigins:0 | https://frontend.kahoot.test for both; no trailing slash. |
| Realtime:RedisConnectionString / ChannelPrefix | Generated container connection and sandbox ASCII prefix, shared by related replicas. |
| ImageStorage | Bind installed ImageStorageOptions defaults, including MaxFileSizeBytes=5,242,880, UploadsSubdirectory=uploads and StagingSubdirectory=uploads/staging under owned wwwroot. |
| TrustedProxies:Networks | No added networks normally; built-in loopback trust still exists. Phase 13 explicitly defines its untrusted profile. |
| OTEL_SERVICE_NAME / OTEL_TRACES_SAMPLER | Suite-owned service name / always_on; all OTLP transport/options are isolated below. |

Export capture, UTC offsets, drain gates, forwarding profiles and SQL observers are explicit test-only observation/fault seams. They must not rewrite endpoint results, authorize requests or replace production side effects.

### Telemetry export boundary

Preserve production observability and filters. From the first host, use PostConfigureAll<OtlpExporterOptions> for every named exporter: HttpProtobuf, synthetic local endpoint, empty Headers, bounded timeout and HttpClientFactory returning an owned client with a local handler. It returns application/x-protobuf 200 and an empty valid protobuf response without networking. Inspect the installed [1.19.1 options API](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.19.1/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs).

This changes only external export transport. Phase 20 adds local BaseExporter<Activity>/processors and immutable log snapshots through installed SDK APIs; no InMemory package. Verify every signal/named exporter is isolated. Do not disable activities, clear production filters or change error writers. SDK capture does not prove deployed Collector delivery.

### Small helper interfaces

All paths below are under `backend/test/Kahoot.Api.IntegrationTests/`. Create helpers at their first consuming phase.

| File / helper | Required interface and responsibility |
| --- | --- |
| TestSupport/ApiCollection.cs, ApiDependencyFixture.cs | Task-based xUnit IAsyncLifetime; `Task<ApiSandbox> CreateSandboxAsync(CancellationToken cancellationToken = default)`; collection disables parallelization. |
| TestSupport/ApiSandbox.cs, SchemaMode.cs | IAsyncDisposable; `Task<ApiHost> StartHostAsync(string replicaName, CancellationToken cancellationToken = default)`; own related hosts/resources. SchemaMode Migrated/Empty is added at Phase 19. |
| TestSupport/ApiConfiguration.cs, KahootApiFactory.cs | Generated early settings and actual entry point; only named/narrow fault profiles when a phase needs them. |
| TestSupport/MigrationCaller.cs | Anonymous ICurrentUser for the migration-only setup provider; no mutable identity and no registration in API factories. |
| TestSupport/ApiHost.cs, TestCertificate.cs | Actual BaseAddress/Services; `HttpClient CreateClient()`, `HttpClient CreateCookieClient()`, `HttpClient CreateAuthenticatedClient(ApiIdentity identity)`; owned handlers/jars/pinning. |
| TestSupport/LocalOtlpHandler.cs | Export boundary only; valid empty protobuf HTTP 200, no network/payload rendering. |
| TestSupport/ApiIdentity.cs, AuthenticationData.cs | Secret-safe sealed class, not a positional record with generated ToString; `Task<ApiIdentity> RegisterAndLoginHostAsync(ApiHost host, CancellationToken cancellationToken)`; `Task<ApiIdentity> SeedAndLoginAdministratorAsync(ApiHost host, CancellationToken cancellationToken)`. |
| TestSupport/DatabaseAssertions.cs | Fresh no-tracking snapshots with actual AppDbContext; do not wrap every xUnit assertion. |
| TestSupport/GameData.cs | `Task<GameScenario> CreateLobbyAsync(ApiHost host, ApiIdentity owner, CancellationToken cancellationToken)`; minimal quiz/questions/game through HTTP. GameScenario contains IDs/public response data. |
| TestSupport/ProblemAssertions.cs | `Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken cancellationToken)`; content type/status/code/requestId checks and cloned JsonElement. Model-binding assertions remain separate. |
| TestSupport/SignalRConnection.cs, SignalREvents.cs | Real HubConnection/IAsyncDisposable; `Task<SignalRConnection> ConnectAsync(ApiHost host, ApiIdentity? identity, CancellationToken cancellationToken)`; `Task<JsonElement> InvokeAsync(string method, object?[] arguments, CancellationToken cancellationToken)`; bounded subscriptions/Closed observations. |
| TestSupport/WebSocketObservation.cs | When close-code evidence first needs it, observe a real BCL ClientWebSocket after actual SignalR negotiation/JSON handshake; record received close frame/status separately from abort/I/O failure. Use owned pinning, limits and cancellation. |
| TestSupport/PostgresRequestGate.cs, AsyncGate.cs | External transaction/connection and pg_blocking_pids observation; scoped IDs, release in finally, observe server completion. |
| TestSupport/ImageSamples.cs | Small legitimate codec fixtures; no external images or application uploads. |
| TestSupport/FaultSandbox.cs | Dedicated containers/hosts for disruption/small pool/storage/lifecycle; never stop shared containers. |
| TestSupport/UtcTimeProvider.cs | Phase 18 only: `void AdvanceUtc(TimeSpan interval)`; override UTC, retain real timers/timestamps. Does not control JWT-library, PG or Redis clocks. |
| TestSupport/DrainGate.cs | Phase 19: actual IHostedLifecycleService, awaitable StoppingEntered and `void Release()`; cancellation-aware StoppingAsync gate, other lifecycle methods no-op. No worker/server replacement. |
| TestSupport/TelemetryCapture.cs | Immutable Activity/log snapshots at observation/export; do not retain pooled LogRecords or render secret values in assertions. |

Keep JSON visible at call sites and helpers focused. Split growing setup by feature. Do not create a generic CRUD driver, fixture inheritance tree, service-locator test DSL or universal assertion library.

### Reliable async assertions

Register hub callbacks before connecting/commands. Use Channel<T> or TaskCompletionSource with RunContinuationsAsynchronously and bounded cancellation. Match event/game/participant/stateVersion, not just the next callback. Dispose subscriptions.

For forbidden delivery, prove both recipients are attached and observe positive delivery on the intended recipient before checking the foreign connection's captured messages over a bounded interval. This proves interval absence, not infinite absence. A never-ready observer's timeout is not privacy evidence.

Task.WhenAll does not establish SQL overlap. Hold an external row lock and observe pg_blocking_pids before cancellation/release. Test-owned timestamp mutations are explicit arrangements; the real request/hub makes the decision.

Use System TimeProvider normally. A narrow UTC override does not control timers, JWT lifetime checks, PostgreSQL deadlines or Redis TIME/TTLs. Never shorten security windows to obtain passing ordinary tests.

## Phase execution and verification convention

Each phase lists Read, Files, Interfaces, steps and a gate. Read-only production files remain unchanged. Begin with a focused failing contract/harness-compilation cycle, then write only the test support required to exercise existing behavior. A legitimate production regression is not permission to fix production.

Working directory is `backend`:

```bash
dotnet build test/Kahoot.Api.IntegrationTests/Kahoot.Api.IntegrationTests.csproj --configuration Release
dotnet test test/Kahoot.Api.IntegrationTests/Kahoot.Api.IntegrationTests.csproj --configuration Release --no-build --filter "Phase=01" --logger "console;verbosity=normal"
```

Use `[Trait("Phase", "01")]` through "20" and change the filter per phase. Category=Http/Realtime/Extended/Fault is optional when needed for runner profiles. xUnit 2.9 fixtures return Task; do not copy xUnit v3 ValueTask signatures.

On WSL without Linux dotnet, use `'/mnt/c/Program Files/dotnet/dotnet.exe'` from the mounted checkout. Docker must be reachable by that process. SDK/container failures mean assertions were not exercised.

## Phase 1 — Establish the real API host and isolated resources

**Read:** API project/Program, test Directory.Build.props, CurrentUser, AddPersistence/AddInfrastructure and tagged factory configuration APIs.

**Files:** Create collection/fixture, sandbox, configuration, factory, host, certificate, MigrationCaller and local OTLP handler above; `Features/Shared/ApiHarnessTests.cs`.

**Interfaces:** Produce CreateSandboxAsync, StartHostAsync and the three named client factories. Migrate with a disposable setup provider before starting fresh API pools.

- [ ] **1.1** Write Host_StartsActualEntryPoint_AndServesOwnedEndpoints: anonymous GET / is HTML 200, /health/live is exactly Healthy, actual AppDbContext/ISender/security options resolve after startup. Add no test controller.
- [ ] **1.2** Implement generated resources/migrations/early configuration. Assert real IWebHostEnvironment.ContentRootPath equals the owned root and upload/staging paths remain beneath it. No file may be created under repository wwwroot.
- [ ] **1.3** Implement real HTTPS/dynamic port/pinning; assigned address is loopback HTTPS. An untrusted client fails TLS, the pinned client reaches /, and a foreign certificate fails the fixture callback. Redirects remain disabled.
- [ ] **1.4** Implement independent clients/jars and local OTLP transport; check all configured signals/named options use that transport with no ambient export headers.
- [ ] **1.5** Write Sandboxes_DoNotShareDatabaseRedisOrFiles and ReplicaHosts_ShareDurableResourcesButOwnLocalState: independent sandboxes cannot read each other's DB marker/prefix/file; related hosts share intended resources but have distinct local singleton objects.
- [ ] **1.6** Exercise cleanup after success and deliberately failed initialization; await all resources, verify DB/root/prefix cleanup while a sibling sandbox stays usable.
- [ ] **1.7** Run Phase=01 build/test. Report images and host boundary without credentials; review helpers for unnecessary abstractions.

**Gate:** Real Program runs over HTTPS with isolated dependencies. Migrated-schema startup is proven; empty-schema/bootstrap is not yet covered.

## Phase 2 — Establish routing, binding and ProblemDetails regressions

**Read:** ApiController, GlobalExceptionHandler, JwtAuthenticationInstaller, default ProblemDetails registration and `backend/test/Kahoot.Api.UnitTests/Features/Shared/ApiControllerFrameworkContractTests.cs`.

**Files:** Create `TestSupport/ProblemAssertions.cs`; `Features/Shared/{RoutingAndBindingTests,ProblemDetailsContractTests}.cs`.

**Interfaces:** Consume Phase 1. Introduce the minimal real registration/login setup from Phase 3 if needed here; do not create a fake identity or duplicate that helper later.

- [ ] **2.1** MissingProtectedIdentity_ReturnsUnauthorizedProblem: GET /api/quizzes returns 401/application/problem+json/Auth.Unauthorized, expected title/type, request-path instance and nonempty requestId. Separate status/code checks from trace-format regression.
- [ ] **2.2** BusinessNotFound_WithActiveRequestTrace_PreservesMapped404: real Host requests random quiz GUID with valid W3C traceparent; expect 404 Quiz.NotFound. Retain failure if duplicate traceId yields 500; capture sanitized exception type. Never disable tracing or replace the MVC factory.
- [ ] **2.3** ProblemTraceId_MatchesIncomingTraceId_As32HexCharacters: challenge and genuine validator failure must return the incoming 32-character TraceId. Report full Activity.Id overwrites separately from business mapping.
- [ ] **2.4** Malformed JSON/type mismatches/missing body produce framework binding 400; validly bound invalid fields reach MediatR/GlobalExceptionHandler with Validation.Failed and grouped errors. Fresh DB reads prove no persistence. The two error families need not share extensions.
- [ ] **2.5** Invalid GUID route, unsupported method and unknown route exercise actual 404/405 without handler execution. Do not require fabricated ProblemDetails for all router rejections.
- [ ] **2.6** Reserve generic 500/redaction assertions for Phase 19's real fault path; do not add a production throw endpoint.
- [ ] **2.7** Run Phase=02; show each regression/source and keep the required gate blocked while mapping/correlation is red. Independent phases remain separately runnable.

**Gate:** Binding, routing, validators, business results and infrastructure errors are distinguished without masking real framework defects.

## Phase 3 — Verify registration and login through real security

**Read:** AuthController, Register/Login records/validators/handlers, UsernameNormalization, PasswordHasher, JwtTokenGenerator and LoginRateLimiter.

**Files:** Create/complete `TestSupport/{ApiIdentity,AuthenticationData,DatabaseAssertions}.cs`; `Features/Auth/{RegistrationTests,LoginTests}.cs`.

**Interfaces:** Produce RegisterAndLoginHostAsync and SeedAndLoginAdministratorAsync. Identity holds account IDs and secret-safe generated credentials.

- [ ] **3.1** Register_CreatesHost_AndReturnsOnlyPublicAccountFields: actual username/password JSON gives 201 accountId/username, no hash/password/token fields; DB contains Host/Active/default security state and real password verification succeeds. Current register Location is empty.
- [ ] **3.2** Verify normalized/case duplicate Auth.UsernameUnavailable 409, validator failures 400 and no rows on rejection. Report Phase 2 mapped-error regressions rather than weakening assertions.
- [ ] **3.3** Login_ReturnsValidBearerAndTwoScopedCookies: assert accountId/username/accountKind/accessToken/expiresIn, validate issued JWT through installed primitives, then call a protected endpoint with it.
- [ ] **3.4** Parse distinct Set-Cookie headers: kahoot_refresh_token Secure/HttpOnly/Lax/Path=/api/auth; kahoot_csrf_token Secure/not HttpOnly/Lax/Path=/. Check configured Max-Age with elapsed tolerance and no refresh secret in JSON.
- [ ] **3.5** Unknown user/wrong password/suspended login exercise actual errors without unsafe account-existence details; no valid refresh row is issued.
- [ ] **3.6** Exercise real login limiter at an observed bounded threshold plus a second isolated identity; account for server refill, assert Request.RateLimited 429, and avoid password-load timing claims.
- [ ] **3.7** Run Phase=03. Persisted refresh secrets are hashes and no credential values appear in test output.

**Gate:** Real cryptography, issuance, cookies and protected bearer usability are exercised.

## Phase 4 — Verify JWT database checks and role authorization

**Read:** JwtAuthenticationInstaller, CurrentUser, controller metadata and User security fields.

**Files:** Create `Features/Auth/{JwtAuthenticationTests,RoleAuthorizationTests}.cs`; only named token arrangements in AuthenticationData.

**Interfaces:** Use issued identities. Special JWT fixtures use installed crypto APIs and sandbox keys, not replacement authentication.

- [ ] **4.1** Missing/malformed/wrong signature/issuer/audience/expired JWTs yield 401 Auth.Unauthorized; real Host succeeds. ClockSkew=0; arrange clearly expired claims rather than sleeping to expiry.
- [ ] **4.2** Signed tokens with invalid/missing GUID subject or invalid token_security_version are rejected using actual NameIdentifier/sub and role semantics.
- [ ] **4.3** After issuance, commit deletion/suspension/role change/security-version increment in separate scenarios. The next protected request rejects the token from DB state; assert current 401 and report the documented status difference.
- [ ] **4.4** Host cannot access Admin routes; SystemAdmin cannot access Host-only quiz/game/upload routes. Valid wrong-role identity yields 403 Auth.Forbidden; Admin is not an implicit superuser.
- [ ] **4.5** Verify AllowAnonymous for auth/join/join-info/downloads while preserving each endpoint's own guards. A failed JWT on an anonymous endpoint differs from mandatory authorization.
- [ ] **4.6** Concurrent requests with distinct identities persist correct ownership/audit callers. Never mutate shared DefaultRequestHeaders or global CurrentUser.
- [ ] **4.7** Run Phase=04; report correlation regressions separately and audit token-fixture diagnostics.

**Gate:** Actual middleware and database security state govern requests.

## Phase 5 — Verify refresh cookies, CSRF, Origin and rotation

**Read:** AuthController refresh/CSRF/cookie helpers, RefreshRequest/handler, AuthErrors and RefreshTokenOptions.

**Files:** Create `Features/Auth/{RefreshTests,RefreshCsrfAndOriginTests,RefreshConcurrencyTests}.cs`; `TestSupport/{PostgresRequestGate,AsyncGate}.cs` for actual concurrency.

**Interfaces:** Consume login/cookie clients and fresh token snapshots. SQL gates execute real SQL and expose bounded waits/release.

- [ ] **5.1** Successful cookie refresh with exact CSRF header/allowed Origin yields 200, usable new bearer and replacement cookies; DB shows one consumed parent and one replacement, JSON excludes refresh secrets.
- [ ] **5.2** Missing/mismatch/blank/multiple CSRF header, missing/mismatching CSRF cookie and disallowed authority reject with Auth.Forbidden 403 without consuming the row. Cookie mode requires Origin or Referer.
- [ ] **5.3** Cover request authority/configured allowlist, scheme/host/port distinctions, userinfo/attacker suffix/trailing slash, Origin precedence and valid Referer path fallback. Raw Cookie headers are appropriate where CookieContainer normalizes malformed values.
- [ ] **5.4** Body-token mode has no refresh cookie, still requires exactly one nonblank CSRF header, permits absent Origin without a CSRF cookie, and validates supplied Origin. Cover optional/empty body binding. Cookie presence wins over a valid body token, including an empty refresh cookie.
- [ ] **5.5** Missing/malformed/expired/revoked tokens produce Auth.InvalidRefreshToken 401 and no credentials; distinguish model binding from handler token errors.
- [ ] **5.6** Establish two refresh requests behind a real external user-row lock, observe waits then release. Expect one rotation and immediate consumed-token Auth.RefreshRace 409 within ten-second grace. Arrange the parent's actual RotatedAt timestamp beyond grace, retry HTTP and assert Auth.RefreshTokenReuse 401/family revocation. Do not invent a ConsumedAt field, sleep ten seconds or accept race/reuse interchangeably.
- [ ] **5.7** Run Phase=05 with independent jars/clients. This proves server handling; HttpClient does not enforce browser SameSite/CORS.

**Gate:** Mode selection, CSRF/Origin, rotation and durable replay are exercised.

## Phase 6 — Verify logout, logout-all and password changes

**Read:** AuthController actions/handlers, account-security lock and token/security-version mutations.

**Files:** Create `Features/Auth/{LogoutTests,LogoutAllTests,ChangePasswordTests}.cs`.

**Interfaces:** Consume real sessions, jars and fresh DB reads.

- [ ] **6.1** Valid cookie logout yields 204/empty body, actual handler revocation scope and deletion cookies with matching paths/security attributes.
- [ ] **6.2** Logout reads the refresh cookie only; test actual no-cookie/header and cookie CSRF/Origin rules. Invent no body-token logout contract.
- [ ] **6.3** Create two sessions, logout one and verify the other according to handler scope. Logout-all with valid bearer clears cookies, revokes all refresh families and invalidates older bearer security versions.
- [ ] **6.4** Verify Host/SystemAdmin access to protected logout-all/password actions and their actual bearer boundary; these actions do not reuse refresh's CSRF guard.
- [ ] **6.5** Actual currentPassword/newPassword change yields 204, new login succeeds, old password fails and previous refresh/bearer credentials become invalid. Wrong current/invalid replacement preserve security state.
- [ ] **6.6** Repeat appropriate operations and assert source-defined idempotency without duplicate mutation; newly issued post-change credentials still reach a protected endpoint.
- [ ] **6.7** Run Phase=06. Live Host socket eviction belongs to Phase 17 and cannot be inferred from DB revocation alone.

**Gate:** HTTP credential/session changes affect subsequent real authentication.

## Phase 7 — Verify quiz/question contracts and ownership

**Read:** QuizzesController, slice records/validators/handlers, QuizErrors and keyset cursor.

**Files:** Create `Features/Quizzes/{QuizCrudTests,QuizListTests,QuestionMutationTests,QuizOwnershipTests}.cs`.

**Interfaces:** Two real Host identities; explicit request JSON and immutable returned IDs.

- [ ] **7.1** Create/get/update/delete metadata: initial revision 1, incremented revision, timestamps/ownership, create Location resolves to owner GET, delete 204 has no body.
- [ ] **7.2** List defaults/pageSize/cursor/items/nextCursor/hasMore using two pages. Verify source order and isolation; invalid cursors use real validator/errors.
- [ ] **7.3** Add/update/delete/reorder with text/imageId/durationSeconds/basePoints/choices[{text,isCorrect}]. Verify fresh GET data, order, reorder message/revision, exact-set rejection and atomic preservation.
- [ ] **7.4** Assert AddQuestion 201/Location and record absent single-question GET. Author-facing quiz data can contain correctness; player payload secrecy is a separate boundary.
- [ ] **7.5** Cross-owner/missing IDs yield Quiz.NotFound/Quiz.QuestionNotFound as implemented; no foreign mutation/content leakage.
- [ ] **7.6** Actual game usage causes active Quiz.InUse and finished Quiz.HasSessions deletion protection. Use Phase 12's real uploaded images for ownership/reference scenarios once available.
- [ ] **7.7** Run Phase=07; report absent expectedRevision instead of claiming stale-edit enforcement or sending invented concurrency fields.

**Gate:** All nine actions have deliberate binding, ownership, serialization and persistence coverage.

## Phase 8 — Verify administration, revisions and pending termination

**Read:** Admin controllers, Features/Admin, AccountErrors, suspension channel/finalizer and seeder.

**Files:** Create `Features/Administration/{UserAdministrationTests,AdministratorAdministrationTests,SuspensionResponseTests}.cs`.

**Interfaces:** Real SystemAdmin/Host identities, current DB revision snapshots and SQL gate for finalization ordering.

- [ ] **8.1** Verify list pagination/username/status and get account fields: accountId/username/accountKind/status/createdAt/statusChangedAt/revision/terminationPending. HTTP enum values are Host/SystemAdmin and Active/Suspended.
- [ ] **8.2** Current-revision suspend/reactivate yields 204/durable security changes; stale revision yields Account.ConcurrentModification 409 without mutation.
- [ ] **8.3** Hold a relevant game lock while actual suspension commits/enqueues. Assert suspend's 202 {terminationPending:true} branch and independently observe worker state; no-game case covers 204. Release in finally; do not assume all suspensions yield 202.
- [ ] **8.4** While genuine finalization is pending, reactivation yields Account.TerminationPending; after observed completion/current revision it succeeds. Keep the worker running.
- [ ] **8.5** Exercise administrator list/create/suspend/reactivate with actual bodies; create 201 currently has empty Location. Verify hashed credential and subsequent real login, with no secret leakage.
- [ ] **8.6** Assert sole-active-admin Account.LastAdministrator, missing/wrong-kind Account.NotFound, duplicate Account.Conflict and Host/anonymous role rejection. Arrange necessary locks explicitly.
- [ ] **8.7** Run Phase=08; report immediate response versus actual finalization separately. Socket effects are Phase 17.

**Gate:** All eight actions, enum contracts, revision checks and pending responses use real workers.

## Phase 9 — Verify game creation, controls, queries and reports

**Read:** GamesController Host actions, lifecycle DTOs/handlers, idempotency and game errors.

**Files:** Create `TestSupport/GameData.cs`; `Features/Games/{GameCreationTests,GameLifecycleTests,GameCommandReplayTests,GameReadContractTests}.cs`.

**Interfaces:** Produce CreateLobbyAsync through HTTP. Introduce the minimal real Host SignalR attachment helper from Phase 14 here if needed for abandonment prevention; later expand it rather than duplicate it.

- [ ] **9.1** Create by quizId: 201 gameId/pin/joinUrl/title/status/stateVersion/totalQuestions/createdAt, owner Location GET, immutable snapshot and configured client URL. Foreign/missing/empty quiz cases use actual errors.
- [ ] **9.2** Keep Host attached; traverse two questions through start/end-question/show-leaderboard/advance/results/end. Assert each operation's own response fields/stateVersion and fresh DB state, not a guessed universal transition DTO.
- [ ] **9.3** Replay identical commandId/operation/expectedStateVersion: original response, one durable transition. Different parameters/operation with reused ID yields Validation.Failed; new ID with stale version yields Game.ConcurrentModification.
- [ ] **9.4** Invalid transitions/no-more-questions/archive/cross-owner failures leave state/version/idempotency unchanged.
- [ ] **9.5** Verify overview/report phase contracts, adding real Phase 11 answers once available. Finished report follows snapshots/materialized scores despite later quiz changes.
- [ ] **9.6** Participants includeRemoved/limit/cursor contract includes gameId/presenceVersion/reservedParticipantCount/participants/nextCursor. This cursor is nullable integer seat index, not quiz keyset token. DELETE removal returns 204/durable removal.
- [ ] **9.7** Run Phase=09; retain real Host presence and workers. Do not disable abandonment globally for stability.

**Gate:** Host HTTP controls, replay, ownership and durable lifecycle are exercised.

## Phase 10 — Verify anonymous admission and lobby information

**Read:** Join/GetJoinInfo, actual validators/handler/recovery, limiter and token/seat constraints.

**Files:** Create `Features/Games/{JoinInfoTests,LobbyJoinTests,LobbyJoinRecoveryTests}.cs`.

**Interfaces:** Real lobby and independent anonymous clients; actual UUID-v4 joinOperationIds.

- [ ] **10.1** Anonymous join info returns gameId/title/status/participantCount/maxCapacity/isFull without owner/token/answer data.
- [ ] **10.2** Join pin/nickname/joinOperationId returns participantId/playerSessionToken/gameId/nickname/title/seatNumber and durable seat/hash. Token prefix pst_ and length 68 are exact; never print it.
- [ ] **10.3** Invalid PIN/nickname/scalars/non-v4 operation, normalized nickname collision, non-lobby and full game use actual errors with unchanged admission counts on failure.
- [ ] **10.4** Replay an operation and assert implemented recovery/no second seat or token mutation. An empty one-time token in an existing-operation response is an explicit contract observation, not permission to reissue the secret.
- [ ] **10.5** Exercise bounded real RemoteIpAddress limiter behavior; forwarded trust is Phase 13. Do not pretend arbitrary X-Forwarded-For is trusted.
- [ ] **10.6** Compare REST reserved admission to actual hub JoinGame attachment. REST alone is not a connected player/group event.
- [ ] **10.7** Run Phase=10. Large capacity bursts/races remain companion/system evidence.

**Gate:** Real anonymous admission/validation/recovery is distinct from live connection state.

## Phase 11 — Verify REST answers, token guards and secrecy

**Read:** SubmitAnswer controller/handler/validator, token lookup, limiter and result/rank materializers.

**Files:** Create `Features/Games/{AnswerTokenTests,AnswerSubmissionTests,AnswerRemovalTests}.cs`.

**Interfaces:** Real started game/multiple participants; body contains only questionId/choiceIds, identity comes from session token.

- [ ] **11.1** Valid X-Session-Token/current snapshot choices yield 200 accepted/alreadyAnswered and one answer/count/score update. JSON excludes correctness/points/totalScore/token/internals before reveal.
- [ ] **11.2** Missing/wrong length/prefix/hash/revoked/foreign-game tokens reject with Game.InvalidSessionToken. First nonblank X-Session-Token wins and is not trimmed; Bearer pst_ fallback trims and recognizes Bearer case-insensitively. Keep JWT middleware active on the anonymous action.
- [ ] **11.3** Replay accepted answer: accepted=true/alreadyAnswered=true, unchanged durable counts/scores and one row. Verify committed-answer retrieval around real limiter rejection where implemented.
- [ ] **11.4** Wrong question/foreign or invalid choice sets/PostgreSQL-expired deadline reject without partial state. Change authoritative DB timestamp explicitly, not just UTC provider.
- [ ] **11.5** Owner removes participant through DELETE; subsequent REST answer rejects revoked token at controller lookup. Do not demand hub Game.ParticipantRemoved precedence from this earlier REST guard.
- [ ] **11.6** All eligible answers or removal of last unanswered eligible player triggers equality auto-close/materialization. Verify results/counts/report from fresh reads; use two players to test an answer before automatic reveal.
- [ ] **11.7** Run Phase=11. Controller lookup does not filter ExpiresAt; verify overall expired/finished behavior through handler and report discrepancies without adding a guard.

**Gate:** REST player identity, replay, secrecy and durable answer effects are exercised.

## Phase 12 — Verify multipart uploads and anonymous files

**Read:** ImagesController, upload pipeline, ImageStorageService/options and reference/cleanup rules.

**Files:** Create `TestSupport/ImageSamples.cs`; `Features/Images/{ImageUploadTests,UploadBoundaryTests,ImageDownloadTests}.cs`.

**Interfaces:** Legitimate JPEG/PNG/WebP samples, actual MultipartFormDataContent and owned storage.

- [ ] **12.1** Host uploads file field: 201 imageId/url/Location, real owner metadata/sanitized file; anonymous URL download returns decodable stored bytes.
- [ ] **12.2** No/wrong/duplicate/empty file, corrupt/unsupported content and wrong identity reject with exact source errors; no orphan metadata/final file on rejection.
- [ ] **12.3** Verify 5,242,880-byte file and 5,308,416-byte total-request limits including multipart overhead. Use decodable fixtures to isolate size from codec errors; measure wire body. Assert Kestrel limits and mapped Image.TooLarge where action/handler owns it; distinguish an earlier server rejection if reproduced.
- [ ] **12.4** Exercise actual form limits: 2,048-byte headers, one value, 1,024-byte value; malformed parser failures use observed/source-defined semantics. Attributes alone are not runtime evidence.
- [ ] **12.5** Download asserts Content-Type/sanitized bytes/nosniff and public, max-age=31536000, immutable. Missing file and /uploads map Image.NotFound. Do not invent ETag/range requirements.
- [ ] **12.6** Invalid GUID/extensions/case and encoded slash/backslash/traversal/nesting cannot escape uploads. Confirm raw target reached Kestrel; HttpClient/Uri may normalize it. A narrow BCL TLS HTTP/1.1 sender is allowed when raw-target evidence requires it, using fixture certificate pinning.
- [ ] **12.7** Attach uploaded image to a question through HTTP, verify owner/single-question constraints and continuity under snapshot references. Run Phase=12; exhaustive codec/cleanup races stay in Infrastructure.

**Gate:** Actual form binding, wire limits, storage and secure streaming are exercised.

## Phase 13 — Verify CORS and forwarded trust

**Read:** CorsInstaller, Program forwarding/options/order, refresh authority and limiter keys.

**Files:** Create `Features/Security/{CorsTests,ForwardedHeadersTests}.cs`; named generated host profiles only.

**Interfaces:** Real HTTP clients and factory early settings; no global proxy/environment mutation.

- [ ] **13.1** OPTIONS preflight verifies exact allowed origin, credentials, method/header acceptance and no wildcard. Disallowed origin gets no allow-origin; HTTP clients do not enforce browser rejection.
- [ ] **13.2** Verify CORS headers on actual success and auth/error responses to establish middleware order and early capture.
- [ ] **13.3** Inspect effective KnownProxies/KnownIPNetworks and ForwardLimit=1. Program retains default loopback trust; empty configured networks are not an untrusted profile. Use an explicit dedicated test-host options profile making loopback untrusted for negative tests.
- [ ] **13.4** Trusted-peer For/Proto affect real address/authority decisions and one-hop processing; the explicit untrusted peer profile ignores them.
- [ ] **13.5** X-Forwarded-Host is not enabled and cannot rewrite Request.Host; verify consequent same-origin CSRF decisions without inventing proxy policy.
- [ ] **13.6** Invalid configured CIDR fails actual startup only in a dedicated profile, without changing normal security settings.
- [ ] **13.7** Run Phase=13; report server middleware evidence separately from deployed Nginx/browser behavior.

**Gate:** Trust and CORS are established through actual middleware, not localhost assumptions.

## Phase 14 — Verify SignalR transport, envelopes and attachment

**Read:** MapHub, RealtimeInstaller, GameHub/Filter and UnauthenticatedSocketGuard.

**Files:** Create/extend `TestSupport/{SignalRConnection,SignalREvents}.cs`; `Realtime/{HubTransportTests,HostAttachmentTests,PlayerAttachmentTests,UnauthenticatedSocketTests}.cs`.

**Interfaces:** ConnectAsync/InvokeAsync and bounded events/Closed. Extend any helper introduced in Phase 9.

- [ ] **14.1** Real HTTPS negotiation/WebSocket upgrade succeeds; advertised usable transport is WebSockets and LongPolling is unavailable. Keep production size/timeout settings.
- [ ] **14.2** Real owner JWT JoinAsHost(game GUID string) yields success/data gameId/connected/error=null. Invalid GUID is nonterminal validation; anonymous/wrong-role/cross-owner yields Auth.Forbidden and actual termination.
- [ ] **14.3** Anonymous JoinGame(pin,nickname,operation string) yields actual envelope/session fields and connected presence. Invalid operation/duplicate attachment exercise real validation.
- [ ] **14.4** Host cannot become Player, Player cannot become Host, and attached Host cannot change games. Arbitrary group names cannot be requested; an unknown method never joins a group.
- [ ] **14.5** Invalid Reconnect/SubmitAnswer before attachment return actual failures. A test-owned corrupted catch-up invariant yields real-filter Server.InternalError without SQL/stack details; cancellation remains distinct.
- [ ] **14.6** An unattached socket closes after production 15-second admission deadline in a bounded Extended test. An attached peer stays usable; JWT connection alone is not attachment.
- [ ] **14.7** Frame beyond actual 32 KiB receive limit is rejected/closed without state mutation. Count encoded protocol bytes. Run Phase=14 and record actual close behavior; documented 4403 requires real-frame evidence.

**Gate:** Real protocol/transport/auth/attachment/admission are exercised.

## Phase 15 — Verify broadcasts, personal events and audiences

**Read:** GameNotificationService/GameDtos, lifecycle materializers and server-derived groups.

**Files:** Create `Realtime/{GameBroadcastTests,PersonalResultTests,AudienceIsolationTests,PresenceEventTests}.cs`.

**Interfaces:** Attached owner/two players/foreign game, callbacks registered before commands.

- [ ] **15.1** HTTP start yields player QuestionStarted versus host QuestionStartedForHost. Verify real IDs/version/index/deadline fields; player choices omit isCorrect, host gets implemented correctness/basePoints.
- [ ] **15.2** Actual answers/end yield QuestionEnded and participant-only PersonalQuestionResult, with committed submitted/isCorrect/pointsAwarded/totalScore.
- [ ] **15.3** Leaderboard/finish yield LeaderboardUpdated/PersonalLeaderboardUpdated and GameEnded/PersonalGameEnded; verify materialized ranks/podium. Do not guess personal event names.
- [ ] **15.4** Personal payloads identify only the intended player; foreign-game owner/player see no matching events during a bounded interval after positive delivery.
- [ ] **15.5** Observe ParticipantPresenceChanged on connect/disconnect/reconnect; distinguish stateVersion/presenceVersion and reserved/connected counts. REST admission alone is not live presence.
- [ ] **15.6** Received event corresponds to committed DB state read separately. Replay yields one durable transition; assert exact event deduplication only where dispatch guarantees it. Pub/Sub is not exactly-once/replay.
- [ ] **15.7** Run Phase=15; record observed bounded delivery, not production latency percentiles.

**Gate:** Actual network audience isolation and pre-reveal serialization are proven.

## Phase 16 — Verify reconnect recovery, deadlines and fencing

**Read:** GameHub Reconnect/BuildCatchUpStateAsync, GameDtos, ordered locks/generation/eviction.

**Files:** Create `Realtime/{ReconnectLobbyTests,ReconnectGameplayTests,ReconnectResultsTests,ReconnectFencingTests,ReconnectExpiryTests}.cs`.

**Interfaces:** Real admitted sessions and independent replacement connections; explicit DB timestamps for expiry arrangements.

- [ ] **16.1** Lobby recovery data uses status, not stage: LOBBY/gameId/stateVersion/title/nickname/seatNumber/totalParticipants. Seat/token identity remains stable.
- [ ] **16.2** QUESTION_ACTIVE includes questionIndex/totalQuestions/questionText/imageUrl/deadlineUtc/remainingSeconds/alreadyAnswered/choices[{id,text,orderIndex}]/totalScore. Index is zero-based; provisional current points and isCorrect are absent.
- [ ] **16.3** QUESTION_RESULTS includes actual submitted/isCorrect/pointsAwarded/selectedChoiceIds/correctChoiceIds/counts/totalScore for answered and unanswered players, including question image.
- [ ] **16.4** LEADERBOARD includes topParticipants/rank/totalScore; FINISHED podium/acceptedAnswers. Missing materialized rank/snapshot/deadline fails explicitly rather than fabricating defaults.
- [ ] **16.5** Replacement increments durable generation and fences old socket; only replacement acts. Same-socket same-player retry follows actual success behavior/no extra seat; a different token on attached socket is rejected.
- [ ] **16.6** Verify removed-before-revoked Game.ParticipantRemoved, inactive Host Game.Unavailable and invalid/expired tokens, including finished 24-hour window. Use PG timestamps with comfortable margins; exact boundary needs lock/clock observations.
- [ ] **16.7** Block reconnect with external game lock, commit lifecycle transition then release; catch-up must reflect one committed phase/version. Run Phase=16.

**Gate:** Lost-event recovery, stable identity, secrecy and durable fencing use the actual hub.

## Phase 17 — Verify two API instances and Redis recovery

**Read:** Backplane, both subscribers/heartbeat workers and notification/presence startup/sweep signals.

**Files:** Create `Realtime/{BackplaneDeliveryTests,CrossInstanceEvictionTests,PresenceRecoveryTests}.cs`; extend named replica ownership only.

**Interfaces:** Complete hosts share DB/prefix/root/security but own sockets/singletons. Trigger behavior through requests/hub calls.

- [ ] **17.1** Owner/player on A, player on B: command at A reaches B with correct committed payload/version through actual Redis.
- [ ] **17.2** Live second sandbox/prefix receives no first-sandbox event after positive delivery is observed.
- [ ] **17.3** Reconnect player to B; observe generation and old A eviction, replacement answers successfully, stale A cannot write even if abort races invocation.
- [ ] **17.4** HTTP suspend/remove on one instance evicts remote Host/player. ParticipantRemoved carries committed stateVersion before implemented abort when delivery succeeds.
- [ ] **17.5** Password/logout-all invalidation closes remote invalid Host via real DB checks/heartbeats; observe bounded actual worker effects.
- [ ] **17.6** Commit direct DB invalidation without publication as an explicit missed-message arrangement; real ten-second sweep closes stale socket. Label this omission, not Redis outage.
- [ ] **17.7** Run Phase=17, observe normal lease/count cleanup. Do not claim durable Pub/Sub replay, large-scale load or guaranteed delivery under Redis outage.

**Gate:** Actual scale-out delivery/eviction and recovery are established locally with two hosts.

## Phase 18 — Verify health and readiness privacy

**Read:** Four health checks, routes, actual critical-worker tracker/lifecycle.

**Files:** Create `HealthChecks/{HealthEndpointTests,ReadinessDependencyTests,ReadinessWorkerTests}.cs`; fault resources and `TestSupport/UtcTimeProvider.cs` only when first needed.

**Interfaces:** Real started host/probes; dedicated dependency containers. UTC-only override retains real timers.

- [ ] **18.1** After ApplicationStarted, live/ready/alias return exactly plain-text Healthy 200 without diagnostic JSON. Liveness executes no dependency probes.
- [ ] **18.2** Stop dedicated DB or Redis; after three-second cache expiry observe actual Unhealthy 503 ready/alias while live stays Healthy. Cached Healthy is not a fresh-probe result.
- [ ] **18.3** Portable owned storage failure, such as replacing staging with a file after startup, yields Degraded 503 without leftover health files. Restore in finally; do not assume chmod on Windows mounts works.
- [ ] **18.4** In a dedicated UTC profile, actual ICriticalWorkerFailureTracker.ReportFailure(string, Exception), advance UTC beyond 15 minutes and probe; assert Degraded. ReportSuccess and subsequent cache renewal recover. This direct tracker arrangement is not proof of every worker's failure path.
- [ ] **18.5** Concurrent real probes under held dependency operation complete within a generous bounded runner deadline around the 450 ms probe/gate budget. Record timing separately from percentile SLOs.
- [ ] **18.6** Bodies expose no connection strings/file paths/exceptions/worker identifiers. Stopping-state ready is Phase 19 while listener is deliberately still active.
- [ ] **18.7** Run Phase=18; report continuous-failure evidence separately from backlog-age guarantees.

**Gate:** Actual probe routing/cache/degradation/privacy are established.

## Phase 19 — Verify faults, cancellation, empty startup and drain

**Read:** Exception handler, migration/seeder, Program admission/Stopping/ShutdownTimeout and lifecycle.

**Files:** Create `TestSupport/{FaultSandbox,DrainGate,SchemaMode,WebSocketObservation}.cs` if not introduced earlier; `Features/Shared/{DependencyFailureTests,RequestCancellationTests,StartupTests,ShutdownTests}.cs`; `Realtime/ShutdownConnectionTests.cs`.

**Interfaces:** Dedicated fault resources, external SQL gate and IHostedLifecycleService drain gate. Add explicit Empty schema mode; default remains migrated.

- [ ] **19.1** Hold every connection in a dedicated small production pool after startup; real request times out with 503 Database.PoolExhausted/Retry-After: 5/redacted problem. Observer uses an independent pool; release in finally.
- [ ] **19.2** Stop dedicated DB/Redis during feature requests; assert exact path's public error, no partial write and recovery after readiness. Do not assume every Redis exception maps Service.Unavailable or auto-retries.
- [ ] **19.3** Lock real Host/game row, prove request blocked with pg_blocking_pids, cancel client and observe server RequestAborted/transaction completion through narrow test-only observation that still executes SQL. State stays unchanged and next request succeeds. Client cancellation alone is insufficient.
- [ ] **19.4** Actual Program starts on Empty DB with real bootstrap enabled; verify migrations/seeding and SystemAdmin HTTP login using host-pool enum reads. Retain metadata/seeder regressions; do not pre-migrate or silently refresh production metadata.
- [ ] **19.5** Invalid configuration, unreachable owned database and root-initialization failure fail closed with sanitized diagnostics/complete cleanup. Use bounded runner startup deadlines; do not change normal retry defaults.
- [ ] **19.6** Register the test-only DrainGate as IHostedLifecycleService and gate StoppingAsync before hosted StopAsync. Admit a mutation before shutdown and prove its SQL wait on an external lock. Call real StopApplication first, observe its token plus StoppingEntered, then send ready/non-health probes while Kestrel still listens: 503, with Retry-After: 5 on admission rejection. Release the SQL lock and observe the already-admitted request's actual completion/persistence before releasing the drain gate. Release both gates in finally and await shutdown. See [Generic Host lifecycle order](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs). Never assert new connections work after listener shutdown.
- [ ] **19.7** Observe Host/Player/unattached socket drain and actual close frames using WebSocketObservation, not only HubConnection.Closed. Keep documented 1001/4403 requirement regressions where unmet. OnDisconnected during stopping skips normal membership cleanup, so leases may recover by TTL; do not require immediate zero Redis keys as a shutdown guarantee. Await workers/subscriptions/disposal and report shutdown against configured 30 seconds, identifying artificial drain-gate time separately. Run Phase=19.

**Gate:** Isolated faults, real rollback/cancellation, empty startup and drain have evidence or explicit defects.

## Phase 20 — Verify telemetry privacy and review coverage

**Read:** Observability/scrubber/error writers, home endpoint, Development OpenAPI and route metadata.

**Files:** Create `TestSupport/TelemetryCapture.cs`; `Features/Shared/{TelemetryPrivacyTests,EnvironmentEndpointTests,RouteCoverageTests}.cs`.

**Interfaces:** Installed SDK exporters/processors create immutable Activity/log snapshots; retain production filters/tracing and local export transport.

- [ ] **20.1** Real hub access_token query JWT still permits JoinAsHost after scrubber. Logs/exported server spans contain no raw token/query or disallowed URL/user-agent/server-address tags. Leak assertions report booleans, never leaked values.
- [ ] **20.2** Non-hub access_token and repeated query values do not grant accidental auth or leak. Any Items/query observation is a narrow test-only observer, not a debug endpoint.
- [ ] **20.3** Exported spans retain useful route/status/trace correlation; real health instrumentation is filtered. ASP.NET hosting activities differ from exported server spans, so do not require no Activity at all.
- [ ] **20.4** Capture Phase 19 fault logs/responses for tokens/passwords/keys/connection credentials/SQL parameters/payload leaks. Honor filters; no ClearProviders or sensitive EF logging.
- [ ] **20.5** Home HTML matches environment behavior; actual OpenAPI/Scalar paths are present only in a separate Development host, absent in Testing. Inspect representative real schema/security/enum contracts without inventing unspecified properties.
- [ ] **20.6** Explicit source-derived manifest maps all 39 actions plus home/health/hub to concrete scenarios. Public metadata reflection is allowed for audit, not runtime-generated generic behavior tests. Record Location and other gaps.
- [ ] **20.7** Run all implemented phases/profiles, targeted format and final diff review. Report passing contracts, failing regressions, unavailable runtime and separate release evidence.

**Gate:** Production telemetry remains enabled/isolated/secret-safe; every actual route has deliberate coverage and honest status.

## Final verification and completion reports

From `backend`, with working Docker and SDK:

```bash
dotnet build test/Kahoot.Api.IntegrationTests/Kahoot.Api.IntegrationTests.csproj --configuration Release
dotnet test test/Kahoot.Api.IntegrationTests/Kahoot.Api.IntegrationTests.csproj --configuration Release --no-build --logger "console;verbosity=normal"
dotnet format test/Kahoot.Api.IntegrationTests/Kahoot.Api.IntegrationTests.csproj --verify-no-changes --no-restore
```

From repository root:

```bash
git -c core.whitespace=cr-at-eol diff --check -- backend/test/Kahoot.Api.IntegrationTests
git diff -- backend/test/Kahoot.Api.IntegrationTests
```

Inspect untracked authored files too; git diff omits them. Do not stage/commit unrelated work. Build/format does not prove HTTP/DB/Redis/WebSocket assertions ran.

For each phase report: files and helper responsibilities; exact commands/profile/filter/outcomes; actual HTTP/hub/DB/delivery evidence; required-contract regressions with sanitized source reproduction; environment limitations and gate status (passed, blocked by defect, or not executed).

Separate release work remains: deployed ingress/browser behavior; unmet close-code contracts; large fan-out/slow consumers; production latency percentiles; replica restart/Redis partition/load; four-hour soak and DR/image-volume reconciliation. Use the actual verification profiles; never extrapolate from local correctness tests.

## Ready-to-use Gemini implementation prompt

> Implement only Phase N from `docs/superpowers/plans/2026-10-01-api-integration-tests.md` in `backend/test/Kahoot.Api.IntegrationTests`. Read repository/OpenWolf instructions, actual phase sources, prior helper contracts and installed versions first. Preserve Clean Architecture/vertical slices, the single API reference and production behavior.
>
> Use actual Program through WebApplicationFactory with early generated configuration and real loopback HTTPS Kestrel, PostgreSQL, Redis, auth and workers. Separate setup/read scopes. No fake authentication/ISender/DbSets/hubs/backplane/storage, new packages, public Program/friend attributes, invented fields, skipped regressions or production fixes.
>
> Write readable tests and only needed helpers. Use bounded async observations and real SQL blockers; protect secrets and cleanup resources. Keep ProblemDetails/trace/close/startup defects visible with evidence. Never weaken the harness to hide a valid failing contract.
>
> Run scoped phase checks, inspect tracked/untracked diff, check off only completed steps and report exact outcomes/gate status. Stop at Phase N. Do not implement another phase, change deployment configuration or automatically commit.
