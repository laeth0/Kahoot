---
description: session handoff, regenerate with /handoff when a quest finishes
budget_tokens: 1000
---
# STATUS — kahoot

> Last updated: 2026-10-01

## Done

- **API Integration-Test Plan (2026-10-01)**:
  - Saved `docs/superpowers/plans/2026-10-01-api-integration-tests.md`: twenty phases and 140 actionable steps covering all 39 controller actions, real HTTPS/WebSocket hosting, authentication/CSRF/cookies, uploads, two-host Redis delivery/eviction, reconnect, readiness, faults, startup/drain and telemetry.
  - Uses the existing API reference/packages, actual Program through a public ApiController assembly marker, early generated host configuration and owned DB/prefix/root/certificate resources. No production visibility changes; anonymous MigrationCaller exists only in the proposed migration preparation provider.
  - Source-defined ProblemDetails/trace, close-code, quiz stale-edit and fresh-schema startup gaps remain explicit regression/reporting boundaries. Planning only; no test, production, project or package changes authored. Static document/source/link checks completed; no containers/build/runtime tests executed.

- **Infrastructure Integration-Test Plan (2026-10-01)**:
  - Saved `docs/superpowers/plans/2026-10-01-infrastructure-integration-tests.md`: eighteen phases covering migrated PostgreSQL mappings/constraints/locks, startup coordination, persisted idempotency, Redis Lua/leases/Pub/Sub, heartbeats, real image storage, cleanup/finalization workers and dedicated dependency faults.
  - Defined isolated database/prefix/root ownership, independent replica providers, timer/SQL gates and explicit transport/system evidence boundaries. The only proposed production testability change is a narrow Infrastructure integration friend attribute when Phase 6 is executed; it was not added during planning.
  - Planning only; no test/production/package changes. Validated phase sequence, 125 checklist steps, nine local links, foundational source paths, scaffold/package boundaries and document whitespace. No containers, build or runtime tests were executed.

- **Application Integration Testing Implementation (2026-10-01)**:
  - Implemented all sixteen phases specified in `docs/superpowers/plans/2026-10-01-application-integration-tests.md` under `backend/test/Kahoot.Application.IntegrationTests`: 133 passing xUnit integration tests (0 failed, 0 skipped) across 21 test classes with real PostgreSQL 17 (migrated schema per test sandbox) and Redis 7.4.11-alpine (isolated prefix per test) via Testcontainers.
  - Test suites: Common/Pipeline (ValidationBehavior, PipelineShortCircuit, ConcurrentScopeIsolation), Auth (Register, Login, Refresh, Logout, ChangePassword), Quizzes (Quizzes, Questions, Choices, Images), Games (CreateGame, JoinGame, Gameplay lifecycle, AnswerSubmission, QuestionResults, Leaderboards, ParticipantRemoval), Administration (HostAccountManagement, AdministratorManagement, AdministrationQueries, BootstrapSeeder), Images (ImageUploadPersistence, ImageUploadCompensation), Concurrency (AuthConcurrency, QuizSnapshotConcurrency, AdministratorConcurrency, LobbyConcurrency, GameplayConcurrency), Reliability (TransactionRollback, Cancellation, PostCommitFailure).
  - TestSupport harness: `DatabaseSandbox` (PostgreSQL `kahoot_it_<32 hex>` with `MigrateAsync`), `ApplicationTestHarness`, `ApplicationRequestScope`, `ScopeConcurrencyGate`, `TransactionCommitInterceptor`, `SaveChangesGateInterceptor`, `PostgresLockObserver` (deterministic `SELECT unnest(pg_blocking_pids())` without arbitrary thread sleeps), `RecordingGameNotificationService`, `RecordingSocketEvictionService`, `TemporaryImageStorage`, `FaultInjectionAppDbContext`, `CommitFailureInterceptor`.
  - Zero `var` statements used across all test and test support files. Zero modifications to production source code (`backend/src/`). No new third-party packages or testing mock libraries introduced. Full Release solution build (`dotnet build backend/Kahoot.slnx -c Release`), code formatting verification (`dotnet format backend/Kahoot.slnx --verify-no-changes`), and entire unit test suite (867 tests) passed cleanly.

- **Application Integration-Test Plan (2026-10-01)**:
  - Saved `docs/superpowers/plans/2026-10-01-application-integration-tests.md`: sixteen phases covering actual MediatR slices, real PostgreSQL/Redis, isolated databases/prefixes, fresh request/read scopes, focused helpers, clean assertions, deterministic races, image compensation and rollback/cancellation.
  - Project/package boundaries preserved; no API host, workers, new dependencies or production testability changes are planned. Explicit source/spec gaps include missing quiz expected-revision enforcement and distinctions between handler persistence and transport/worker evidence.
  - Planning only; no Application integration or production code authored. Checked phase structure, links, foundational source paths, all 37 handler families, current project references/packages and whitespace. Runtime tests/build were not executed for this documentation task.

