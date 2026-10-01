# Kahoot Infrastructure Unit Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Gemini handoff:** The user selected Gemini as the implementer. Execute phases sequentially in the existing workspace. If these skills are unavailable, follow the concrete tasks and review gates below directly; installing plugins or spawning agents is not a prerequisite. Implement only the phase requested in the execution prompt. This document authorizes no deployment, commit, merge, dependency upgrade, or unrelated production change.

**Goal:** Build meaningful, isolated tests for the Infrastructure behavior that currently exists, under `backend/test/Kahoot.Infrastructure.UnitTests`.

**Architecture:** Keep the test project referencing only `Kahoot.Infrastructure`; its Application and Domain contracts are available through existing transitive references. Mirror Infrastructure responsibilities and test observable behavior through existing interfaces and public methods. Use real cryptographic libraries, local clocks, real EF change tracking with persistence suppressed, and small explicit SignalR doubles; verify external systems in integration projects.

**Tech Stack:** .NET 10; xUnit 2.9.3; Microsoft.NET.Test.Sdk 17.14.1; xunit.runner.visualstudio 3.1.4. Existing production dependencies include EF Core 10.0.12, Npgsql EF provider 10.0.3, Konscious.Security.Cryptography.Argon2 1.3.1, Microsoft.IdentityModel.JsonWebTokens 8.23.0, ASP.NET Core SignalR/Redis 10.0.12, and ImageSharp 3.1.12. Do not upgrade these versions.

**Spec:** The user's request for a phased Infrastructure unit-test implementation plan, supported by [authentication](../../02-authentication.md), [account management](../../03-account-management.md), [images](../../05-image-management.md), [realtime](../../10-realtime-and-protocol.md), [operations](../../12-platform-operations-and-health.md), [architecture](../../13-architecture-and-deployment.md), and [verification boundaries](../../14-verification-and-testing.md). These documents describe requirements; executable source determines what is implemented. Report missing functionality separately.

**Inspection date:** 2026-10-01. Recheck every target before its phase because other agents may be editing this workspace.

## Global constraints

- The current request is for planning only. Do not implement this document until the user requests a phase.
- Read root and backend `AGENTS.md`, `.wolf/OPENWOLF.md`, `.wolf/STATUS.md`, and relevant cerebrum learnings. The historical backend instruction to keep `test/` empty is superseded within the user's explicitly requested testing scope; do not edit instructions to remove it.
- Preserve unrelated modifications, staged files, existing test projects, and Application/Domain work. Do not reset or broadly format the checkout.
- Test actual code. Do not silently implement requirements, alter security policies, extract private methods into public APIs, or refactor services to make tests pass.
- The only planned production edit is a narrow friend-assembly attribute for two internal Infrastructure types in Phase 5. No new runtime abstractions are needed.
- Preserve the sole direct project reference to `../../src/Kahoot.Infrastructure/Kahoot.Infrastructure.csproj` and shared `backend/test/Directory.Build.props`.
- Use existing xUnit assertions. Do not add Moq, NSubstitute, FluentAssertions, FakeTimeProvider packages, EF InMemory, SQLite, Testcontainers, or a new test framework for these phases.
- Use explicit C# types, file-scoped namespaces, descriptive names, and explicit constructors. Follow `backend/.editorconfig`. Use `[Fact]` for distinct behavior and `[Theory]` for meaningful input matrices.
- Create helpers only when a listed test uses them. No base test hierarchy, universal fixture, generic test host, fake repository, or blanket assembly-wide disabling of parallelism.
- No PostgreSQL/Redis connections, actual WebSocket server, filesystem writes, image decoding, HTTP requests, real environment mutation, or hosted-service execution in the unit suite.
- Generate signing keys in memory solely for tests. Use synthetic credential inputs; do not read real appsettings secrets or `.env.*`, and do not log generated tokens/passwords.
- No sleeps or Stopwatch latency assertions. Use injected time and explicit task completion. Bounded `WaitAsync(TimeSpan.FromSeconds(10))` is allowed only as a deadlock watchdog, never as a behavioral timing assertion.
- Existing-code tests may pass on their first run. Do not deliberately break production code or invent an expected initial failure to imitate feature-development TDD.
- If a test exposes a confirmed implementation gap, preserve the intended assertion and report the gap. Do not change expectations to conceal it. Clearly label deliberate characterization of existing behavior separately from a requirement assertion.

## Review focus

1. Exact clock boundaries: IP expiry is inclusive at one minute; username failure expiry is strictly after fifteen minutes; worker degradation begins at fifteen minutes. Phases 2 and 4 exercise those different comparisons.
2. Cryptographic interoperability: a readable JWT is not necessarily valid, and a correctly shaped password hash is not necessarily correct. Phase 3 verifies real signatures and password round trips, without claiming statistical timing-attack resistance.
3. Resource cleanup: authentication, disconnect, duplicate registration, and shutdown must dispose the right handshake timers. Phase 6 checks disposal and callbacks queued before disposal.
4. Audience isolation: notification payloads must reach the intended group, and personal sends must remain bounded. Phase 7 checks requested destinations and send concurrency; actual membership/backplane isolation remains integration work.
5. Configuration failure: options validation must reject invalid values without starting background services or opening connections. Phase 8 tests real registration delegates and records current DR admission behavior.

## Current implementation and scope map

The unit project currently has only its `.csproj` and empty logical folders `Persistence`, `Realtime`, `Security`, and `Storage`. Shared xUnit/VSTest dependencies already exist. No Infrastructure unit-test classes or mocking packages were found during inspection.

| Existing source | Unit-test scope | Additional verification needed |
| --- | --- | --- |
| `Security/LoginRateLimiter.cs` | In-process IP window and username backoff | Trusted IP extraction, handler delay, HTTP 429 and multi-instance policy |
| `Security/PasswordHasher.cs` | Real Argon2id format, verification and malformed-input handling | 16-active/50-queued saturation, throughput, memory ceiling and timing distributions |
| `Security/JwtTokenGenerator.cs` | Signed JWT content, signature, lifetime and metadata | API authentication, account revocation, cookie/CSRF behavior and key rotation |
| `Services/PinGeneratorService.cs` | Eight ASCII-digit output contract | Database uniqueness, PIN collision retries and statistical randomness assessment |
| `Services/CriticalWorkerFailureTracker.cs` | Elapsed-failure health and recovery | Readiness HTTP status, actual worker outages and replica behavior |
| `Persistence/SuspensionFinalizerChannel.cs` | Local hint delivery, cancellation and bounded dropping | PostgreSQL sweep and eventual finalization after a dropped hint |
| `Persistence/AuditableEntityInterceptor.cs` | Actual EF tracked-entity stamping and modified-property flags | Persisted creation-field protection and database transactions |
| `Realtime/RealtimeOptions.cs` | `HasValidChannelPrefix` predicate | Redis namespaces and deployment isolation |
| `Realtime/UnauthenticatedSocketGuard.cs` | Timer registration, disposal and abort decisions | Actual handshake termination, close codes, transport drain and load |
| `Realtime/GameHubFilter.cs` | Delegate execution, cancellation and exception envelope | SignalR invocation serialization over a real connection |
| `Realtime/GameNotificationService.cs` | Requested group/event/payload, batching and failure handling | Server-derived membership, Redis backplane delivery, ordering after commit and reconnect recovery |
| `ServiceCollectionExtension/*Installer.cs`, `DependencyInjection.cs` | Configuration binding/validation and selected registration contracts | Host startup, migrations, Redis availability and operational readiness |
| `Storage/ImageStorageService.cs` | No isolated unit seam for the storage pipeline today | Filesystem/component integration, actual image sanitization and cleanup |