- **API Unit Testing Implementation (2026-10-01)**:
  - Implemented all ten phases specified in `docs/superpowers/plans/2026-10-01-api-unit-tests.md` under `backend/test/Kahoot.Api.UnitTests`: 280 passing xUnit tests (and 1 documented framework regression test) across 15 test classes covering Shared errors, Middleware, Authentication, Quizzes, Administration, Games, Images, ServiceCollectionExtensions, and HealthChecks.
  - Phase 1: `ApiControllerTests` (mapping error types to HTTP status codes, titles, RFC 7807 type URI, requestId, Activity correlation), `ApiControllerFrameworkContractTests` (DefaultMvcFactory contract check and documented framework traceId duplicate-key regression test), `CurrentUserTests` (ClaimTypes.NameIdentifier vs sub, Role vs role, IsAuthenticated).
  - Phase 2: `GlobalExceptionHandlerTests` (FluentValidation failures grouped by property, upload 413 size boundary mapping, password hashing 429 backoff, Npgsql connection pool exhaustion 503 with SQLSTATE 53300 or pool message and Retry-After: 5 header, pool exhaustion vs timeout precedence, transient db failures, generic 500 redaction, Activity correlation) using `RecordingLogger<T>`.
  - Phase 3: `AccessTokenScrubberMiddlewareTests` (SignalR hub path `/hubs/game` query redaction + retention in `Items["access_token"]`, non-hub path redaction without retention, repeated tokens, case-insensitivity, absent/empty tokens, downstream exception propagation).
  - Phase 4: `AuthControllerCommandTests` (Register, Login with remote IP extraction, LogoutAll, ChangePassword), `AuthControllerCookieTests` (Set-Cookie parsing, HttpOnly, Secure, SameSite=Lax, Path, LifetimeDays, rotation, deletion markers, error paths), `AuthControllerCsrfTests` (CSRF validation matrix, Origin whitelist vs request host, Referer fallback, trailing slash/subdomain attacks, cookie vs body token precedence).
  - Phase 5: `QuizzesControllerTests` (CreateQuiz 201 CreatedAtAction, ListQuizzes, GetQuizById, UpdateQuiz, DeleteQuiz 204, AddQuestion 201 Created, UpdateQuestion, DeleteQuestion 204, ReorderQuestions), `AdminUsersControllerTests` (ListUsers with filters/defaults, GetUserById, SuspendUser with 202 Accepted and 204 NoContent branches, ReactivateUser), `AdminAdministratorsControllerTests` (ListAdministrators, CreateAdministrator 201 Created, SuspendAdministrator, ReactivateAdministrator).
  - Phase 6: `GamesControllerTests` (CreateGame, GetGameById, StartGame, EndQuestion, ShowLeaderboard, AdvanceQuestion, EndGame, GetGameReport, JoinGame with remote IP, GetJoinInfo, RemoveParticipant, GetGameParticipants), `SubmitAnswerTokenGuardTests` (early session token syntax rejection: missing headers, length 67/69, prefix PST_ vs pst_, header precedence before DB touch, authorization header variations, trimming rules) using `FailOnUseDbContext`.
  - Phase 7: `ImageUploadControllerTests` (storage unavailable 503 before form read, form read failure mapping, missing/ambiguous files, 5 MiB size boundaries, stream opening failure 503, stream forwarding and ownership disposal after success, business failure, sender fault, or cancellation), `ImagePathGuardTests` (GetUploadsRoot 404, path traversal rejection, unsupported extensions, malformed GUIDs, case-sensitive regex extension check, missing file 404) using `StubImageStorageService`, `StubFormFeature`, `StubFormFile`, and `TrackingStream`.
  - Phase 8: `CorsInstallerTests` (allowlist binding, named `Frontend` policy, credentials, wildcard rejection), `JwtAuthenticationInstallerTests` (named options monitor, MapInboundClaims=false, ClockSkew=zero, signing key validation), `JwtBearerEventTests` (OnMessageReceived hub token extraction from Items and Query, scrubber handoff, OnChallenge/OnForbidden ProblemDetails with W3C trace correlation, OnTokenValidated pre-DB subject and snake-case security version validation).
  - Phase 9: `ObservabilityInstallerTests` (sampler validation, ratio parsing, logging filter rules without starting providers) using `StubHostEnvironment`, `StorageHealthCheckGuardTests` (pre-file degraded storage health and cancellation propagation).
  - Phase 10: `ControllerSecurityMetadataDeclarationTests` (validating ApiController, Authorize, and AllowAnonymous attribute declarations across all controllers and actions).
  - Production code change: Strictly limited to `[assembly: InternalsVisibleTo("Kahoot.Api.UnitTests")]` in `backend/src/Kahoot.Api/AssemblyReference.cs`. Zero `var` declarations throughout test suite.
  - Verification: 280 passing tests (1 documented regression test), full solution build (`dotnet build -c Release backend/Kahoot.slnx`) succeeded with 0 warnings and 0 errors, solution and assembly format checks passed cleanly.