Do not treat a reference to a PostgreSQL or Redis library as proof of external I/O. For example, configuring Npgsql without opening a connection is permitted; executing a query is not.

## Planned files and responsibilities

All paths below are relative to the repository root. Add only files belonging to the requested phase.

```text
backend/test/Kahoot.Infrastructure.UnitTests/
  TestSupport/
    ManualTimeProvider.cs                  # UTC clock for time-dependent services
    RecordingLogger.cs                     # Structured log observations when behavior requires them
    StubCurrentUser.cs                     # Explicit ICurrentUser values for audit tests
    SuppressPersistenceInterceptor.cs      # Stops SaveChanges before database persistence
    RecordingTimerTimeProvider.cs          # Records ITimer registrations and exposes controlled callbacks
    StubHubCallerContext.cs                # Minimum public HubCallerContext implementation
    FilterTestHub.cs                        # Simple public hub method for invocation metadata
    RecordingHubContext.cs                 # IHubContext<GameHub>, including focused clients/proxies
    RecordedSend.cs                        # Destination, event, argument and token observations
    InfrastructureConfiguration.cs         # Fresh in-memory configuration for installer tests
  Services/
    PinGeneratorServiceTests.cs
    CriticalWorkerFailureTrackerTests.cs
  Security/
    LoginRateLimiterTests.cs
    PasswordHasherTests.cs
    PasswordHashingCollection.cs
    JwtTokenGeneratorTests.cs
  Persistence/
    AuditableEntityInterceptorTests.cs
    SuspensionFinalizerChannelTests.cs
  Realtime/
    RealtimeOptionsTests.cs
    UnauthenticatedSocketGuardTests.cs
    GameHubFilterTests.cs
    GameNotificationServiceTests.cs
    GameNotificationPersonalEventsTests.cs
    GameNotificationFailureTests.cs
  ServiceCollectionExtension/
    SecurityInstallerTests.cs
    PersistenceInstallerTests.cs
    StorageInstallerTests.cs
    RealtimeInstallerTests.cs
  DependencyInjectionTests.cs
```

`Services`, `ServiceCollectionExtension`, and `TestSupport` need no empty `Folder` entries in the project file. Namespace prefixes are `Kahoot.Infrastructure.UnitTests`, followed by the corresponding directory. `Storage/` can remain empty: disk-dependent tests belong in the existing Infrastructure integration project.

## Execution and verification convention

Run these commands from `backend/` with a .NET 10 SDK:

```bash
dotnet test test/Kahoot.Infrastructure.UnitTests/Kahoot.Infrastructure.UnitTests.csproj --configuration Release
dotnet build Kahoot.slnx --configuration Release
dotnet format test/Kahoot.Infrastructure.UnitTests/Kahoot.Infrastructure.UnitTests.csproj --verify-no-changes
```

For a task-specific run, append `--filter FullyQualifiedName~ClassName` to the test command. Every phase must also run the entire Infrastructure unit project, so previously implemented phases remain covered.

In this WSL checkout, Linux `dotnet` may be absent. Use `'/mnt/c/Program Files/dotnet/dotnet.exe'` in place of `dotnet`; Gemini running on native Windows can use normal `dotnet`. Missing tooling is an environment limitation, not a reason to rewrite the tests.

Before the first phase, capture scoped status and baseline results. After each phase, inspect only its diff and run the applicable test/build/format commands once. Broaden verification when a change or failure gives a concrete reason. Do not commit automatically. Record any pre-existing build/format failure separately, preserving user files.

## Phase 1 — Establish isolated contracts and clock support

**Read:** `Services/PinGeneratorService.cs`, `Realtime/RealtimeOptions.cs`, the unit `.csproj`, shared test props, and existing Domain test conventions. Source paths in phase sections are relative to `backend/src/Kahoot.Infrastructure/` unless stated otherwise.

**Create:** `TestSupport/ManualTimeProvider.cs`, `Services/PinGeneratorServiceTests.cs`, `Realtime/RealtimeOptionsTests.cs` under the unit project.

**Interfaces:** `string PinGeneratorService.GeneratePin()`; `static bool RealtimeOptions.HasValidChannelPrefix(RealtimeOptions options)`.

Define a small `ManualTimeProvider : TimeProvider` with an explicit `DateTimeOffset` constructor, `override DateTimeOffset GetUtcNow()`, and `void Advance(TimeSpan duration)`. Store UTC and use no system clock. Later phases use this clock directly; it does not implement timers.

| Test or theory | Required assertions |
| --- | --- |
| `GeneratePin_ReturnsEightAsciiDigits` | For a small bounded sample, e.g. 32 calls, every result has length 8 and every character lies between `'0'` and `'9'`. |
| `GeneratePin_UsesInvariantDigitsUnderDifferentCulture` | Temporarily set a non-default culture, then assert the same ASCII contract. Restore the execution-local culture in `finally`; never change process-wide default cultures. |
| `HasValidChannelPrefix_AcceptsAsciiIdentifier` | Accept length 1 and 64, letters/digits, `_`, `-`, and mixtures of them. |
| `HasValidChannelPrefix_RejectsInvalidIdentifier` | Reject empty, length 65, spaces, colon, dot, slash, newline, non-ASCII accented letters, non-ASCII digits and emoji. Do not invent null support for the nonnullable property. |

- [ ] Confirm the project is still infrastructure-only and contains no new package references.
- [ ] Write the clock helper and the tests listed above using existing public behavior.
- [ ] Run both targeted classes, then the whole Infrastructure unit suite.
- [ ] Run the solution build and scoped formatting verification; review only Phase 1 files.

**Exit:** Contract tests pass without infrastructure. Do not assert PIN uniqueness, force a randomly generated leading zero, run distribution tests, or replace cryptographic randomness with predictable randomness.

## Phase 2 — Verify in-process login throttling

**Read:** `Security/LoginRateLimiter.cs`, `backend/src/Kahoot.Application/Common/Interfaces/ILoginRateLimiter.cs`, and `AUTH-SEC-002` in the authentication specification.

**Create:** `Security/LoginRateLimiterTests.cs`. **Reuse:** `ManualTimeProvider`.

**Interfaces:** `bool IsIpRateLimited(string ipAddress)` records an attempt; `TimeSpan GetUsernameBackoffDelay(string normalizedUsername)`; `void RecordFailedAttempt(string normalizedUsername)`; `void ResetFailedAttempts(string normalizedUsername)`.

Use a fresh limiter and a fixed clock such as `2026-01-01T00:00:00Z` per test. Important: `IsIpRateLimited` is a mutating admission check, not a read-only query.

| Test or theory | Required assertions |
| --- | --- |
| `IsIpRateLimited_AllowsThirtyAttemptsThenRejects` | Calls 1–30 return false; call 31 returns true at the same time. |
| `IsIpRateLimited_ExpiresAttemptsAtExactlyOneMinute` | At `59.9999999s` after 30 admitted attempts the next call is limited; at exactly `60s` a call is admitted. Use ticks and a fresh setup for each boundary case. |
| `IsIpRateLimited_UsesRollingWindow` | Admit 15 at t=0 and 15 at t=30s. At t=60s exactly the first 15 expire: admit 15 new calls, then reject one. This differs from a fixed calendar-minute counter. |
| `IsIpRateLimited_IsolatesAndNormalizesIpKeys` | Different IPs have independent budgets; padded IP text shares a budget with its trimmed form; whitespace/empty values share the `unknown` bucket. |
| `IsIpRateLimited_RejectionDoesNotExtendWindow` | A rejected call before expiry does not create a fresh admitted timestamp that keeps the original budget exhausted after its expiry. |
| `IsIpRateLimited_ConcurrentAttemptsAdmitExactlyThirty` | At fixed time, run 64 bounded attempts against one instance with a shared async start signal; exactly 30 are admitted. No sleeps, blocking barriers or multi-replica claim. |
| `GetUsernameBackoffDelay_FollowsProgression` | Failure counts 0–4 produce zero; 5 => 1s, 6 => 2s, 7 => 4s, 8 => 8s, 9 and further failures => 10s. Include a bounded run of additional failures to guard against shift overflow. |
| `GetUsernameBackoffDelay_ExpiresStrictlyAfterFifteenMinutes` | Five failures at t=0 still yield 1s at exactly 15 minutes; at 15 minutes plus one tick the delay is zero. |
| `RecordFailedAttempt_RenewsWindowAndRestartsExpiredCount` | A later failure moves the expiry anchor to its own timestamp. A failure strictly after an expired window starts a new count of one. |
| `ResetFailedAttempts_ClearsOnlySelectedUsername` | Resetting one key removes its delay while another retains its own delay. |
| `UsernameBackoff_IgnoresBlankNamesAndKeepsKeysIndependent` | Empty/whitespace inputs do not create delay; distinct normalized keys remain independent. Do not make the limiter normalize case or Unicode itself. |

- [ ] Implement the IP-window cases and run `LoginRateLimiterTests`.
- [ ] Implement the username cases and run the same class.
- [ ] Verify bounded local concurrency using task completion, not scheduling assumptions.
- [ ] Run the full Infrastructure suite, build, format check and scoped diff review.

**Exit:** Both dimensions and exact time comparisons are demonstrated. This limiter is process-local. Tests do not establish trusted proxy extraction, distributed budgets, account lockout, or the Application handler's actual waiting behavior.

## Phase 3 — Verify password hashing and signed JWT issuance

### Task 3A — PasswordHasher

**Read:** `Security/PasswordHasher.cs`, `backend/src/Kahoot.Application/Common/Interfaces/IPasswordHasher.cs`, and `AUTH-HASH-001`/`AUTH-SEC-001`.

**Create:** `Security/PasswordHasherTests.cs`, `Security/PasswordHashingCollection.cs`.

**Interfaces:** `Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default)`; `Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default)`; `Task<bool> VerifyDummyPasswordAsync(string password, CancellationToken cancellationToken = default)`.

Use actual Argon2id with its configured 64 MiB memory, 3 iterations and parallelism 1. Put only hashing tests in a named xUnit collection with `DisableParallelization = true` to avoid memory spikes. `PasswordHasher` does not implement `IDisposable`; do not invent a `using` lifetime for it.

| Test or theory | Required assertions |
| --- | --- |
| `HashPasswordAsync_ProducesConfiguredArgon2idFormat` | Six `$`-separated segments including the leading empty segment; algorithm `argon2id`; version `v=19`; parameters `m=65536,t=3,p=1`; decoded salt length 16 bytes; decoded digest length 32 bytes. |
| `HashPasswordAsync_UsesDistinctSaltsForRepeatedPassword` | Two hashes of the same synthetic password contain different salts and each successfully verifies. This checks salting, not a statistical entropy claim. |
| `VerifyPasswordAsync_AcceptsMatchingPasswordAndRejectsMismatch` | A generated hash verifies its input and rejects a different password. Use exact strings; do not trim or normalize them in the test. |
| `VerifyPasswordAsync_PreservesUnicodeAndWhitespaceInput` | A small number of real round trips cover Unicode and meaningful leading/trailing spaces; altering that input fails verification. Avoid a large KDF theory matrix. |
| `HashPasswordAsync_RejectsNullPassword` | `ArgumentNullException`. Intentional null-contract probes may use a narrowly scoped `null!` argument; no general nullability suppression. |
| `VerifyPasswordAsync_RejectsMalformedHash` | False for null/empty hash, wrong segment count, missing leading separator, wrong algorithm/version, reordered or changed parameters, invalid Base64, and salts/digests of the wrong decoded size. Build synthetic Base64 fields with the indicated sizes; keep these cases off the KDF path. |
| `VerifyPasswordAsync_RejectsNullPassword` | False, following the current verifier's contract. |
| `VerifyDummyPasswordAsync_PerformsVerificationForNonMatchingInput` | A clearly synthetic nonmatching input returns false through the public dummy method. No comparison of elapsed milliseconds. |

**Known limitation:** The fast gate path calls `_gate.Wait(0)` without observing the cancellation token; the KDF then executes synchronously. Do not write a test assuming that an already-canceled token always stops hashing, and do not silently fix this behavior. Saturating 16 real KDF operations plus 50 queued requests is a resource/load scenario, not part of this routine unit phase. Do not access the private gate or replace Argon2 parameters.

- [ ] Add the collection definition and real hashing/verification assertions.
- [ ] Add the cheap malformed-input theory; ensure only valid fixtures trigger actual KDF work.
- [ ] Run the hashing class and confirm no infrastructure or timing dependency.

### Task 3B — JwtTokenGenerator

**Read:** `Security/JwtTokenGenerator.cs`, `Security/JwtOptions.cs`, `backend/src/Kahoot.Application/Common/AccessTokenResult.cs`, and the actual `User`/`UserRole` types.

**Create:** `Security/JwtTokenGeneratorTests.cs`. **Reuse:** `ManualTimeProvider`.

**Interface:** `AccessTokenResult GenerateAccessToken(User user)` on a generator constructed with `IOptions<JwtOptions>` and `TimeProvider`.