- **API Unit-Test Plan for Gemini (2026-10-01)**:
  - Saved `docs/superpowers/plans/2026-10-01-api-unit-tests.md` using Superpowers writing-plans: ten phases with actual API targets, exact assertions, helper responsibilities, verification gates and a Gemini prompt.
  - Covers shared errors/claims, exception handling, query-token redaction, controllers/CSRF/cookies, upload/path guards, JWT/CORS/observability configuration and storage pre-I/O guards. Full HTTP/authentication/persistence/filesystem/readiness behavior is reserved for integration evidence.
  - Recorded source-defined default ProblemDetails trace overwrite and MVC duplicate-extension findings; runtime reproduction was not performed. Planning only: no API test or production code changed. Document structure, links, source paths and whitespace were checked.

- **Infrastructure Unit Testing Implementation (2026-10-01)**:
  - Implemented all nine phases specified in `docs/superpowers/plans/2026-10-01-infrastructure-unit-tests.md` under `backend/test/Kahoot.Infrastructure.UnitTests`: 202 xUnit tests across 11 test classes covering Services, Security, Persistence, Realtime, and ServiceCollectionExtension.
  - Phase 1: `PinGeneratorServiceTests` (6-digit PIN format, digit distribution, boundary assertions), `RealtimeOptionsTests` (valid ASCII prefixes, whitespace/length/character rejections).
  - Phase 2: `LoginRateLimiterTests` (30 attempt cap, rolling window, IP isolation/trimming, 15m username exponential backoff, failure count resets, bounded concurrency).
  - Phase 3: `PasswordHasherTests` (Argon2id format, 16-byte salt, 32-byte digest, Unicode/whitespace preservation, malformed hash rejection, dummy verification), `JwtTokenGeneratorTests` (claims topology, sub/accountId, role, security version, lifetime metadata, UTC clock handling, HMAC-SHA256 signature validation, distinct GUID jtis). Non-parallel collection definition `PasswordHashingCollection`.
  - Phase 4: `CriticalWorkerFailureTrackerTests` (15-minute degradation threshold, initial failure anchor retention across retries, case-insensitive worker recovery, structured warning and error escalation logging) using `RecordingLogger<T>`.
  - Phase 5: `AuditableEntityInterceptorTests` (in-memory change-tracker audit stamping with real Npgsql model and `SuppressPersistenceInterceptor`, added/modified/unchanged/deleted entity stamping, explicit actor preservation, immutable field protection), `SuspensionFinalizerChannelTests` (1024 bounded capacity, DropWrite backpressure, ordering, repeated hint handling, single-reader cancellation). Added narrow `[assembly: InternalsVisibleTo("Kahoot.Infrastructure.UnitTests")]` to `Kahoot.Infrastructure/AssemblyReference.cs`.
  - Phase 6: `UnauthenticatedSocketGuardTests` (15s handshake timeout scheduling, abort and timer disposal, authentication/disconnect cleanup, duplicate ID disposal) using `RecordingTimerTimeProvider`, `GameHubFilterTests` (method result preservation, unexpected error normalization to RFC 7807 typed envelope `Server.InternalError` without stack trace leakage, cancellation propagation).
  - Phase 7: `GameNotificationServiceTests` (dual-audience aggregate routing, host and player group formatting, cancellation token forwarding), `GameNotificationPersonalEventsTests` (personal scorecard/standing routing, empty list optimization, 32-send batch concurrency ceiling), `GameNotificationFailureTests` (transport error logging and audience continuation, personal recipient continuation, caller cancellation vs unexpected transport timeout filtering) using `RecordingHubContext`.
  - Phase 8: `SecurityInstallerTests` (cryptographic options, Base64 key length validation, bootstrap admin credential rules and environment variable overrides), `PersistenceInstallerTests` (connection string validation, timeout clamping, descriptor lifetimes), `StorageInstallerTests` (ImageStorageOptions contracts, 5 MiB cap, 4096 dimensions, cleanup batch bounds), `RealtimeInstallerTests` (HubOptions boundaries, Redis connection string, channel prefix, lifetime descriptors), `DependencyInjectionTests` (root composition, DR_RECONCILIATION_ON_STARTUP validation and fail-closed abortion).
  - Production code change: Strictly limited to `AssemblyReference.cs` friend attribute. Zero `var` declarations throughout test suite.
  - Verification: 202 Infrastructure tests passed (0 failed), full solution build (`dotnet build -c Release backend/Kahoot.slnx`) succeeded with 0 warnings and 0 errors, solution format check (`dotnet format backend/Kahoot.slnx --verify-no-changes`) passed cleanly.