Use in-memory options with issuer `unit-test-issuer`, audience `unit-test-audience`, a newly generated 32-byte Base64 signing key, and a fixed whole-second time. Construct a User with fixed nonempty ID, DisplayUsername, NormalizedUsername, synthetic PasswordHash, role and TokenSecurityVersion. Use `JsonWebTokenHandler` for token reading and actual `ValidateTokenAsync` validation. Reading claims alone does not verify a signature.

| Test or theory | Required assertions |
| --- | --- |
| `GenerateAccessToken_IncludesIdentityAndSecurityClaims` | `sub` and `accountId` equal the user ID; `unique_name` equals DisplayUsername; `role` reflects Host/SystemAdmin; both `token_security_version` and `tokenSecurityVersion` equal the specified version; no invented `tenantId` claim. |
| `GenerateAccessToken_ReturnsMatchingLifetimeMetadata` | At t=0 with 15 minutes, ExpiresAt = t+15m and ExpiresInSeconds = 900; JWT iat/nbf/exp match UTC Unix seconds. Repeat with a valid non-default lifetime such as 2 minutes => 120 seconds. |
| `GenerateAccessToken_UsesInjectedUtcClock` | A clock initialized with a nonzero offset yields claims and expiration representing the same UTC instants. Account for JWT whole-second precision explicitly. |
| `GenerateAccessToken_UsesHmacSha256AndValidSignature` | Header algorithm HS256; validation succeeds with the correct key, exact issuer/audience and allowed HS256 algorithm. |
| `GenerateAccessToken_RejectsWrongValidationKeyIssuerOrAudience` | Validation fails independently for a different same-length key, wrong issuer, and wrong audience. These are test-validator assertions about the generated token, not API authentication tests. |
| `GenerateAccessToken_ProducesDistinctValidJwtIds` | Two issuances contain parseable, nonempty and different GUID jti values. Avoid asserting exact token strings. |
| `GenerateAccessToken_RejectsNullUserAndConstructorDependencies` | ArgumentNullException for the public guards. Invalid options are tested through the installer in Phase 8. |

For fixed historical time, either disable lifetime validation in tests focused on signature/issuer/audience, or provide a test-local lifetime validator using the manual clock. Do not let the machine's current date determine outcomes. If claiming expiry validation, use the latter and separately exercise before-expiry and after-expiry instants.

- [ ] Implement metadata/claim tests and actual signature-validation tests.
- [ ] Run `JwtTokenGeneratorTests`, then all Infrastructure tests, build and formatting checks.
- [ ] Review synthetic inputs and remove any accidental credential/token output.

**Exit:** Real cryptographic behavior is verified at the component boundary. Password policy belongs to Application validation; authentication authorization, refresh-token rotation, cookie policies and JWT revocation belong to their existing layers/integration suites.

## Phase 4 — Verify critical-worker health decisions

**Read:** `Services/CriticalWorkerFailureTracker.cs`, `backend/src/Kahoot.Application/Common/Interfaces/ICriticalWorkerFailureTracker.cs`, and `OPS-WORK-002`.

**Create:** `Services/CriticalWorkerFailureTrackerTests.cs`, `TestSupport/RecordingLogger.cs`. **Reuse:** `ManualTimeProvider`.

**Interfaces:** `void ReportFailure(string workerName, Exception exception)`; `void ReportSuccess(string workerName)`; `bool HasDegradedBacklog(out string? failingWorkerName, out TimeSpan? failureDuration)`.

`RecordingLogger<T>` implements `ILogger<T>` and captures LogLevel, EventId, original Exception and structured state pairs. Do not assert fully rendered prose, decimal formatting, or call private fields. Use `NullLogger<T>.Instance` when logging is irrelevant to a test.

| Test or theory | Required assertions |
| --- | --- |
| `HasDegradedBacklog_NoFailuresReturnsFalseAndNullDetails` | False, null worker, null duration. |
| `HasDegradedBacklog_DegradesAtExactlyFifteenMinutes` | After first failure, false at 15m minus one tick; true at exactly 15m, returning the tracked name and elapsed duration. |
| `ReportFailure_RetainsFirstFailureTimeAcrossRetries` | Retry at t+10m does not postpone degradation to t+25m; at t+15m the original elapsed period is returned. |
| `ReportSuccess_ResetsOnlyMatchingWorker` | A case-insensitive worker-name match clears its state; another failing worker remains tracked. Never rely on ConcurrentDictionary enumeration order when several are degraded. |
| `ReportSuccess_NewFailureStartsFreshWindow` | Recovery followed by another failure requires a fresh 15-minute period. |
| `ReportFailure_LogsWarningThenEscalatesAtThreshold` | Before threshold: Warning, original exception, `EventName=CriticalWorkerAttemptFailed`; reporting at threshold: Error with `EventName=CriticalWorkerBacklogEscalated` and expected FailureCount. |
| `ReportSuccess_LogsRecoveryOnlyForTrackedWorker` | Information event `CriticalWorkerRecovered` with elapsed duration for an actual recovery; no recovery event for an unknown worker. |

- [ ] Implement elapsed-time and recovery tests.
- [ ] Implement structured event checks with the focused logger helper.
- [ ] Run targeted/full tests and standard build/format/diff gates.

**Exit:** The health tracker is deterministic. Do not claim `/health/ready` returns 503 from these tests; that mapping and actual worker failures require API/integration checks.

## Phase 5 — Verify local persistence hooks and suspension hints

### Task 5A — Narrow access to internal types

**Modify only:** `backend/src/Kahoot.Infrastructure/AssemblyReference.cs`.

Add `using System.Runtime.CompilerServices;` and the assembly attribute `[assembly: InternalsVisibleTo("Kahoot.Infrastructure.UnitTests")]`, following the existing Application friend-assembly convention if it is still present. Keep the assembly reference type and its existing behavior intact. Apply the attribute before the file-scoped namespace.

Both `AuditableEntityInterceptor` and `SuspensionFinalizerChannel` remain internal. Do not access them via reflection, make them public, or grant access to other assemblies.

### Task 5B — Audit stamping with real EF tracking and suppressed persistence

**Read:** `Persistence/AuditableEntityInterceptor.cs`, `Persistence/AppDbContext.cs`, `backend/src/Kahoot.Domain/Entities/IAuditableEntity.cs`, `Quiz.cs`, and the Quiz configuration.

**Create:** `Persistence/AuditableEntityInterceptorTests.cs`, `TestSupport/StubCurrentUser.cs`, `TestSupport/SuppressPersistenceInterceptor.cs`.

**Interfaces:** Existing sync `SavingChanges` and async `SavingChangesAsync` hooks; `ICurrentUser.UserId`, `Role`, `IsAuthenticated`.

Configure an actual AppDbContext with the existing Npgsql provider using an inert, non-secret connection string such as `Host=127.0.0.1;Database=unit_test_model;Username=unit_test`. Register the real audit interceptor first and the suppression interceptor second. The latter returns `InterceptionResult<int>.SuppressWithResult(0)` from both saving hooks, so SaveChanges stops before database work. Dispose the context normally.

This is an in-memory change-tracker test with the real provider configured, not an EF InMemory database. Never call OpenConnection, EnsureCreated, Migrate, LINQ database queries, or transaction APIs. Do not create fake DbSets or query providers. Keep the save helper local to this class unless another concrete test requires it.

Use real Quiz entities with fixed Id, HostAccountId and Title. Set states explicitly. Run important cases through both sync and async save APIs, without blocking on async.

| Test or theory | Required assertions |
| --- | --- |
| `SaveChanges_AddedEntityGetsInjectedUtcTimestampsAndActor` | CreatedAt and UpdatedAt become the fixed UTC time; missing CreatedBy/UpdatedBy become current UserId. |
| `SaveChanges_AddedEntityPreservesExplicitActors` | Existing nonnull CreatedBy and UpdatedBy are preserved even when the caller differs; timestamps still follow current time. Include one actor missing and one explicitly set. |
| `SaveChanges_AddedEntityWithoutCallerKeepsNullActors` | With null UserId, unset actor IDs stay null, timestamps still set. |
| `SaveChanges_ModifiedEntityUpdatesOnlyMutableAuditMetadata` | UpdatedAt becomes now and UpdatedBy becomes authenticated actor; CreatedAt/CreatedBy properties have IsModified=false even if the test marked them modified. Assert flags, not imaginary database persistence. |
| `SaveChanges_ModifiedEntityWithoutCallerPreservesUpdatedBy` | Existing UpdatedBy remains intact with null UserId; UpdatedAt still changes. |
| `SaveChanges_UnchangedAndDeletedEntitiesAreNotStamped` | Existing audit values for both states remain unchanged. |
| `SaveChanges_AllAddedEntriesUseSameTimestamp` | Multiple added auditable entries receive the same injected time in one invocation. |

Add a defensive direct-hook null-context test only if a legitimate `DbContextEventData` can be constructed without null-forcing its required dependencies. It is not a reason to introduce a diagnostic-event factory or reflection. No test is required for private implementation details.

- [ ] Add the friend attribute and minimal test doubles.
- [ ] Register persistence suppression after the production interceptor and confirm the save result is zero.
- [ ] Implement the state/actor matrix for sync and async paths.
- [ ] Run audit tests with no PostgreSQL server available; any attempted connection is a test-design failure.

### Task 5C — Bounded suspension channel

**Read:** `Persistence/SuspensionFinalizerChannel.cs`, `backend/src/Kahoot.Application/Common/Interfaces/ISuspensionFinalizerChannel.cs`.

**Create:** `Persistence/SuspensionFinalizerChannelTests.cs`.

**Interfaces:** `void NotifySuspension(Guid hostAccountId)`; `ValueTask<Guid> ReadAsync(CancellationToken)`; `ValueTask<bool> WaitToReadAsync(CancellationToken)`; `bool TryRead(out Guid hostAccountId)`.

| Test or theory | Required assertions |
| --- | --- |
| `NotifySuspension_DeliversHintsInOrderFromSingleProducer` | A short known sequence is read in the same order; a drained channel returns false from TryRead. |
| `NotifySuspension_PreservesRepeatedHints` | Two notifications for the same ID can be read twice. No deduplication is implemented. |
| `ReadAsync_WakesAfterNotification` | Start a pending read, notify, await the original operation and verify the ID. |
| `WaitToReadAsync_SignalsWithoutConsumingHint` | A pending wait completes true on notification; a subsequent read still receives it. |
| `EmptyChannel_ReadAndWaitHonorCancellation` | For each API, begin a pending operation on an empty channel and cancel its token; await OperationCanceledException without notification. |
| `NotifySuspension_DropsNewHintsAfterCapacity` | Enqueue 1024 distinct IDs, then one extra. Drain exactly the first 1024; the extra is absent. Notification completes without blocking. |
| `NotifySuspension_AcceptsAgainAfterDrain` | After freeing space, a newly emitted ID is delivered. |
| `NotifySuspension_MultipleProducersRetainHintsBelowCapacity` | A bounded set of producers emits fewer than 1024 unique IDs; one reader obtains every ID. Compare a set/count; do not assert order between producers. |

Await each ValueTask once, or convert it to Task once with AsTask when required by assertions/watchdogs. This channel has one-reader configuration; do not introduce concurrent readers in these tests.

- [ ] Implement delivery, pending-operation, cancellation and capacity cases.
- [ ] Run the channel class and full Infrastructure tests.
- [ ] Run build and formatting checks, including a targeted format check of `AssemblyReference.cs`; inspect the small production diff.

**Exit:** Local audit mutation and wake-up hint semantics are established. Persistence immutability and eventual suspended-game cleanup after dropped hints remain integration obligations.

## Phase 6 — Verify handshake timeout management and hub error envelopes

### Task 6A — UnauthenticatedSocketGuard

**Read:** `Realtime/UnauthenticatedSocketGuard.cs`, `RT-TEST-009` and `OPS-SHUT-001`.

**Create:** `Realtime/UnauthenticatedSocketGuardTests.cs`, `TestSupport/RecordingTimerTimeProvider.cs`.

**Interfaces:** `void Track(string connectionId, Action abortConnection)`; `void MarkAuthenticated(string connectionId)`; `void Remove(string connectionId)`; `void AbortAll()`.

Create a timer-focused TimeProvider that overrides `CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)` and returns a small nested `ITimer` implementation. Capture registrations, scheduled dueTime/period, state and disposal. Expose a controlled `FireQueuedCallback()` method to simulate a callback already queued before disposal. Implement Change/Dispose/DisposeAsync locally as required by ITimer. Do not create actual timers or build a general scheduler.

| Test or theory | Required assertions |
| --- | --- |
| `Track_SchedulesOneShotFifteenSecondTimeout` | dueTime=15s, period=Timeout.InfiniteTimeSpan, connection ID supplied as callback state; no immediate abort. |
| `Timeout_AbortsTrackedConnectionAndDisposesTimerOnce` | Fire the queued callback: abort count 1 and timer disposed. Fire it again: abort count remains 1. |
| `MarkAuthenticated_DisposesTimerAndSuppressesQueuedTimeout` | Mark the tracked ID authenticated, then fire its previously queued callback; no abort. |
| `Remove_DisposesTimerAndSuppressesQueuedTimeout` | Same cleanup on disconnect; no abort afterward. |
| `Track_DuplicateIdDisposesNewTimerAndRetainsOriginalRegistration` | Track the same ID twice with different abort actions; the second timer is disposed. Without reusing the ID after removal, its queued callback uses the retained original abort action and aborts at most once. |
| `AbortAll_AbortsEveryPendingRegistrationOnce` | Multiple IDs abort once and their timers are disposed; a second AbortAll and queued callbacks do not abort again. |
| `Cleanup_UnknownIdsAndEmptyShutdownAreSafe` | Unknown MarkAuthenticated/Remove and empty AbortAll do not throw or abort other connections. |