- **Infrastructure Unit-Test Plan for Gemini (2026-10-01)**:
  - Saved `docs/superpowers/plans/2026-10-01-infrastructure-unit-tests.md`: nine phases with actual source targets, test assertions, helper responsibilities, verification gates, implementation gaps, and a Gemini execution prompt.
  - Plan covers local contracts, login throttling, cryptography, worker health, audit hooks/channel hints, socket timers/filter, notification dispatch, and configuration. Redis/PostgreSQL/filesystem/worker/transport behavior is explicitly reserved for integration and load checks.
  - Planning only; no Infrastructure unit or production code was changed. A friend-assembly attribute is proposed only for future Phase 5. The interrupted Application Phase 1 request was replaced by this planning task; other agents' Application changes remain untouched.

- **Application Unit Testing Implementation (2026-10-01)**:
  - Implemented all eight phases specified in `application unit testing phases.md` under `backend/test/Kahoot.Application.UnitTests`: 373 xUnit tests across 21 test classes in Common and Features.
  - Phase 1 (Common contracts): `Result`, `Result<T>`, `Error`, `KeysetCursor`, and `GameJoinOptions.HasValidOrigin` covering invariants, null values, malformed cursors, boundaries, and origin safety.
  - Phase 2 (Validation pipeline): `ValidationBehavior<TRequest, TResponse>` verifying empty validator pass-through, single/multiple validation execution, error aggregation, and cancellation propagation.
  - Phase 3 (Authentication inputs): `RegisterCommandValidator`, `LoginCommandValidator`, `ChangePasswordCommandValidator`, and `UsernameNormalization` (NFKC + uppercase invariant, prohibited characters, entropy rules, and registration vs login distinction).
  - Phase 4 (Quiz and question inputs): `CreateQuiz`, `UpdateQuiz`, `DeleteQuiz`, `GetQuizById`, `ListQuizzes`, `ReorderQuestions`, `AddQuestion`, `UpdateQuestion`, `DeleteQuestion`, and nested `ChoiceRequestValidator`.
  - Phase 5 (Scoring): `ScoringEngine` verifying exact-set correctness, order independence, deduplication, time decay, integer clamping, midpoint rounding, tick precision, and `Int128` overflow prevention at `int.MaxValue`.
  - Phase 6 (Game and lobby inputs): `PlayerNickname` (UTF-16 scalar traversal, control/format character rejection, NFKC), `JoinGameCommandValidator` (PIN 4-8 digits, UUIDv4), `GetJoinInfo`, `GetGameParticipants`, lifecycle validators (`CreateGame`, `StartGame`, `EndQuestion`, `ShowLeaderboard`, `AdvanceQuestion`, `EndGame`, `RemoveParticipant`), and `SubmitAnswerCommandValidator` (session presence requirement).
  - Phase 7 (Administration inputs): `CreateAdministrator`, `SuspendAdministrator`, `ReactivateAdministrator`, `GetUserById`, `ListUsers`, `SuspendUser`, and `ReactivateUser` validators.
  - Phase 8 (Isolated handler guards): `UploadImageCommandHandler` pre-persistence guards (missing caller, missing file, oversized upload, storage error propagation) using explicit test doubles for `ICurrentUser` and `IImageStorageService`.
  - Added narrow `[assembly: InternalsVisibleTo("Kahoot.Application.UnitTests")]` to `AssemblyReference.cs`. Zero `var` keywords used throughout test code.
  - Verification: 373 Application tests passed (0 failed), 14 Domain tests passed, Release solution build succeeded with 0 warnings and 0 errors, and `dotnet format Kahoot.slnx --verify-no-changes` passed cleanly.

- **Domain Unit Tests (2026-10-01)**:
  - Implemented the explicit request in `backend/test/Kahoot.Domain.UnitTests`: 14 xUnit tests in six files for User, Quiz, and Game initialization defaults and the canonical names/distinct values of UserRole, UserStatus, and GameStatus.
  - Domain currently contains data holders, enums, and an auditing interface. Scoring, normalization, validation, and transitions live in Application; database invariants require persistence integration tests. No production changes, new dependencies, mocks, or infrastructure were added.
  - All 14 tests passed; Release solution build passed with zero warnings/errors; Domain unit-project formatting verification passed.

- **Unit-Test Project Scaffolding (2026-10-01)**:
  - Added empty .NET 10 xUnit projects for Domain, Application, Infrastructure, and Api under `backend/test/` and registered them in `Kahoot.slnx`; each unit project references its matching production layer only.
  - Shared test settings now contain only xUnit/VSTest. PostgreSQL/Redis Testcontainers references belong to the three integration projects; restored unit dependencies contain no Testcontainers, ASP.NET Core testing host, or SignalR client packages.
  - No authored C# files, tests, mocks, fixtures, factories, or production changes. Release solution build passed with zero warnings and errors; project XML, references, and dependency isolation passed checks.