Explicitly remove or abort tracked registrations during test cleanup. These tests establish timer scheduling and registration decisions, not real elapsed WebSocket close behavior. Reuse of a connection ID after removal while an old callback is queued is not covered by the duplicate-registration assertion; if required, investigate and report it separately rather than assuming a registration-generation fence exists.

- [ ] Build only the timer controls required by these cases.
- [ ] Implement scheduling, cleanup and queued-callback tests without sleeping.
- [ ] Run the guard class.

### Task 6B — GameHubFilter

**Read:** `Realtime/GameHubFilter.cs` and the envelope requirement `RT-HUB-001`.

**Create:** `Realtime/GameHubFilterTests.cs`, `TestSupport/StubHubCallerContext.cs`, `TestSupport/FilterTestHub.cs`. **Reuse:** `RecordingLogger`.

**Interface:** `ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)`.

Construct a real `HubInvocationContext` using a simple test Hub with a public method, the stub caller context, an empty ServiceProvider and public MethodInfo. This public metadata is required by SignalR's constructor; do not use reflection to reach private production code. Do not instantiate the production GameHub or its Redis/database dependencies.

| Test or theory | Required assertions |
| --- | --- |
| `InvokeMethodAsync_ReturnsDelegateResultWithoutModification` | Next receives the same context, executes once, and returns the same object; include null result. |
| `InvokeMethodAsync_ConvertsUnexpectedExceptionToSafeEnvelope` | Serialize returned object to JsonElement: success=false, data=null, error.code=`Server.InternalError`, error.description=`An unexpected server error occurred.`; no source exception text or stack trace in response JSON. |
| `InvokeMethodAsync_LogsOriginalExceptionWithInvocationContext` | Error log retains original exception plus Method and ConnectionId structured values. |
| `InvokeMethodAsync_PropagatesCancellation` | An OperationCanceledException from next escapes as the original exception; no internal-error envelope and no unexpected-error log. Include TaskCanceledException through the same rule. |

- [ ] Implement delegate/envelope/cancellation cases without dynamic access to anonymous properties; inspect JsonElement instead.
- [ ] Run the filter class, full Infrastructure suite, build, format and scoped diff review.

**Exit:** Local timeout cleanup and exception normalization are covered. No claims about live SignalR connections, authentication enforcement, actual wire serialization or close codes.

## Phase 7 — Verify SignalR notification dispatch and bounded personal sends

**Read:** `Realtime/GameNotificationService.cs`, `backend/src/Kahoot.Application/Common/Interfaces/IGameNotificationService.cs`, and personal event records in `backend/src/Kahoot.Application/Features/Games/Models/GameDtos.cs`.

**Create:** `Realtime/GameNotificationServiceTests.cs`, `Realtime/GameNotificationPersonalEventsTests.cs`, `Realtime/GameNotificationFailureTests.cs`, `TestSupport/RecordingHubContext.cs`, `TestSupport/RecordedSend.cs`. **Reuse:** `RecordingLogger`.

### Task 7A — Focused SignalR doubles and aggregate routing

`RecordingHubContext : IHubContext<GameHub>` supplies IHubClients and IGroupManager. Its group client implements `IClientProxy.SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)`. Record exact group/event/payload/token, support immediate completion, injected exceptions and explicitly released sends. Unused audience/group-management APIs throw NotSupportedException so unintended routing fails clearly. Never launch a hub/server or emulate Redis.

Use fixed GUIDs in expectations. Production group format uses normal GUID text, not N-format:

```text
host:{hostAccountId}:game:{gameId}:hosts
host:{hostAccountId}:game:{gameId}:players
host:{hostAccountId}:game:{gameId}:participant:{participantId}
```

| Existing method | Expected events and audiences |
| --- | --- |
| `PublishQuestionStartedAsync` | Players receive `QuestionStarted` with playerPayload; hosts receive `QuestionStartedForHost` with hostPayload. |
| `PublishQuestionEndedAsync` | Both groups receive `QuestionEnded` with the supplied payload. |
| `PublishLeaderboardUpdatedAsync` | Both groups receive `LeaderboardUpdated`. |
| `PublishGameEndedAsync` | Both groups receive `GameEnded`. |
| `PublishParticipantPresenceChangedAsync` | Both groups receive `ParticipantPresenceChanged`. |

Implement tests named `PublishQuestionStartedAsync_RoutesDistinctAudiencePayloads`, an aggregate-routing theory for the remaining methods, and `PublishAsync_ForwardsCallerTokenWithoutAddingPayloadFields`:

- Assert exactly the intended destinations, exact event names, a single payload argument, and the original payload object.
- Distinct host/player payloads must never be swapped or broadcast through All/Users/another game's group.
- A non-default CancellationToken reaches SendCoreAsync unchanged.
- The service routes caller-supplied payloads; it does not redact correctness itself or inject missing gameId/stateVersion/presenceVersion fields. Do not invent that behavior from comments or documentation.

### Task 7B — Personal results and batch limits

| Existing method | Required personal event |
| --- | --- |
| `PublishQuestionEndedWithPersonalResultsAsync` | `PersonalQuestionResult` with each original PersonalQuestionResultEvent |
| `PublishLeaderboardUpdatedWithPersonalRanksAsync` | `PersonalLeaderboardUpdated` with each original PersonalLeaderboardEvent |
| `PublishGameEndedWithPersonalRanksAsync` | `PersonalGameEnded` with each original PersonalGameEndedEvent |

For each method, assert aggregate notifications complete before personal sends begin; one event per participant goes only to its participant group, with the correct typed payload. Empty personal lists produce exactly the two aggregate notifications. Multiple participants receive their own records; do not assert completion order within a batch.

Add `PublishPersonalEvents_ProcessesAtMostThirtyTwoSendsPerBatch`:

1. Use 33 participant records. Aggregate sends complete immediately; each personal send returns an incomplete TaskCompletionSource task using RunContinuationsAsynchronously.
2. Start publication and observe exactly 32 personal sends started, with 33rd absent.
3. Release one send; the 33rd still must not start because the service awaits the entire batch.
4. Release the remaining 31 sends and await an explicit signal that the 33rd starts.
5. Release the final send, await publication, and verify all 33 destinations exactly once with maximum in-flight personal sends <=32.

Use callbacks/task signals and thread-safe recording. No sleeps, polling loops, elapsed-time assertions, or reflection into the private batch helper. Always release remaining tasks in finally when an assertion fails to avoid stranded publications.

### Task 7C — Failure and cancellation semantics

| Test | Required assertions |
| --- | --- |
| `PublishAsync_LogsTransportFailureAndContinuesOtherAudience` | Throw from player send with an uncanceled token: publication completes, host send is still attempted, original exception logged at Error. |
| `PublishPersonalEvents_ContinuesAfterOneFailedRecipient` | One personal send fails with an uncanceled token; other recipient sends are attempted and the public operation completes. |
| `PublishAsync_DoesNotSwallowFailureWhenCallerTokenIsCanceled` | Cancel the supplied token and make the fake send fail; the exception escapes. For cancellation-specific case use OperationCanceledException carrying that token. Passing a canceled token to a successful fake is not enough to assert cancellation. |
| `PublishAsync_HandlesUnexpectedCancellationAccordingToCatchFilter` | An OperationCanceledException with an uncanceled caller token is logged/suppressed, following the current `when (!cancellationToken.IsCancellationRequested)` filter. Do not assume all cancellation exceptions propagate. |

- [ ] Implement the focused recording proxy and aggregate routing cases.
- [ ] Implement each personal event type and empty-list case.
- [ ] Implement deterministic batching and failure/cancellation cases.
- [ ] Run all three classes, full Infrastructure suite, build, format and diff checks.

**Exit:** The dispatcher requests the correct groups and respects its local batching/failure contract. Unit doubles do not establish that group members are authorized, messages traverse Redis, notifications happen after database commit, or slow clients disconnect correctly. Post-commit `CancellationToken.None` is the responsibility of calling feature handlers; do not change it here.

## Phase 8 — Verify configuration validation and registration contracts

**Read:** all four installers in `ServiceCollectionExtension/`, `DependencyInjection.cs`, and the actual Options classes. Reuse Phase 1 prefix tests; do not duplicate their full character matrix here.

**Create:** `TestSupport/InfrastructureConfiguration.cs`, the four installer test classes, and `DependencyInjectionTests.cs`.

Build configuration through `new ConfigurationBuilder().AddInMemoryCollection(...)`. A fresh valid dictionary is returned for each test; one key is then overridden to exercise a rule. Use these keys to establish a valid baseline:

```text
ConnectionStrings:DefaultConnection = Host=127.0.0.1;Database=unit_test_model;Username=unit_test
Database:CommandTimeoutSeconds = 30
Database:MigrationCommandTimeoutSeconds = 300
BootstrapAdmin:Enabled = false
Jwt:Issuer = unit-test-issuer
Jwt:Audience = unit-test-audience
Jwt:SigningKey = <fresh Base64 key generated in memory>
Jwt:AccessTokenMinutes = 15
RefreshToken:LifetimeDays = 14
RefreshToken:FamilyMaxLifetimeDays = 30
GameJoin:ClientBaseUrl = https://quiz.example.test
Realtime:RedisConnectionString = 127.0.0.1:6379
Realtime:ChannelPrefix = kahoot_unit_tests
```

ImageStorage may use its committed defaults. Disabled BootstrapAdmin still needs a present section because AddSecurity uses GetRequiredSection. Do not supply an empty section and expect it to count as present.

Call the actual installer, build a disposable ServiceProvider and resolve only the appropriate `IOptions<T>.Value`. Exercise `IStartupValidator.Validate()` for a valid and invalid complete configuration if startup validation is part of the assertion. Do not claim ValidateOnStart has run just because Value access passed. Do not start a Host or resolve IConnectionMultiplexer, IHostedService collections, Redis HubLifetimeManager, image storage pipelines, or presence services. Avoid whole-graph ValidateOnBuild where missing external service dependencies would invalidate the unit harness.

### Task 8A — SecurityInstaller

**Interface:** `IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)`.

Test valid options and the following independent mutations through the real bound options:

- Jwt issuer/audience empty or whitespace => OptionsValidationException.
- SigningKey malformed Base64 or 31 decoded bytes => rejected; 32 and 64 bytes => accepted.
- AccessTokenMinutes 0 or -1 => rejected; positive custom value => accepted.
- Refresh LifetimeDays 0/-1 => rejected; FamilyMaxLifetimeDays below LifetimeDays => rejected; equality => accepted.
- Enabled bootstrap with blank username or password lengths 15/129 => rejected; lengths 16/128 with nonblank username => accepted by this installer.
- Disabled bootstrap does not require credentials. The installer's executable password rule checks length, not the complexity claimed by its comment; do not add a complexity test to this registrar.
- In-memory BOOTSTRAP_ADMIN_ENABLED/USERNAME/PASSWORD override nested options through the actual Configure delegate. Malformed enabled value => InvalidOperationException, not OptionsValidationException. No Environment.SetEnvironmentVariable calls.
- Required missing sections fail registration. Keep this separate from invalid bound-option cases.

Inspect descriptors for singleton IPasswordHasher->PasswordHasher and IJwtTokenGenerator->JwtTokenGenerator. Descriptor inspection is sufficient; do not instantiate rate limiters backed by Redis. Do not freeze every internal descriptor or registration order.

### Task 8B — PersistenceInstaller

**Interface:** `IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)`.

- A missing/whitespace DefaultConnection or nonpositive command/migration timeout rejects options.
- DefaultConnection overrides a conflicting Database:ConnectionString in the options object.
- IAppDbContext is scoped and aliases AppDbContext; verify descriptor intent. Actual same-instance scoped resolution may be checked using a stub current user and fixed clock if it remains connection-free.
- Optional connection-free data-source checks may resolve NpgsqlDataSource and inspect its public ConnectionString via NpgsqlConnectionStringBuilder, then dispose it. Never open a connection.
- If those checks are implemented, test default max/min pool values 80/10, preservation of explicitly valid max/min values such as 90/5, timeout 0 or >15 clamped to 15, and a positive timeout <=15 preserved. The installer supplies defaults when pool settings are absent; it does not clamp an explicit maximum to 80. Follow source, not the broader comment.

### Task 8C — StorageInstaller

**Interface:** `IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)`.

Resolve ImageStorageOptions only. Test defaults accepted and each contract mutation rejected:

| Property | Actual accepted contract |
| --- | --- |
| UploadsSubdirectory / StagingSubdirectory | Exactly `uploads` / `uploads/staging` |
| MaxFileSizeBytes | Exactly 5_242_880 |
| MaxWidth / MaxHeight | Exactly 4096 / 4096 |
| MaxPixelArea / MaxDecodeMemoryBytes | Exactly 16_777_216 / 67_108_864 |
| MinimumFreeStorageRatio | Exactly 0.10 |
| OrphanRetentionDays / StagingQuarantineHours | Exactly 7 / 24 |
| CleanupIntervalMinutes | Greater than 0 |
| CleanupBatchSize / ReconciliationBatchSize | 1–1000 inclusive |
| MaxBatchesPerPass | 1–100 inclusive |

Use boundary theories for bounded values and change one exact-contract field at a time. This verifies registration validation; it does not test disk capacity or image sanitization. Check singleton IImageStorageService registration without invoking the implementation.

### Task 8D — RealtimeInstaller and root admission

**Interfaces:** `IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)`; `IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)`.