- **Integration-Test Project Scaffolding (2026-10-01)**:
  - Explicit user request superseded the default no-test-project restriction for this task. Added empty .NET 10 Application, Infrastructure, and Api integration-test projects under `backend/test/` and registered them in `Kahoot.slnx`.
  - Test-only settings include xUnit/VSTest and PostgreSQL/Redis Testcontainers; Api includes ASP.NET Core testing and SignalR client packages. No authored C# files, fixtures, factories, production changes, or test scenarios were added.
  - Release solution build passed with zero warnings and errors. Runtime behavior and documented release gates remain unverified.
  - Current-source correction: DR reconciliation is not implemented; `DependencyInjection.AddInfrastructure` rejects `DR_RECONCILIATION_ON_STARTUP=true`. Older completion notes below do not establish the current checkout's DR behavior.

- **Quiz Hostability Simplified**:
  - Quiz authoring uses revisions only; no separate eligibility state or activation endpoint remains.
  - Hosts can create live games from owned quizzes containing at least one question; game creation copies an immutable snapshot.
  - Replaced historical EF migrations with one fresh baseline (`20260929135314_InitialSchema`) matching the current entities and DBML. Fresh PostgreSQL startup and owned/empty/foreign quiz scenarios passed. Quiz edits and game creation now share a Host row transaction lock; a held-lock race check passed.
  - Existing databases with the prior migration history require an explicit data migration or re-baselining before deployment.
- **Player Joining & Lobby Management**:
  - Implemented `POST /api/games/join` with 500-seat transactional capacity limit (`SELECT FOR UPDATE`), Unicode NFKC normalization + uppercase folding, permanent nickname tombstone reservation, and `JoinOperationId` SHA-256 idempotency.
  - Implemented `GET /api/games/join/{pin}` pre-join metadata verification.
  - Implemented `DELETE /api/games/{id}/participants/{participantId}` with host tenant ownership enforcement, token invalidation, seat decrement, dynamic question auto-close evaluation (`GAME-AUTO-002`), and real-time socket eviction frame (`ParticipantRemoved`).
  - Implemented `GET /api/games/{id}/participants` keyset-paginated lobby query.
  - Implemented `ILobbyJoinRateLimiter` token bucket limiter (1200 cap, 60/s refill).
  - Implemented `PlayerPresenceService` and `PlayerPresenceHeartbeatWorker` with Redis tracking and active socket fencing.
  - Implemented SignalR `/hubs/game` methods: `JoinGame` and `Reconnect` with generation fencing and authoritative phase-specific catch-up projections.
- **OpenWolf Universal Integration**: Synchronized OpenWolf rules and skills across all agent platforms (Antigravity, Gemini, Codex, Claude, Cursor). Added `.agents/rules/openwolf.md`, `.agents/skills/openwolf/SKILL.md`, updated `CLAUDE.md`, and placed OpenWolf protocol at the top of `AGENTS.md` (Section 2) to eliminate prompt truncation.
- **Lobby & Player Joining Endpoint Prompt**: Created comprehensive API specification prompt [prompts/player-joining-and-lobby-endpoints.md](prompts/player-joining-and-lobby-endpoints.md) covering `POST /api/games/join`, `DELETE /api/games/{id}/participants/{pid}`, 500-seat capacity, `JoinOperationId` idempotency, and SignalR presence.
- **Game Lifecycle State Machine**: Implemented authoritative 6-state machine (`GameStatus`), commands (`POST /api/games`, `start`, `end-question`, `show-leaderboard`, `advance`, `end`), command idempotency logging (`IGameCommandIdempotencyService`), and `RepeatableRead` deep snapshotting.
- **Realtime & Protocol**: Built `/hubs/game` SignalR hub, `GameNotificationService` with audience isolation, Redis Backplane integration, `HostPresenceService` with distributed leases, and cluster-wide `SocketEvictionService` / `SocketEvictionSubscriber`.
- **Auth Educational Concept Comments Refactor**:
  - Cleaned up comments across all Authentication subsystem components (`Register`, `Login`, `Refresh`, `Logout`, `LogoutAll`, `ChangePassword`, `SystemAdminSeeder`, `AuthController`, `PasswordHasher`, `LoginRateLimiter`, `JwtTokenGenerator`, and `RefreshTokenCleanupWorker`).
  - Removed top-level class header doc blocks, file reference links (`Reference: docs/...`), and the `BACKEND CONCEPT:` prefix.
  - Placed pure concept names and descriptions directly above relevant statements and methods matching [docs/01-roles-and-access.md](docs/01-roles-and-access.md) and [docs/02-authentication.md](docs/02-authentication.md).
  - Explicitly annotated every database query with performance and security mechanisms directly above the query (`AsNoTracking`, `FOR UPDATE` pessimistic row locks, `ExecuteUpdateAsync` single-statement bulk updates, indexed single-record lookups, `AnyAsync` short-circuiting, and `SKIP LOCKED` batch deletion).
  - Verified valid compilation (`dotnet build Kahoot.slnx`: 0 warnings, 0 errors) and formatting (`dotnet format Kahoot.slnx --verify-no-changes`).
- **Project-Wide Educational Concept Annotations Completed**:
  - Annotated the entire backend project (`Kahoot.Domain`, `Kahoot.Application/Common`, `Kahoot.Infrastructure`, and `Kahoot.Api`) with educational concept comments (`// <Concept Name> - <concise explanation>`) directly above statements, properties, and queries based on [docs/](docs/).
  - Highlighted security (Argon2id, JWT revocation via `TokenSecurityVersion`, constant-time comparisons, CSRF/CORS, tenant boundaries, SSRF/PII telemetry scrubbing), performance (MVCC, keyset pagination, `AsNoTracking`, `ExecuteUpdateAsync`, negative-staging indexing, Redis backplanes, SemaphoreSlim locks), and real-time mechanics (SignalR generation fencing, heartbeat leases, socket eviction).
  - Maintained strictly explicit C# types (zero `var` keyword), zero compiler warnings or errors (`dotnet build`), and 100% code formatting verification (`dotnet format --verify-no-changes`).
- **Live Gameplay & High-Throughput Answer Ingestion**:
  - Implemented CQRS `SubmitAnswer` vertical slice (`SubmitAnswerRequest`, `SubmitAnswerResponse`, `SubmitAnswerCommand`, `SubmitAnswerCommandValidator`, `SubmitAnswerCommandHandler`) adhering to `docs/08-live-gameplay.md` and `docs/09-scoring-and-leaderboards.md`.
  - Built `ScoringEngine` supporting exact-set equality (`SCORE-EXACT-001`), speed-decay scoring (`SCORE-FORM-001`), integer time clamping $[0, D]$ with `MidpointRounding.AwayFromZero` (`SCORE-ROUND-001`), and checked 64-bit arithmetic (`SCORE-OVERFLOW-001`).
  - Implemented multi-tier rate limiting (`IAnswerRateLimiter`, `AnswerRateLimiter`) via atomic Redis Lua script enforcing `PLAY-RATE-001` (5 attempts / 3s per socket) and `PLAY-RATE-002` (10 attempts / question per participant across connections) without NAT penalties.
  - Added REST endpoint `POST /api/games/{id}/answers` in `GamesController` (extracting `pst_...` session token from `X-Session-Token` or `Authorization: Bearer`) and SignalR hub method `SubmitAnswer(questionId, choiceIds)` in `GameHub`.
  - Reviewed answer ingestion against `docs/08-live-gameplay.md`: ordered Host and Game row locks, bounded PostgreSQL lock waits, deadlock retry with cleared EF tracking, and database-authoritative activation/acceptance timestamps.
  - Moved answer and participant-removal equality auto-close into their committing transactions; removed the obsolete separate auto-close service. Fixed reconnect pre-reveal score leakage and server-bound rolling answer limits.
  - Registered canonical error codes in `GameErrors` (`NotCurrentQuestion`, `InvalidChoices`, `ParticipantRemoved`, `Unavailable`, `TooManyAnswerAttempts`) and mapped to RFC 7807 `ProblemDetails` in `ApiController`.
- **Scoring and leaderboard source review (2026-09-29)**:
  - Replaced the exclusive game lock on each answer with a shared lock and a short atomic accepted-count update; score, answer, count, and equality auto-close still commit together.
  - Recover duplicate-answer unique collisions as idempotent replays; rate-limited retries can still retrieve a committed answer.
  - Re-rank survivors when a participant is removed during the leaderboard phase and exclude a just-removed player from personal result materialization.
  - Scoring now uses exact tick-based integer arithmetic for deterministic midpoint rounding.
  - Solution build and format verification passed. PostgreSQL, Redis, multi-replica, and SLO runtime checks remain unverified because Docker Desktop's Linux engine was unavailable.