- Reject blank RedisConnectionString, invalid prefix and invalid GameJoin origin through bound options. A few representative origin cases suffice; full origin semantics belong to Application tests.
- Resolve `IOptions<HubOptions>` without resolving the backplane: MaximumReceiveMessageSize=32768, KeepAliveInterval=15s, ClientTimeoutInterval=30s, HandshakeTimeout=15s, MaximumParallelInvocationsPerClient=1. Do not claim these values establish actual slow-client behavior.
- Inspect important lifetimes: IGameNotificationService scoped; IPinGeneratorService scoped; IConnectionMultiplexer singleton factory not invoked; ICriticalWorkerFailureTracker and ISuspensionFinalizerChannel singleton at root composition.
- A complete valid configuration can register AddInfrastructure and validate options without starting a host or touching external services.
- DR_RECONCILIATION_ON_STARTUP malformed => InvalidOperationException; true => InvalidOperationException explaining security-ledger reconciliation; absent/false => registration can proceed.
- For rejected DR values, capture descriptor count before/after and confirm rejection occurred before downstream registration. Do not invent a reconciliation service or maintenance endpoint.

- [ ] Implement the fresh configuration helper and isolated option-provider setup.
- [ ] Complete each installer class and root admission cases against actual delegates.
- [ ] Confirm no real infrastructure factory was invoked accidentally.
- [ ] Run targeted/full tests and standard build/format/diff gates.

**Exit:** Configuration rules and selected DI lifetimes are established. These assertions do not verify migration execution, host readiness, connection pool exhaustion, or production environment overrides from real deployment files.

## Phase 9 — Final suite review and integration handoff

This phase adds no filler tests or new test framework. Close the unit scope and make remaining verification obligations explicit.

- [ ] Recheck all files against the current implementation; remove obsolete helper code and unused imports.
- [ ] Verify every helper has actual consumers and every test checks behavior rather than reproducing the production algorithm.
- [ ] Confirm there is no network/disk I/O, hosted-service execution, environment mutation, sleep, private reflection, fake IQueryable, EF InMemory or SQLite substitution.
- [ ] Run the complete Infrastructure unit suite, Release solution build, and scoped formatting verification. Include Phase 5's production attribute in the final diff review.
- [ ] Verify the project still has only its existing direct Infrastructure reference and shared packages.
- [ ] Summarize per-phase coverage, actual pass/fail/skip counts from output, confirmed gaps and integration obligations. Do not turn unavailable infrastructure checks into skipped unit tests that appear covered.
- [ ] Update the repository's OpenWolf handoff according to its current instructions. Do not overwrite another agent's active work or create a README.

### Explicit integration backlog

| Area | Existing files / behavior | Required evidence outside this unit plan |
| --- | --- | --- |
| PostgreSQL model and persistence | AppDbContext, Configurations/*, Migrations/* | Applied schema, enum mappings, constraints, actual FK/unique exception translation, row locks, rollback and audit field persistence |
| Startup | DatabaseMigrationService, DatabaseSeeder | Advisory-lock serialization, failed migration/seeding admission, permissions and cancellation |
| Workers | RefreshTokenCleanupWorker, SuspensionFinalizerWorker, QuestionImageCleanupWorker, GameAbandonmentWorker | Real batch SQL, SKIP LOCKED, retries, retention, races, resource release and restart recovery |
| Stored command idempotency | GameCommandIdempotencyService | Cache replay, payload mismatch, persisted results and concurrent inserts in PostgreSQL |
| Distributed rate limits | LobbyJoinRateLimiter, AnswerRateLimiter | Execute the real Lua scripts in Redis, authoritative Redis TIME, TTLs, token refill/window boundaries and multi-key atomicity |
| Presence / eviction | HostPresenceService, PlayerPresenceService, SocketEvictionService, both eviction subscribers, heartbeat workers | Real leases, generation fencing, pub/sub delivery/loss, stale sockets, database cross-check recovery and multiple instances |
| Hub workflows | GameHub | JoinAsHost/JoinGame/Reconnect/SubmitAnswer, authentication/authorization, group membership, actual envelopes and transport closure |
| Notifications | GameNotificationService + caller handlers | Audience membership, post-commit publication, Redis backplane, slow readers, reconnect catch-up and event versions |
| Image storage | ImageStorageService + cleanup worker | Real temporary directories, image bytes, decoder limits, metadata removal, atomic moves, staging cleanup, malicious files and storage failures |
| Operational limits | PasswordHasher, pool, sockets and workers | Memory/queue saturation, latency percentiles, timing distributions, load, degraded HTTP readiness and graceful shutdown |

Local filesystem/image tests are component integration tests even when they run without PostgreSQL or Redis. Place them in `backend/test/Kahoot.Infrastructure.IntegrationTests`, using isolated temporary directories and real ImageSharp operations when that scope is requested. Do not refactor the current storage pipeline merely to relabel those tests as unit tests.

### Known gaps and limits to preserve in the report

1. **DR reconciliation:** `docs/13-architecture-and-deployment.md` specifies restoration reconciliation. Current AddInfrastructure explicitly rejects `DR_RECONCILIATION_ON_STARTUP=true`; no unit test can demonstrate a reconciliation workflow that is absent. Test its implemented fail-closed admission and report the missing workflow.
2. **Signing-key rotation:** `docs/02-authentication.md` describes retaining a prior key during a grace period. Current JwtOptions has a single SigningKey and the API validator uses one IssuerSigningKey. Issuance tests establish only the existing single-key behavior; previous-key rotation remains an implementation gap to recheck separately.
3. **Hash cancellation/saturation:** Fast-path cancellation is not guaranteed and current KDF work is synchronous. The routine suite does not establish the stated 16-active/50-queued resource envelope or timing indistinguishability. Report those limits; avoid production changes under this plan.
4. **Process-local login limits:** A single limiter instance's correctness does not imply a shared login budget across replicas. Report scope without inventing a distributed requirement or changing the implementation.
5. **Dispatcher payload responsibility:** GameNotificationService routes payloads supplied by callers. Tests cannot claim it injects versions, removes correctness fields or commits database state; those responsibilities live elsewhere.

## Phase completion report template

```text
Phase completed: <number and name>
Files added/modified: <exact paths>
Behavior demonstrated: <specific contracts>
Verification executed: <commands and actual results/counts>
Pre-existing/environment failures: <specific evidence, if any>
Implementation gaps: <source and requirement references, if any>
Remaining integration evidence: <bounded list>
Production changes: <none, or Phase 5 friend attribute only>
Next phase: <number; do not implement until requested>
```

## Ready-to-use Gemini prompt

```text
Implement Phase <N> of docs/superpowers/plans/2026-10-01-infrastructure-unit-tests.md.
Read the full global constraints and the selected phase before editing.
Inspect the actual source and current working tree; do not assume the snapshot is unchanged.
Write tests only for the behavior currently implemented and preserve Clean Architecture boundaries.
Use existing dependencies and the exact scenario assertions specified in the phase.
Do not implement later phases, integration fixtures, or missing production functionality.
If a confirmed defect prevents a required assertion, report it with evidence and keep it separate from test implementation.
Run the targeted tests, all completed Infrastructure unit tests, Release build and scoped formatting check.
Preserve unrelated changes and do not commit automatically.
Finish with the phase completion report from the plan.
```