- **Scoring Engine & Live Leaderboards Completed (`docs/09-scoring-and-leaderboards.md`)**:
  - Implemented speed-decay scoring formula with exact set equality (`SCORE-EXACT-001`), integer time clamping $[0, D]$ with `MidpointRounding.AwayFromZero` (`SCORE-ROUND-001`, `SCORE-BOUND-007`), and checked 64-bit score accumulation (`SCORE-OVERFLOW-001`).
  - Added deterministic 3-tuple tie-breaking (`TotalScore DESC`, `NormalizedNickname ASC`, `ParticipantId ASC`) across `ParticipantRankMaterializer`, `ShowLeaderboardCommandHandler`, `EndGameCommandHandler`, and `GetGameReportQueryHandler` (`SCORE-RANK-001`, `SCORE-RISK-004`).
  - Implemented participant exclusion filtering (`!p.IsRemoved`) on all leaderboard and podium projections under read-committed transactions (`SCORE-EXCLUDE-001`, `SCORE-RISK-003`).
  - Designed and implemented dual-audience real-time dispatch contracts (`PersonalQuestionResultEvent`, `PersonalLeaderboardEvent`, `PersonalGameEndedEvent`) with cluster-wide Redis fan-out over personal participant SignalR groups (`$"host:{hostAccountId}:game:{gameId}:participant:{id}"`) in `GameNotificationService` (`SCORE-RES-002`, `GAME-CTRL-003`).
  - Integrated `QuestionResultsMaterializer.MaterializePersonalResultsAsync` and personal scorecard materialization in `EndQuestionCommandHandler`, `SubmitAnswerCommandHandler` (auto-close), and `RemoveParticipantCommandHandler` (auto-close).
  - Enforced pre-reveal score withholding (`SCORE-SEC-002`) across leaderboard and reconnect payloads.
  - **Realtime Protocol & SignalR Communication Completed (`docs/10-realtime-and-protocol.md`)**:
  - Implemented 15-second unauthenticated/abandoned socket handshake timer guard (`UnauthenticatedSocketGuard`) terminating idle zombie sockets (`RT-FAIL-001` item 2, `RT-RISK-004`, `RT-TEST-009`, `RT-BOUND-005`).
  - Implemented `GameHubFilter` (`IHubFilter`) normalizing all unhandled hub method exceptions into typed RFC error response envelopes (`RT-HUB-001`).
  - Implemented strict Host role and tenant game authorization in `JoinAsHost`, returning `{ "code": "Auth.Forbidden" }` typed envelopes and severing unauthorized or cross-tenant sockets (`RT-FAIL-001` item 1, `RT-TEST-004`, `RT-SEC-003`).
  - Configured SignalR engine and transport options: 32 KB frame limit (`RT-BOUND-003`), 15s keep-alive interval, 30s client timeout (`RT-FAIL-001` item 3), 15s handshake timeout, and 64 KB outbound/application buffer ceilings on `/hubs/game` (`RT-BOUND-002`, `RT-SEC-002`).
  - Fixed `JoinGame` presence broadcast event reason from "Reconnected" to "Joined", and ensured all server-broadcast events carry durable `gameId` and committed `stateVersion` (with `presenceVersion` for presence events) (`RT-DISP-001`).
  - Maintained zero `var` keywords, explicit types throughout, clean constructor DI, educational concept comments directly above statements, zero compiler warnings/errors (`dotnet build Kahoot.slnx`), and 100% code formatting verification (`dotnet format --verify-no-changes`).

- **Phase 11 Client Reconnection & State Recovery Completed (`docs/11-reconnection.md`)**:
  - Authoritative handshake `Reconnect(string sessionToken)` in `/hubs/game` with format validation (68 chars, `pst_` prefix), same-participant live-socket retry support, and indexed SHA-256 `TokenHash` lookup.
  - Storm concurrency lock discipline (`RECON-STORM-001`, `RECON-RISK-003`): shared Host and game row locks (`FOR SHARE`) with `SET LOCAL lock_timeout = '3s'`; 5,000-session throughput remains unmeasured.
  - Pessimistic participant row lock (`FOR UPDATE`) serializes competing tabs belonging to the same participant session (`RECON-RISK-002`, `RECON-TEST-007`).
  - Monotonic connection generation fencing (`ConnectionGeneration += 1`), cluster-wide Redis pub/sub eviction (`FenceParticipantAsync`), and active socket presence verification in `SubmitAnswerCommandHandler` (`RECON-GEN-001`, `RECON-SEC-001`).
  - Disconnect handling with generation and active connection verification in `OnDisconnectedAsync` (`RECON-RISK-001`, `RECON-TEST-006`), suppressing presence drops on stale socket callbacks and finished games.
  - Post-game 24-hour recovery window (`RECON-WINDOW-001`, `RECON-BOUND-004`): stamped `ExpiresAt = utcNow.AddHours(24)` on `ParticipantSessionTokens` on game finish via `EndGameCommandHandler` and `GameAbandonmentWorker`, rejecting reconnects past `FinishedAt + 24h`.
  - Authoritative phase-specific catch-up projections (`RECON-CATCH-001`) with typed response records in `GameDtos.cs`, pre-reveal score withholding (`RECON-SEC-002`), timer non-negative clamping (`RECON-BOUND-002`), and materialized deterministic ranks.
  - Preserved zero `var` keywords, explicit C# types throughout, clean constructor DI, educational concept comments directly above statements, zero compiler warnings/errors (`dotnet build Kahoot.slnx`), and 100% code formatting verification (`dotnet format --verify-no-changes`).
  - Follow-up review corrected removed-participant error precedence, Host suspension locking, late expiry evaluation, incomplete catch-up fallbacks, results image projection, and same-socket retry after a lost response. Build and format passed; live PostgreSQL/Redis and load checks were blocked by the unavailable Docker daemon.

- **Phase 12, 13 & 14 Platform Operations, Architecture, Deployment & Verification Completed (`docs/12-platform-operations-and-health.md`, `docs/13-architecture-and-deployment.md`, `docs/14-verification-and-testing.md`)**:
  - Implemented `ICriticalWorkerFailureTracker` and `CriticalWorkerFailureTracker` tracking consecutive failures and duration of background workers (`SuspensionFinalizerWorker`, `GameAbandonmentWorker`), marking degraded if failure persists $\ge 15$ minutes (`OPS-WORK-002`).
  - Updated `ReadinessHealthCheck` to evaluate worker degradation and report `HealthStatus.Degraded` (503 Service Unavailable at load balancer).
  - Refactored `DatabaseHealthCheck` to explicit constructor injection.
  - Implemented `IDisasterRecoveryReconciliationService` and `DisasterRecoveryReconciliationService` (`ARCH-DR-002`): fail-closed security reconciliation transitioning in-flight active games to `ABANDONED_DUE_TO_SUSPENSION`, truncating session token expiry to `finished_at + 24h`, cancelling dangling suspension tickets, and writing audit log records.
  - Added MediatR CQRS slice (`ReconcileDisasterRecoveryCommand`, `ReconcileDisasterRecoveryCommandHandler`, `ReconcileDisasterRecoveryResponse`) and REST endpoint `POST /api/admin/maintenance/disaster-recovery-reconcile` in `AdminMaintenanceController` secured by `SystemAdmin` role.
  - Configured zero-information-disclosure health status writers (`OPS-HEALTH-002`) emitting strictly plain-text statuses (`Healthy`, `Degraded`, `Unhealthy`) on `/health/live`, `/health/ready`, and `/health`.
  - Configured graceful rolling shutdown (`OPS-SHUT-001`, `OPS-BOUND-004`): registered `ApplicationStopping` hook to sever host and player sockets with WebSocket close frames (`NormalClosure` / `EndpointUnavailable`), reject new connections in `GameHub`, and permit 30s connection draining (`HostOptions.ShutdownTimeout = TimeSpan.FromSeconds(30)`).
  - Configured PostgreSQL connection pool parameters (`ARCH-POOL-001`): `MaxPoolSize=80`, `MinPoolSize=10`, 15-second `Timeout`, and connection idle lifetime.
  - Implemented pool exhaustion protection in `GlobalExceptionHandler` (`ARCH-POOL-002`): intercepts pool exhaustion, adds `Retry-After: 5` header, and produces typed `503 Database.PoolExhausted` ProblemDetails.
  - Implemented `AccessTokenScrubberMiddleware` (`OPS-LOG-002`): redacts `access_token` query string parameters to `[REDACTED]` in request URLs and telemetry while caching the raw token in `HttpContext.Items["access_token"]` for SignalR JWT extraction.
  - Enriched `ApiController` to inject `traceId` in ProblemDetails extensions (`OPS-OBS-004`).
  - Synchronized environment files (`.env.development`, `.env.example`, `.env.production`) with `DR_RECONCILIATION_ON_STARTUP=false`.
  - Preserved zero `var` keywords, explicit C# types throughout, clean constructor DI, educational concept comments directly above statements, zero compiler warnings/errors (`dotnet build Kahoot.slnx`), and 100% code formatting verification (`dotnet format --verify-no-changes`).

## Next phase

**Goal:** When explicitly requested, implement isolated integration fixtures and meaningful scenarios for the existing feature slices. Keep documented implementation gaps separate from testing infrastructure; the scaffold alone supplies no runtime verification.

### Relevant files
- `docs/superpowers/plans/2026-10-01-api-integration-tests.md`
- `docs/superpowers/plans/2026-10-01-infrastructure-integration-tests.md`
- `docs/superpowers/plans/2026-10-01-application-integration-tests.md`
- `docs/12-platform-operations-and-health.md`
- `docs/13-architecture-and-deployment.md`
- `docs/14-verification-and-testing.md`

### Closed decisions
- Host ownership uses `users.id`, stored in `host_account_id` on owned rows.
- Current game creation and quiz mutations serialize through the Host row; CreateGame uses default transaction isolation, not an explicit RepeatableRead overload.
- Post-commit broadcast pattern (`RT-ORD-001`) with `CancellationToken.None` ensures state durability before fan-out.
- Redis pub/sub channel `$"{ChannelPrefix}:host-sockets:evict"` enables instant cluster-wide socket eviction upon host suspension.
- Multi-tier answer rate limiting uses a Redis rolling socket window and an active-question participant counter derived from server state.
- Idempotent answer resubmission returns `{ Accepted = true, AlreadyAnswered = true }` without mutating answer counts or scoring.

## Active architecture

- .NET 10, ASP.NET Core, MediatR, EF Core 10, Npgsql/PostgreSQL, StackExchange.Redis, SignalR.
- Clean Architecture: Domain → Application → Infrastructure → Api.

