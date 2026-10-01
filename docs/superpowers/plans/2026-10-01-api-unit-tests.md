# Kahoot API Unit Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Gemini handoff:** Follow the same execution approach as the Infrastructure plan: implement one requested phase at a time in the existing workspace. If Superpowers skills are unavailable in Gemini, execute the concrete tasks and checks below directly. Installing a plugin, spawning agents, committing, deploying, or changing production behavior is not a prerequisite or an authorization supplied by this document.

**Goal:** Verify the API layer's implemented HTTP adaptation, security guards, response contracts, middleware decisions and configuration through isolated tests in `backend/test/Kahoot.Api.UnitTests`.

**Architecture:** Keep the sole direct project reference to `Kahoot.Api` and its existing ASP.NET Core framework reference. Arrange tests by API feature under `Features/`, with separate `Services`, `Middleware`, `HealthChecks` and `ServiceCollectionExtension` responsibilities. Invoke actual controllers, middleware, health guards and registered authentication callbacks using local HTTP contexts and focused doubles; verify the full request pipeline and external I/O through integration tests.

**Tech Stack:** .NET 10; xUnit 2.9.3; Microsoft.NET.Test.Sdk 17.14.1; xunit.runner.visualstudio 3.1.4; MediatR 14.2.0 through the existing Application reference; ASP.NET Core JWT Bearer 10.0.12. Existing OpenTelemetry packages are 1.19.x. Keep the versions actually present in project files; do not upgrade dependencies.

**Spec:** The user's request for a detailed phased API unit-test plan using Superpowers, following the existing [Infrastructure plan](2026-10-01-infrastructure-unit-tests.md). Read the relevant requirements in [roles/access](../../01-roles-and-access.md), [authentication](../../02-authentication.md), [account management](../../03-account-management.md), [quiz management](../../04-quiz-and-question-management.md), [images](../../05-image-management.md), [game lifecycle](../../06-game-lifecycle.md), [joining/lobby](../../07-joining-and-lobby.md), [gameplay](../../08-live-gameplay.md), [operations](../../12-platform-operations-and-health.md), and [verification](../../14-verification-and-testing.md). Requirements documents describe intended behavior; tests must distinguish implemented behavior from missing functionality.

**Inspection date:** 2026-10-01. Other agents are implementing tests in this checkout; inspect current files before each phase and preserve their work.

## Global constraints

- This task creates a plan only. Do not implement its tests until a phase is explicitly requested.
- Read applicable `AGENTS.md`, `.wolf/OPENWOLF.md`, current `.wolf/STATUS.md` and relevant cerebrum guidance. The historical restriction against backend tests is superseded only within the user's explicit testing request; do not rewrite instructions.
- Preserve the existing `.csproj`: one direct reference to `../../src/Kahoot.Api/Kahoot.Api.csproj` and the existing `Microsoft.AspNetCore.App` framework reference. Application/Infrastructure/Domain types remain available transitively.
- Use the existing xUnit/VSTest dependencies in `backend/test/Directory.Build.props`. Add no Moq, NSubstitute, FluentAssertions, WebApplicationFactory, TestServer, SignalR client, Testcontainers, EF InMemory, SQLite or new framework in these unit phases.
- Do not add a reference to another test project to reuse its helpers. Keep API-specific helpers small, local and justified by listed tests; do not introduce a shared testing framework or production abstraction.
- Mirror feature responsibilities. Do not relocate Application validators, handlers, scoring, normalization or Infrastructure implementations into API tests.
- Use actual source names and signatures. Write explicit C# types, file-scoped namespaces, explicit constructors and meaningful names; follow `backend/.editorconfig` and existing tests.
- Do not expose private methods, invoke them through reflection, fabricate queryable DbSets, or change authentication/security checks to satisfy tests. Public metadata inspection for declared endpoint attributes is allowed when explicitly identified below.
- The only planned production edit is `[assembly: InternalsVisibleTo("Kahoot.Api.UnitTests")]` for existing internal API types. Keep those types internal and preserve existing assembly attributes.
- Do not edit `Program.cs`, production options, routes, migrations, environment files, credentials or runtime behavior under this plan.
- No database/Redis connections, HTTP server, WebSocket transport, filesystem writes/reads, real multipart buffering or OTLP export in the unit suite.
- Use MemoryStream and DefaultHttpContext for isolated HTTP adaptation. A request/response object is not proof of MVC execution, model binding, authorization enforcement, or actual wire serialization.
- Use synthetic strings and in-memory signing keys. Never read real `.env.*` or appsettings secrets, mutate process environment variables, or print raw tokens/passwords in test output.
- Use deterministic inputs and explicit completion signals. No sleeps, arbitrary retries, Stopwatch checks or load/SLO assertions.
- Existing-behavior tests may pass immediately. Do not break production code to fabricate a red/green cycle. A confirmed mismatch is a finding, not permission to implement missing functionality.
- Run only the requested phase plus regression checks for completed phases. No automatic commits, merge, publishing, broad formatting or cleanup.

## Review focus

1. **ProblemDetails ownership:** Controllers and exception handling produce different titles and extension shapes. Phases 1–2 assert their actual fields and distinguish local result construction from framework customization.
2. **Token precedence and CSRF:** Cookie presence controls refresh/logout guard behavior even if its token is blank. Phase 4 covers cookie/body precedence, header multiplicity and strict Origin versus Referer handling.
3. **Credential redaction:** The scrubber must preserve the original token only for the hub path while sanitizing downstream query text. Phases 3 and 8 test both sides of that handoff without claiming upstream server logs are covered.
4. **Early rejection:** Invalid player tokens, upload metadata and image paths must reject before touching database/storage/sender dependencies. Phases 6–7 use fail-on-use collaborators to prove the boundary.
5. **Cancellation and disposal:** Controller delegates must forward cancellation; upload streams must be disposed on success, failure and exceptions. Phases 4–7 test observable resource ownership rather than real I/O.

## Current implementation and test boundaries

The API unit project currently contains its `.csproj` and logical `Features`, `HealthChecks`, and `Middleware` folders; no authored API unit-test classes were found at inspection.

| Actual source under `backend/src/Kahoot.Api` | Unit responsibility | Integration responsibility |
| --- | --- | --- |
| `Controllers/ApiController.cs` | Error-to-status/title mapping and explicit correlation fields | MVC ProblemDetailsFactory customization and actual serialized responses |
| `Services/CurrentUser.cs` | Existing claim lookup/precedence and IsAuthenticated projection | Authentication of the principal and authorization of resources |
| `Middleware/GlobalExceptionHandler.cs` | Typed exception classification and safe local responses | Middleware ordering, response-started behavior and real infrastructure faults |
| `Middleware/AccessTokenScrubberMiddleware.cs` | Query sanitization and hub-only token preservation | Kestrel/hosting/ingress logs before middleware and telemetry exports |
| `Controllers/AuthController.cs` | Commands, IP extraction from HttpContext, CSRF/origin guards and cookie instructions | Credential checks, refresh rotation, concurrency, browser cookie enforcement and proxy trust |
| `Controllers/QuizzesController.cs` | Request-to-command mapping and action-result selection | Ownership, immutable snapshots, revision changes, transactions and MVC validation |
| `Controllers/AdminUsersController.cs`, `Controllers/AdminAdministratorsController.cs` | Mapping, status codes and suspension-result branch | RBAC execution, last-active-admin protection, security revocation and worker finalization |
| `Controllers/GamesController.cs` | Normal action mapping and malformed-token guards | Valid-token EF lookup in SubmitAnswer, tenant ownership, game transitions, capacity and scoring |
| `Controllers/ImagesController.cs` | Upload preflight/form guards, stream ownership and image-path guards | Real multipart parser/limits, sanitization, actual FileStream delivery and static cache headers |
| `ServiceCollectionExtension/JwtAuthenticationInstaller.cs` | Bound options and database-free event callbacks/guards | Real JWT authentication, account-state/version queries and role enforcement |
| `ServiceCollectionExtension/CorsInstaller.cs` | Named policy and bound allowlist | Browser preflight and credential behavior in the actual middleware pipeline |
| `ServiceCollectionExtension/ObservabilityInstaller.cs` | Public configuration admission and logger-filter registration | Sampling behavior, span redaction, export, Collector failure isolation and process instrumentation |
| `HealthChecks/StorageHealthCheck.cs` | Pre-file-I/O failure/cancellation behavior | Real capacity/write/delete probe |
| `HealthChecks/DatabaseHealthCheck.cs`, `HealthChecks/RedisHealthCheck.cs`, `HealthChecks/ReadinessHealthCheck.cs` | No full dependency probe in this unit plan | Real dependency probing, 3-second caching/coalescing, 450ms timeout, lifecycle gating and readiness HTTP status |
| `Program.cs`, `Endpoints/HomePageEndpoint.cs` | No extraction/refactoring for tests | Actual host startup, routes, middleware, shutdown and landing page |

The production GameHub and dispatcher belong to Infrastructure, even though Api references that project. Do not duplicate the Infrastructure unit plan here.

## Planned files

Paths in this tree are relative to `backend/test/Kahoot.Api.UnitTests/`. Create only what the requested phase consumes.

```text
TestSupport/
  HttpContextFactory.cs                 # Contexts with isolated controller/result services
  TestProblemDetailsFactory.cs          # Pass-through MVC factory for controller-owned fields
  TestApiController.cs                  # Public protected-method adapter for shared mapping checks
  RecordingSender.cs                   # Focused ISender boundary; records requests/tokens
  RecordingLogger.cs                   # Structured events and original exceptions
  FailOnUseDbContext.cs                # Throws on every IAppDbContext access; no fake queries
  StubImageStorageService.cs           # Capacity/path decisions only; unused I/O throws
  StubFormFeature.cs                   # Prebuilt forms or deliberate read failures
  StubFormFile.cs                      # Synthetic length/name/type and stream opening
  TrackingStream.cs                    # Small in-memory stream with disposal observation
  StubHostEnvironment.cs               # Inert environment values; no file access
  ApiConfiguration.cs                  # Fresh in-memory configuration for registrars
Features/
  Shared/ApiControllerTests.cs
  Shared/ApiControllerFrameworkContractTests.cs
  Auth/AuthControllerCommandTests.cs
  Auth/AuthControllerCookieTests.cs
  Auth/AuthControllerCsrfTests.cs
  Quizzes/QuizzesControllerTests.cs
  Admin/AdminUsersControllerTests.cs
  Admin/AdminAdministratorsControllerTests.cs
  Games/GamesControllerTests.cs
  Games/SubmitAnswerTokenGuardTests.cs
  Images/ImageUploadControllerTests.cs
  Images/ImagePathGuardTests.cs
Services/
  CurrentUserTests.cs
Middleware/
  GlobalExceptionHandlerTests.cs
  AccessTokenScrubberMiddlewareTests.cs
HealthChecks/
  StorageHealthCheckGuardTests.cs
ServiceCollectionExtension/
  CorsInstallerTests.cs
  JwtAuthenticationInstallerTests.cs
  JwtBearerEventTests.cs
  ObservabilityInstallerTests.cs
```

No empty-folder entries or new project are required. Namespaces begin `Kahoot.Api.UnitTests`, followed by the directory. The derived TestApiController is consumed by both shared mapping and real-framework contract checks.

## Verification convention

From `backend/`, with a .NET 10 SDK:

```bash
dotnet test test/Kahoot.Api.UnitTests/Kahoot.Api.UnitTests.csproj --configuration Release
dotnet build Kahoot.slnx --configuration Release
dotnet format test/Kahoot.Api.UnitTests/Kahoot.Api.UnitTests.csproj --verify-no-changes
```

Append `--filter FullyQualifiedName~ClassName` for a targeted class. Every phase also runs the entire API unit project. Once implemented, verify the friend-assembly file with a targeted production formatting check:

```bash
dotnet format src/Kahoot.Api/Kahoot.Api.csproj --include src/Kahoot.Api/AssemblyReference.cs --verify-no-changes
```

In this WSL checkout Linux dotnet may be unavailable; replace `dotnet` with `'/mnt/c/Program Files/dotnet/dotnet.exe'`. Native Windows Gemini can use regular dotnet.

Record scoped working-tree status and baseline verification before execution. Preserve failures that predate this phase. Inspect the final phase diff, including newly added files, and report actual commands and results. Do not claim the application's HTTP pipeline passed because controller methods returned the expected objects.

## Phase 1 — Establish HTTP test support, shared errors and caller claims

**Read:** `Controllers/ApiController.cs`, `Services/CurrentUser.cs`, `AssemblyReference.cs`, the API unit `.csproj`, shared test props and applicable instructions. Phase source paths are relative to `backend/src/Kahoot.Api/` unless prefixed with `backend/`.

**Modify only:** `backend/src/Kahoot.Api/AssemblyReference.cs`, adding `using System.Runtime.CompilerServices;` and `[assembly: InternalsVisibleTo("Kahoot.Api.UnitTests")]` before the file-scoped namespace, if not already present. Preserve the existing Assembly reference and any other agent's attributes.

**Create:** `TestSupport/HttpContextFactory.cs`, `TestSupport/TestProblemDetailsFactory.cs`, `TestSupport/TestApiController.cs`, both shared feature classes, and `Services/CurrentUserTests.cs`.

### Task 1A — Context and result isolation

Use DefaultHttpContext with fixed Request.Path, TraceIdentifier, optional RemoteIpAddress, HTTPS scheme and a synthetic host. Response.Body is an owned MemoryStream for executed minimal results. Use only local service registrations required by the task, e.g. AddLogging/AddOptions for Results.Problem execution. Dispose providers and response streams in tests.

For direct controller tests, assign an explicit pass-through `ProblemDetailsFactory` via the controller's public property. `TestProblemDetailsFactory` implements the framework's CreateProblemDetails and CreateValidationProblemDetails contracts and copies their supplied fields without extra trace enrichment. This prevents MVC defaults from hiding or changing the controller's own mapping. It does not replace the production controller's Problem method.

For exception-handler and authentication-event response tests, execute the actual IResult into MemoryStream using minimal local services. Do not register the application-wide AddProblemDetails service or start MVC. The framework result uses its JSON fallback when IProblemDetailsService is absent, as shown in the [ASP.NET Core 10.0.12 result implementation](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Http/Http.Results/src/ProblemHttpResult.cs). Assert the fields owned by the API component; actual default writer/factory behavior is an integration obligation, especially for additive trace fields. The known correlation finding in Phase 10 must remain visible; isolation is not a workaround for it.

Use a test-only derived ApiController exposing `IActionResult MapProblem(Error error) => Problem(error)` to exercise the protected contract legitimately. Do not use private reflection.

### Task 1B — ApiController mapping

| Test or theory | Required assertions |
| --- | --- |
| `Problem_MapsErrorTypeToHttpStatus` | NotFound 404; Conflict 409; Unauthorized 401; Forbidden 403; RateLimited 429; Unavailable 503; Validation 400; TooLarge 413; UnsupportedType 415; Failure/default branch 500. Construct Error inputs without accessing Result internals. |
| `Problem_MapsKnownTitlesAndFallsBackToCode` | Representative codes across auth, image, quiz and game families use the exact current title; an unknown code uses its code as title. Include Auth.RefreshRace=>Concurrent refresh, Validation.Failed=>Validation failed, Image.TooLarge=>Payload too large, Game.InvalidStateTransition=>Invalid state transition. |
| `Problem_PreservesDescriptionInstanceAndErrorTypeUri` | Detail=Error.Description; Instance=Request.Path; Type=`https://api.kahoot-saas.local/errors/{code}`; ObjectResult status and ProblemDetails.Status agree. |
| `Problem_AddsRequestIdAndCurrentW3cTraceId` | code and fixed requestId are present; with an explicit W3C Activity, traceId equals Activity.TraceId.ToString(). |
| `Problem_DoesNotInventTraceIdWithoutActivity` | With Activity.Current cleared in the execution context and the pass-through test factory, no controller-added traceId exists. Restore the prior Activity afterward. |

Use `new Activity(...).SetIdFormat(ActivityIdFormat.W3C).Start()` for a real nonnull test Activity. Do not rely on ActivitySource creating an Activity without a listener or mutate Activity.DefaultIdFormat globally.

### Task 1C — CurrentUser projection

Instantiate CurrentUser with a real HttpContextAccessor and local ClaimsPrincipal/ClaimsIdentity values.

| Test or theory | Required assertions |
| --- | --- |
| `CurrentUser_NoContextReturnsAnonymousValues` | UserId=null, Role=null, IsAuthenticated=false. |
| `UserId_ReadsNameIdentifierBeforeSub` | A valid NameIdentifier wins over a different sub. Missing NameIdentifier permits sub fallback. |
| `UserId_InvalidPreferredClaimDoesNotFallBackToValidSub` | Present malformed NameIdentifier produces null even when sub is valid, following the actual null-coalescing precedence. |
| `UserId_ReturnsNullForMissingOrMalformedValue` | Missing or invalid GUID yields null. Guid.Empty parses successfully in the current projection; do not invent a nonempty-ID security guard here. |
| `Role_ReadsMappedRoleBeforeRawRole` | ClaimTypes.Role wins over raw role; fallback occurs only when the preferred claim is absent. |
| `IsAuthenticated_ReflectsPrincipalIdentity` | A nonempty authentication type makes ClaimsIdentity authenticated; a claims-only identity without that type remains unauthenticated. Projection can still read its claims; it does not validate credentials. |

### Task 1D — Real MVC factory contract and known failure

Do not let the isolated mapping fixture conceal the real framework interaction. In ApiControllerFrameworkContractTests, assign the public `DefaultProblemDetailsFactory(Options.Create(new ApiBehaviorOptions()))` to TestApiController; no server or external I/O is needed.

- `Problem_WithDefaultMvcFactoryWithoutActivity_ReturnsMappedResponse`: clear Activity.Current, call MapProblem, and assert mapped status/code/requestId. The real factory also adds its own traceId; do not assert its absence in this case.
- `Problem_WithDefaultMvcFactoryAndActivity_ReturnsMappedResponseWithW3cTraceId`: start a W3C Activity, call MapProblem, and expect the same mapped response with traceId=Activity.TraceId.ToString(). **This required assertion is expected to expose a current source defect:** the default factory already inserts traceId, and ControllerBase.Problem adds the supplied extension with Dictionary.Add, causing a duplicate-key ArgumentException.

This is a component contract check inside the connection-free suite, not a substitute for HTTP integration. Keep the intended regression assertion visible and report the failure; do not change it to Assert.Throws merely to encode the defect as expected success, replace the default factory in this test, or fix production without a separate instruction. Other isolated mapping/claim tests can still be implemented, but Phase 1 cannot be reported fully passing while this required contract remains broken. If the production code has changed before execution, reassess with evidence.

- [ ] Add the narrow friend attribute and context/factory support.
- [ ] Implement the shared error mapping and explicit Activity cases.
- [ ] Implement actual claim precedence/projection cases.
- [ ] Add the real-factory contract assertions and report the known duplicate-key failure separately if still present.
- [ ] Run targeted/full API tests, build, scoped formatting and final diff review.

**Exit:** Shared HTTP adaptation is established without a host. Do not claim these tests authenticate users or enforce authorization.

## Phase 2 — Verify centralized exception classification and safe responses

**Read:** `Middleware/GlobalExceptionHandler.cs`, ImageErrors, PasswordHashingRateLimitedException, and operational error requirements.

**Create:** `Middleware/GlobalExceptionHandlerTests.cs`, `TestSupport/RecordingLogger.cs`. **Reuse:** response contexts from Phase 1.

**Interface:** `ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)`.

RecordingLogger<T> captures log level, original exception and structured values. Tests should inspect those observations rather than rendered prose or a private logging helper. Execute the real handler, rewind its MemoryStream, and parse response JSON into JsonDocument/JsonElement.

| Test or theory | Required assertions |
| --- | --- |
| `TryHandleAsync_MapsValidationFailuresByProperty` | FluentValidation.ValidationException =>400, title Validation.Failed, code Validation.Failed, instance path, requestId, and errors grouped by PropertyName. Multiple messages for one property are retained; do not invent deduplication. |
| `TryHandleAsync_MapsImagePayloadLimitOnlyAtUploadEndpoint` | BadHttpRequestException status 413 at `/api/uploads/images`, including case variant =>413 Image.TooLarge with current description. The same exception at another path follows the generic branch, not this specialized mapping. |
| `TryHandleAsync_MapsPasswordHashingLimit` | PasswordHashingRateLimitedException =>429 Request.RateLimited with safe detail. Do not saturate real hashing. |
| `TryHandleAsync_RecognizesPoolExhaustionThroughWrappers` | Nested NpgsqlException with case-insensitive pool-exhaustion message, or PostgresException SQLSTATE 53300 =>503 Database.PoolExhausted, Retry-After=5; log original outer exception with EventName=DatabasePoolExhausted. |
| `TryHandleAsync_ClassifiesPoolBeforeGenericTimeout` | A pool-message NpgsqlException wrapping TimeoutException still returns Database.PoolExhausted. This is the precedence regression. |
| `TryHandleAsync_MapsTransientDatabaseFailureAndRootTimeout` | A real synthetic NpgsqlException wrapping a timeout, and a plain/root TimeoutException =>503 Service.Unavailable and Retry-After=5. Construct actual exception types; make no connection. |
| `TryHandleAsync_RedactsUnexpectedException` | Unexpected exception =>500 with title `An unexpected error occurred.` and requestId; response excludes secret-like synthetic exception text/stack. Generic branch does not add an error code or explicit instance/detail. Assert absence of component-added values, not an exact framework-generated RFC type URI. |
| `TryHandleAsync_DoesNotMisclassifyPlainPoolMessage` | An ordinary InvalidOperationException containing pool wording remains 500; the pool branch requires an NpgsqlException in the chain. |
| `TryHandleAsync_ReturnsHandledAndCorrelatesActivity` | Each normal classification returns true; explicit W3C Activity contributes its TraceId. Representative no-Activity case has no handler-added traceId in the isolated response setup. |

Construct validation failures through the real FluentValidation types. Inspect Content-Type as media type `application/problem+json`, allowing framework charset formatting. Use actual Npgsql public exception constructors from the installed version; do not mock exception properties.

**Do not invent cancellation behavior:** The handler receives cancellationToken but does not use it to stop response serialization or special-case OperationCanceledException. A generic cancellation exception currently follows the generic branch if the response can be written. Do not change that under this plan or assert automatic RequestAborted handling.

- [ ] Implement validation/upload/429 mappings and execute real result serialization.
- [ ] Implement exception-chain, pool-precedence and transient cases.
- [ ] Implement redaction, handled-result and correlation assertions.
- [ ] Run targeted/full tests and the standard build/format/diff gates.

**Exit:** Classification is verified with synthetic exceptions. Real pool exhaustion, network failures, response-started handling and exception middleware order remain integration work.

## Phase 3 — Verify access-token query redaction

**Read:** `Middleware/AccessTokenScrubberMiddleware.cs`, the JwtAuthenticationInstaller OnMessageReceived callback, and OPS-LOG-002.

**Create:** `Middleware/AccessTokenScrubberMiddlewareTests.cs`. **Reuse:** local HTTP contexts.

**Interface:** `Task InvokeAsync(HttpContext context)` with an explicit RequestDelegate next.

The next delegate records the context it sees and returns a completed task. Parse the sanitized query rather than depending on escaping details such as `%5B` for `[`. Assert raw request query text never contains the synthetic credential after scrubbing.

| Test or theory | Required assertions |
| --- | --- |
| `InvokeAsync_RedactsHubTokenAndPreservesOriginalInItems` | At `/hubs/game` and its segment descendants, token query value becomes [REDACTED], Items[access_token] retains original and next runs exactly once after mutation. |
| `InvokeAsync_RedactsNonHubTokenWithoutRetainingCredential` | Other paths redact the query but do not add the original token to Items. `/hubs/game-other` is not a segment match. |
| `InvokeAsync_PreservesUnrelatedParametersAndRepeatedValues` | Other keys/values, including repeated parameters and an encoded value, retain their parsed contents. |
| `InvokeAsync_RedactsAllRepeatedTokenValues` | Multiple access_token values all become [REDACTED]; on the hub path, the stored value follows StringValues.ToString() (comma-joined). Do not invent rejection or selection of one token at this layer. |
| `InvokeAsync_HandlesKeyAndHubPathCaseAccordingToSource` | Case-variant query key is redacted; hub path matching uses the scrubber's explicit OrdinalIgnoreCase. Recheck authentication callback matching separately in Phase 8. |
| `InvokeAsync_LeavesAbsentOrEmptyTokenUnchanged` | Missing/empty token does not create an Items entry and next still runs once. Whitespace is nonempty and is redacted; do not substitute IsNullOrWhiteSpace for the source check. |
| `InvokeAsync_PropagatesDownstreamException` | If next throws, the same exception escapes and the query was already sanitized. |

- [ ] Implement redaction, retention and query-preservation cases.
- [ ] Implement downstream invocation and exception cases.
- [ ] Run targeted/full tests and standard verification.

**Exit:** The middleware's downstream handoff is demonstrated. Hosting/ingress logs may execute before this middleware; the logger-category policy in Phase 9 and live log inspection remain necessary.

## Phase 4 — Verify authentication actions, cookie instructions and CSRF guards

**Read:** `Controllers/AuthController.cs` in full, AuthErrors and the real request/response/command types under `backend/src/Kahoot.Application/Features/Auth/`.

**Create:** all three `Features/Auth/*Tests.cs` files and `TestSupport/RecordingSender.cs`.

### Task 4A — Focused sender and ordinary command mapping

RecordingSender implements the installed MediatR 14.2.0 ISender. Capture requests/tokens and provide typed configured results or exceptions. All unconfigured sends and unused stream APIs fail clearly; do not create a mediator pipeline, service locator or generic bus simulator. Inspect the actual interface overloads, including non-response Send and both CreateStream overloads. Controller calls use the typed IRequest<TResponse> overload with Result<T> or Result.

One suitable small configuration surface is `void RespondWith<TResponse>(TResponse response)`, with `IReadOnlyList<object> Requests` and observed CancellationTokens. Keep any necessary result cast confined to the helper and verify the configured response type before returning it. Async pending-send control may be added only for the stream-ownership case in Phase 7.

For every successful sender-backed action, assert one send, the exact concrete command fields and original CancellationToken. For representative business failures assert the returned ProblemDetails code/status and absence of cookie side effects.

| Action | Expected request mapping | Success result |
| --- | --- | --- |
| Register(RegisterRequest, token) | RegisterCommand with Username/Password unchanged | CreatedResult 201 with empty Location and RegisterResponse |
| Login(LoginRequest, token) | LoginCommand with Username, Password and RemoteIpAddress.ToString(), else `unknown` | OkObjectResult containing LoginResult.Response; RawRefreshToken travels only through cookie |
| LogoutAll(token) | LogoutAllCommand | NoContentResult 204 and deletion of both auth cookies |
| ChangePassword(ChangePasswordRequest, token) | ChangePasswordCommand(CurrentPassword, NewPassword) | NoContentResult 204 and deletion of both auth cookies |

Direct calls do not execute [Authorize], ApiController model validation or FluentValidation. Do not assert password policies here. Unexpected sender exceptions/cancellation propagate out of the action; the global handler is tested separately.

### Task 4B — Real response-cookie headers

Parse Response.Headers.SetCookie using `Microsoft.Net.Http.Headers.SetCookieHeaderValue.ParseList`; do not assert entire generated strings or a cookie collection mock.

| Behavior | Required assertions |
| --- | --- |
| Successful Login and Refresh | Exactly the two named cookie instructions: `kahoot_refresh_token` and `kahoot_csrf_token`. Refresh contains supplied synthetic raw token; CSRF Base64Url decodes to 32 bytes. |
| Refresh cookie flags | HttpOnly=true, Secure=true, SameSite=Lax, Path=/api/auth, MaxAge=TimeSpan.FromDays(configured LifetimeDays). |
| CSRF cookie flags | HttpOnly=false, Secure=true, SameSite=Lax, Path=/, same configured MaxAge. |
| Logout/LogoutAll/ChangePassword success | Both deletion headers match their original paths and security flags, empty value and expired deletion marker. Do not assume a Max-Age=0 header if the framework uses Expires. |
| Failure paths | Login/Refresh/Logout/LogoutAll/ChangePassword failures do not issue or clear cookies in their current branches. |
| Response body boundary | OkObjectResult contains the public LoginResponse/RefreshResponse object, never LoginResult/RefreshResult with RawRefreshToken. |

Test a second valid LifetimeDays value to prove the options are used. Do not assert cryptographic entropy distributions, exact random CSRF strings, browser Secure behavior or automatic browser deletion.

### Task 4C — CSRF and origin matrix through Refresh and Logout

Do not extract or reflect into ValidateCsrfAndOrigin/IsOriginAllowed/SameOrigin. Exercise them through the actual public actions. Use synthetic allowed origin `https://quiz.example.test`, local request origin `https://api.example.test`, and a known synthetic CSRF value. Build request cookies using the real Cookie header, and represent multiple header values with StringValues.

Each rejected guard must return 403 Auth.Forbidden, send zero commands and set/delete zero cookies. Each admitted guard reaches a configured sender exactly once. Cover Refresh and Logout for the shared guard matrix through small public-action helpers, not private-method calls.

| Input scenario | Actual expected guard behavior |
| --- | --- |
| Missing, empty, whitespace or two X-CSRF-Token values | Rejected for both cookie and body/no-cookie paths. A header is always required by current code. |
| Refresh cookie present, missing CSRF cookie | Rejected even with an allowed Origin. |
| Refresh cookie present, mismatched CSRF header/cookie | Rejected; include equal-length mismatch and different-length mismatch. No Unicode/case normalization of CSRF bytes. |
| Refresh cookie present, matching CSRF, no Origin/Referer | Rejected. |
| Matching cookie/header and allowed canonical Origin | Admitted. Request's own scheme/host origin is also admitted, even if not in the configured frontend list. |
| No refresh cookie, single nonblank header, no Origin/Referer | Admitted by guard; no CSRF-cookie match is required on this transport. |
| Allowed absolute Referer when Origin is absent | Admitted; Referer path/query are allowed while authority comparison still scopes scheme, host and port. |
| Nonempty invalid/disallowed Origin with allowed Referer | Rejected; the code does not fall back from a nonempty Origin to Referer. |
| Origin with trailing slash/path/query/fragment/userinfo; relative/null-like/non-HTTP URI | Rejected. Origin must exactly equal the parsed authority representation. Do not assume arbitrary canonicalization is accepted. |
| Different host, scheme or non-default port from both allowed origins and request origin | Rejected. Include an allowed-host suffix attack such as quiz.example.test.attacker.test. |

Add refresh/logout transport and precedence assertions:

- `Refresh_UsesCookieBeforeBodyToken`: both supplied and valid guard => RefreshCommand contains cookie token.
- `Refresh_BlankCookieDoesNotFallBackToBody`: present blank refresh cookie with otherwise valid cookie guard =>401 Auth.InvalidRefreshToken, zero sends, even with a nonblank body token.
- `Refresh_WithoutCookieUsesBodyToken`: guard admitted => body token forwarded unchanged. Null request is valid only if a usable cookie exists; absent/blank token after an admitted guard =>401 and zero sends.
- `Refresh_ReturnsRotatedResponseAndCookies`: Result<RefreshResult> success returns its public Response and replacement cookies; sender failure maps via ApiController without clearing cookies.
- `Logout_UsesCookieTokenOrNull`: admitted guard => LogoutCommand contains cookie token if present, otherwise null; success clears cookies and returns 204.
- `GuardFailurePrecedesMissingRefreshToken`: missing CSRF and missing token produces 403 before the token-empty 401 branch.

Origin parsing and authority equality are source contracts. In particular, do not assume uppercase Origin spelling, a trailing slash or an explicit default port passes its exact-string authority check merely because SameOrigin uses case-insensitive comparison later. Use canonical positive examples; characterize other spellings only after checking actual Uri behavior on the installed runtime.

- [ ] Implement RecordingSender and ordinary command/result tests.
- [ ] Implement cookie parsing, flag/path/lifetime and failure-side-effect assertions.
- [ ] Implement shared guard matrix and token precedence through public actions.
- [ ] Run all Auth classes, full API suite and standard verification gates.

**Exit:** API transport rules are covered. This does not prove login credential validation, refresh-token replay/rotation, browser CSRF enforcement, trusted proxy behavior or authorization attributes executing.

## Phase 5 — Verify quiz and administrative controller adaptation

**Read:** `Controllers/QuizzesController.cs`, `AdminUsersController.cs`, `AdminAdministratorsController.cs`, and corresponding Application request/response/command/query types.

**Create:** the three matching feature test classes. **Reuse:** sender and controller-context helpers.

For each listed action, write a success mapping assertion and one meaningful Result failure assertion. Always assert exact request type/fields, one sender invocation, original CancellationToken and returned public response object. Do not parameterize away differences in route keys, result wrappers or field lists.

### Task 5A — QuizzesController

| Action | Fields to verify in dispatched request | Success contract |
| --- | --- | --- |
| CreateQuiz | CreateQuizCommand(Title, Description) | CreatedAtActionResult 201; ActionName=GetQuizById; RouteValues[quizId]=response.Id; response body |
| ListQuizzes | ListQuizzesQuery(Cursor, PageSize), including omitted defaults null/50 | Ok 200, original ListQuizzesResponse |
| GetQuizById | GetQuizByIdQuery(quizId) | Ok 200 |
| UpdateQuiz | UpdateQuizCommand(quizId, Title, Description) | Ok 200 |
| DeleteQuiz | DeleteQuizCommand(quizId) | NoContent 204 |
| AddQuestion | AddQuestionCommand(quizId, Text, ImageId, DurationSeconds, BasePoints, Choices) | CreatedResult 201 with `/api/quizzes/{quizId}/questions/{response.Id}` |
| UpdateQuestion | UpdateQuestionCommand(quizId, questionId, Text, ImageId, DurationSeconds, BasePoints, Choices) | Ok 200 |
| DeleteQuestion | DeleteQuestionCommand(quizId, questionId) | NoContent 204 |
| ReorderQuestions | ReorderQuestionsCommand(quizId, QuestionIds), preserving sequence | Ok 200 with ReorderQuestionsResponse |

Use a real question request with several choice records so omitted/swapped mapping fields are detectable. Assert optional ImageId/Description values without teaching the controller to validate them. Do not execute CreatedAtActionResult to claim a URL was generated; that requires MVC routing.

### Task 5B — AdminUsersController

| Action | Mapping / result assertions |
| --- | --- |
| ListUsers | ListUsersQuery(cursor, pageSize, username, status), defaults null/50/null/null, Ok with original response |
| GetUserById | accountId => GetUserByIdQuery; Ok response |
| SuspendUser | accountId + Revision => SuspendUserCommand; TerminationPending=true => AcceptedResult 202 containing new SuspendUserResponse(true); false =>204 |
| ReactivateUser | accountId + Revision => ReactivateUserCommand; success=>204 |

Test both suspension success branches and a failure. A 202 response does not establish eventual teardown; the worker/database scenario belongs to integration.

### Task 5C — AdminAdministratorsController

| Action | Mapping / result assertions |
| --- | --- |
| ListAdministrators | ListAdministratorsQuery, Ok with supplied IReadOnlyList<AdministratorResponse> |
| CreateAdministrator | Username/Password => CreateAdministratorCommand; CreatedResult 201 with empty Location and supplied AdministratorResponse |
| SuspendAdministrator | id + Revision => SuspendAdministratorCommand;204 |
| ReactivateAdministrator | id + Revision => ReactivateAdministratorCommand;204 |

Use existing NotFound/Conflict/Forbidden errors or a synthetic error with matching ErrorType for failure mapping; do not manufacture new production error constants. No unit assertion here proves last-active-admin protection or RBAC enforcement.

- [ ] Complete quiz mapping/result cases and run that class.
- [ ] Complete user suspension/administrative mappings and run both admin classes.
- [ ] Run full API tests and standard verification, preserving other layers' tests.

**Exit:** Route/body/query adaptation and result choices are covered; validation, authorization and persistence behavior remain in their appropriate suites.

## Phase 6 — Verify game controller adaptation and malformed-token rejection

**Read:** `Controllers/GamesController.cs` in full and the exact Application game requests/results. SubmitAnswer is exceptional because the controller itself queries IAppDbContext.

**Create:** `Features/Games/GamesControllerTests.cs`, `Features/Games/SubmitAnswerTokenGuardTests.cs`, `TestSupport/FailOnUseDbContext.cs`.

### Task 6A — Sender-only game actions

| Action | Required request mapping | Success contract |
| --- | --- | --- |
| CreateGame | CreateGameCommand(QuizId) | CreatedResult 201 with `/api/games/{response.GameId}` |
| GetGameById | GetGameQuery(id) | Ok |
| StartGame | StartGameCommand(id, CommandId, ExpectedStateVersion) | Ok with StartGameResponse |
| EndQuestion | EndQuestionCommand with same route/control fields | Ok with EndQuestionResponse |
| ShowLeaderboard | ShowLeaderboardCommand with same fields | Ok with ShowLeaderboardResponse |
| AdvanceQuestion | AdvanceQuestionCommand with same fields | Ok with AdvanceQuestionResponse |
| EndGame | EndGameCommand with same fields | Ok with EndGameResponse |
| GetGameReport | GetGameReportQuery(id) | Ok with GetGameReportResponse |
| JoinGame | JoinGameCommand(Pin, Nickname, JoinOperationId, RemoteIpAddress or unknown) | Ok with JoinGameResponse |
| GetJoinInfo | GetJoinInfoQuery(pin) | Ok with GetJoinInfoResponse |
| RemoveParticipant | RemoveParticipantCommand(id, participantId) |204 |
| GetGameParticipants | GetGameParticipantsQuery(id, includeRemoved, limit, cursor) | Ok; omitted defaults are false/null/null |

For every action assert success fields and a meaningful failure, one send and unchanged cancellation. Do not test a state machine by configuring a fake sender to return fabricated state transitions. No validator or transaction executes here.

### Task 6B — SubmitAnswer token guards before EF lookup

**Interface:** `Task<IActionResult> SubmitAnswer(Guid id, SubmitAnswerRequest request, IAppDbContext dbContext, CancellationToken cancellationToken = default)`.

FailOnUseDbContext implements the actual IAppDbContext and throws a clear InvalidOperationException from every property/method. It returns no DbSet or IQueryable. This helper establishes that early guards never access persistence; do not pass null to disguise a dependency or use it for successful database-backed paths.

Use an actual SubmitAnswerRequest with fixed QuestionId and a small ChoiceIds list. Rejected cases must return401 with Game.InvalidSessionToken, send zero commands and leave FailOnUseDbContext untouched.

- Missing token, empty/whitespace X-Session-Token, and no usable Bearer fallback.
- Wrong lengths 67/69, wrong case-sensitive prefix `PST_`, or missing `pst_` prefix.
- Whitespace X-Session-Token permits Bearer fallback, but a malformed nonblank X-Session-Token takes precedence over a syntactically valid Bearer value and is rejected before DB access.
- Invalid Authorization scheme, empty Bearer token and wrong-length Bearer token, including case-insensitive Bearer spelling recognized before its token is rejected.
- X-Session-Token is not trimmed; Bearer payload is trimmed. Use padded malformed inputs that are guaranteed to fail before lookup; do not send a syntactically valid token to the fail-on-use database and interpret its intentional exception as authentication evidence.

Do not claim the format validator checks hexadecimal characters after `pst_`: its executable guard only checks prefix and total length. Every syntactically accepted token requires a real EF token lookup. Successful token hash lookup, game scoping, revoked/expired/removed sessions, participant identity and dispatch of the final SubmitAnswerCommand belong to API/Application integration tests. Do not refactor the controller to create a unit seam under this plan.

- [ ] Complete sender-only game mapping/result cases.
- [ ] Add FailOnUseDbContext and malformed/header-precedence guard cases.
- [ ] Run both classes, the full API suite and standard verification.

**Exit:** The HTTP adapter and early token rejection are covered; database-backed player authentication, rate limiting, answer acceptance, duplicate scoring and concurrency are not.

## Phase 7 — Verify image controller guards and upload stream ownership

**Read:** `Controllers/ImagesController.cs`, IImageStorageService, UploadImageCommand/UploadImageResponse and ImageErrors.

**Create:** both image feature classes, `StubImageStorageService`, `StubFormFeature`, `StubFormFile`, and `TrackingStream`.

### Task 7A — Local collaborators

- StubImageStorageService supplies configured IsStorageAvailable and GetPhysicalFilePath observations; all unused persistence/sanitization/deletion calls throw. No actual filesystem paths are opened.
- Use a prebuilt FormCollection/FormFileCollection through Request.Form for simple cases; give Request an appropriate multipart content type. It must not parse/buffer a real request body.
- StubFormFeature implements IFormFeature for read exceptions and cancellation observations; ReadFormAsync returns a configured collection or fault. Do not simulate a multipart parser.
- StubFormFile supplies Name, FileName, ContentType, synthetic Length and an in-memory OpenReadStream. It can throw on opening. Metadata-only limit tests do not require allocating a 5 MiB stream or a real image.
- TrackingStream owns a few bytes and records disposal; no temp files. It allows checking that the exact stream is available to the sender while dispatch is in progress and is disposed afterward.

### Task 7B — UploadImage(CancellationToken)

| Test or theory | Required assertions |
| --- | --- |
| `UploadImage_UnavailableStorageRejectsBeforeFormRead` |503 Image.StorageUnavailable; form feature configured to fail if touched is not read; zero sends. |
| `UploadImage_MapsFormReadFailure` | InvalidDataException =>400 Validation.Failed; BadHttpRequestException 413=>413 Image.TooLarge; other BadHttpRequestException=>400 Validation.Failed; IOException/UnauthorizedAccessException=>503 Image.StorageUnavailable with original exception logged. |
| `UploadImage_RejectsMissingOrAmbiguousFile` | No file, multiple files, wrong form-file name instead of `file`, or zero-length file=>400 Validation.Failed; no OpenReadStream and no send. |
| `UploadImage_EnforcesMetadataSizeBoundary` | Length 5_242_881=>413 with no stream opening/send; exactly 5_242_880 reaches the configured sender. Do not equate metadata acceptance with actual byte validation by storage. |
| `UploadImage_MapsStreamOpeningFailure` | IOException/UnauthorizedAccessException while opening=>503 Image.StorageUnavailable; original exception logged; zero sends. |
| `UploadImage_ForwardsActualStreamAndMetadata` | UploadImageCommand gets the original opened stream, FileName, ContentType and Length; send token unchanged; success=>CreatedResult 201 with Location=response.Url and response object. |
| `UploadImage_DisposesStreamAfterSuccessOrBusinessFailure` | Stream is usable during send, disposed after action returns; failure retains the correct ProblemDetails status/code. |
| `UploadImage_DisposesStreamWhenSenderThrowsOrCancels` | Original exception/cancellation escapes and stream is still disposed. Control any pending sender using a task signal; do not sleep. |
| `UploadImage_PropagatesFormCancellation` | OperationCanceledException from the form reader is not caught by its error-mapping branches; sender stays untouched and token reaches the form reader. |

Map form/stream failures using actual public exception types. Avoid assertions about RequestSizeLimit or RequestFormLimits executing during direct calls; those are framework pipeline behavior.

### Task 7C — GetImage/GetUploadsRoot pre-file guards

GetUploadsRoot returns404 Image.NotFound. For GetImage, test null/empty/whitespace name, `..`, slash/backslash, unsupported extension, non-GUID text, malformed D-format GUID, and raw Request.Path patterns `%2e%2e`, `%2f`, `%5c` with case variations. Rejected paths must not call GetPhysicalFilePath.

For syntactically accepted names, configure storage to return null so no FileStream is opened. Verify the resolver receives `/uploads/{filename}` and the response is404 Image.NotFound. Cover `.jpg`, `.jpeg`, `.png`, `.webp`, and uppercase hexadecimal GUID spelling. The regex's extension alternatives are case-sensitive: uppercase `.PNG` currently rejects; do not silently change the regex. Guid.Empty in valid D-format is not explicitly rejected by this guard.

The positive image response opens a real FileStream before applying nosniff/cache headers. Therefore successful file delivery, MIME selection, immutable caching headers, missing-file races and FileShare behavior are integration tests using isolated real files. Do not fabricate success without file I/O or weaken the guard to make it testable.

- [ ] Add only the collaborators needed by guard/resource tests.
- [ ] Complete upload failures, limits, metadata mapping and disposal cases.
- [ ] Complete root/path guards with a null-returning resolver on accepted syntax.
- [ ] Run targeted/full tests and standard verification.

**Exit:** Early upload/path decisions and resource ownership are demonstrated. Decoder security, image metadata removal, actual request buffering and delivery remain outside unit scope.

## Phase 8 — Verify CORS registration and database-free JWT callbacks

**Read:** `ServiceCollectionExtension/CorsInstaller.cs`, `JwtAuthenticationInstaller.cs`, API CorsOptions, Infrastructure JwtOptions, and relevant auth/access requirements.

**Create:** `ApiConfiguration`, `CorsInstallerTests`, `JwtAuthenticationInstallerTests`, `JwtBearerEventTests`. **Reuse:** HTTP response contexts and FailOnUseDbContext.

### Task 8A — Local options setup

Return a fresh in-memory configuration for every test. Set `Cors:AllowedOrigins:0=https://quiz.example.test`, Jwt issuer/audience to synthetic values and Jwt:SigningKey to newly generated Base64 key bytes. Register only the installer being tested, logging/options as required, and build a disposable ServiceProvider. Do not call AddInfrastructure, start Program, resolve a Redis multiplexer or host JWT validation against a database.

### Task 8B — CorsInstaller.AddCorsPolicy

- Resolve API IOptions<Kahoot.Api.Options.CorsOptions> and verify configured allowlist binding.
- Retrieve named `Frontend` policy through ICorsPolicyProvider or framework IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>; use explicit names to avoid the two CorsOptions types colliding.
- Assert configured origins allowed, a different origin rejected, credentials supported, any method/header allowed.
- With no configured origins, policy does not become AllowAnyOrigin; credentials support does not turn an empty allowlist into a wildcard.
- Wildcard `*` plus credentials must not produce a permissive policy; test the real builder's InvalidOperationException when the policy is constructed. Do not add custom validation to the registrar.

These assertions inspect a policy; they do not demonstrate an HTTP preflight or browser blocking a request.

### Task 8C — JwtAuthenticationInstaller options

Resolve named IOptionsMonitor<JwtBearerOptions>.Get(JwtBearerDefaults.AuthenticationScheme). Assert MapInboundClaims=false, ValidateIssuer/Audience/IssuerSigningKey/Lifetime=true, exact configured issuer/audience/key, RoleClaimType=role and ClockSkew=zero.

The installer reads one signing key; it does not set a ValidAlgorithms allowlist in the inspected code. Do not assert an unimplemented explicit HS256 restriction merely because the generator signs HS256. The overall token-validation algorithm policy requires a separate integration/security review.

Malformed Base64 SigningKey fails AddJwtAuthentication while reading configuration. Missing/short-key admission is not an additional API registrar policy to invent here: Infrastructure SecurityInstaller owns its startup validations. No key rotation code is introduced.

### Task 8D — OnMessageReceived handoff

Use real MessageReceivedContext with an AuthenticationScheme named Bearer and the retrieved JwtBearerOptions. Call the actual configured event delegate.

- At the accepted `/hubs/game` segment, a nonempty string in Items[access_token] takes precedence over query token.
- Without a usable Items value, a nonempty unredacted query token is extracted.
- Query equal to `[REDACTED]` is not accepted as a token; absence/empty values leave Token unset.
- Non-hub path ignores token query/items; `/hubs/game-other` is not a segment match.
- Case-variant `/HUBS/GAME` also matches: the framework's default StartsWithSegments overload uses OrdinalIgnoreCase. Test this alongside segment-boundary rejection. This was checked against the [ASP.NET Core 10.0.12 implementation](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Http/Http.Abstractions/src/PathString.cs).
- Exercise the public scrubber followed by this callback in one local test: downstream query is redacted, callback receives the original hub token through Items. No HTTP server needed.

Do not invent Authorization-header precedence from this callback alone: the JWT handler's default extraction occurs elsewhere.

### Task 8E — OnChallenge/OnForbidden and early OnTokenValidated failures

Construct actual JwtBearerChallengeContext, ForbiddenContext and TokenValidatedContext using their installed public constructors. Use minimal result services and response MemoryStream from Phase 1.

- OnChallenge handles the challenge, returns 401 application/problem+json with Auth.Unauthorized, current title/detail/instance/requestId, and explicit W3C traceId when present; source exception text is not exposed.
- OnForbidden emits 403 Auth.Forbidden with its current safe title/detail/instance and correlations. Do not force the final ASP.NET writer to match a test-only serializer.
- OnTokenValidated missing/malformed subject => authentication failure with `Invalid user identifier in token.` before resolving IAppDbContext.
- Valid subject plus missing/malformed snake-case token_security_version => failure with `Missing or invalid token_security_version claim.` before database resolution. A camel-case tokenSecurityVersion alone does not satisfy this callback.
- NameIdentifier takes precedence over sub; malformed preferred subject does not fall back. Numeric negative/zero versions are parseable here and cross into DB validation; do not invent a positive-version guard.

Register FailOnUseDbContext only when useful to assert a request service was not accessed; otherwise leave it unregistered and confirm the early failures complete. No successful OnTokenValidated case belongs here: it performs EF AsNoTracking/SingleOrDefaultAsync against Users. Active/suspended/missing account, role mismatch and security-version mismatch require real persistence integration.

- [ ] Implement configuration helper, policy and named JWT option assertions.
- [ ] Implement query/items extraction and scrubber-to-event handoff.
- [ ] Implement real challenge/forbidden events and pre-database claim failures.
- [ ] Run all registrar/event classes, full API tests and standard verification.

**Exit:** Local security configuration and event adaptation are established. Authentication/authorization, signed-token acceptance, DB revocation checks and browser CORS behavior remain integration evidence.

## Phase 9 — Verify observability admission and storage-health early failures

### Task 9A — Observability public configuration

**Read:** `ServiceCollectionExtension/ObservabilityInstaller.cs` and OPS-LOG-002/OPS-OBS-003/004.

**Create:** `ServiceCollectionExtension/ObservabilityInstallerTests.cs`, `TestSupport/StubHostEnvironment.cs`.

**Interface:** `IServiceCollection AddObservability(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, ILoggingBuilder logging)`.

Use a local ServiceCollection and the real AddLogging callback to supply ILoggingBuilder; pass synthetic in-memory configuration and an inert IHostEnvironment. Invoke the registrar only. Do not build/resolve TracerProvider, MeterProvider, OpenTelemetry logger/exporter, or hosted services. Invalid sampler configuration is evaluated before registration of instrumentations/exporters.

| Test or theory | Required assertions |
| --- | --- |
| `AddObservability_RejectsUnsupportedSampler` | Unknown OTEL_TRACES_SAMPLER =>InvalidOperationException with its public configuration message. |
| `AddObservability_RejectsInvalidSamplerRatio` | Reject nonnumeric text, comma-decimal `0,1`, NaN, infinity, negative and >1 values for OTEL_TRACES_SAMPLER_ARG. Invariant numeric parsing is intentional. |
| `AddObservability_AcceptsSupportedConfigurationWithoutStartingProviders` | Registration accepts always_on, always_off, traceidratio, parentbased_always_on, parentbased_always_off, parentbased_traceidratio; trims/case-folds names. Accept ratios0/1/0.1 and omitted sampler in Development/Production. This is configuration admission, not a sampling-output assertion. |
| `AddObservability_SuppressesInformationalHostingRequestLogs` | Resolve only IOptions<LoggerFilterOptions>; find the registered Microsoft.AspNetCore.Hosting.Diagnostics rule at Warning. Do not instantiate logging exporters or claim external ingress logs are filtered. |

Private sampling selection and tag-redaction helpers are not exposed or reflected into. Real Activity/span filtering, sensitive-tag removal, resource attributes, OTLP export and exporter-failure isolation belong to an observability component/integration phase with appropriate control over providers. Do not refactor the registrar just to increase a unit coverage percentage.

### Task 9B — StorageHealthCheck pre-file behavior

**Read:** `HealthChecks/StorageHealthCheck.cs`, `ReadinessHealthCheck.cs`, DatabaseHealthCheck and RedisHealthCheck to understand the concrete boundaries.

**Create:** `HealthChecks/StorageHealthCheckGuardTests.cs`. **Reuse:** image storage and environment stubs.

**Interface:** `Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)`.

- IsStorageAvailable=false =>Degraded with `Image storage is unavailable.`; no environment path/file access.
- IsStorageAvailable throws an ordinary synthetic exception =>same Degraded result, with no source exception leaked through description.
- IsStorageAvailable throws OperationCanceledException while the supplied token is canceled =>original cancellation propagates.
- A cancellation-like exception with an uncanceled caller token follows the catch-all Degraded branch. Do not assert universal cancellation propagation.

Keep the storage predicate false or throwing for every unit case; a true predicate enters a real FileStream probe. Do not test successful health by writing a temp file under this unit project.

**Readiness limitation:** ReadinessHealthCheck takes concrete DatabaseHealthCheck/RedisHealthCheck/StorageHealthCheck and executes actual dependency checks. Its healthy cache, refresh coalescing, lifecycle transitions and timeouts are implemented, but are not independently isolated here without additional seams/mocking infrastructure. Do not pass null collaborators, mutate private cache fields, implement a large fake Redis interface, or change production constructors merely to label those tests unit tests. Exercise the complete readiness component in integration testing.

- [ ] Implement public telemetry admission/filter-registration tests without starting providers.
- [ ] Implement only pre-file storage-health failure/cancellation cases.
- [ ] Run both classes, full API tests and standard verification.

**Exit:** Configuration admission and pre-I/O health decisions are covered. Sampling/export, live dependency health and readiness HTTP responses remain unverified by this unit phase.

## Phase 10 — Final contract review and integration handoff

This phase reviews coverage and documents limitations. Add a test only when a concrete uncovered API behavior remains; no getter/setter, DTO-constructor, endpoint-count or duplicated validator tests.

- [ ] Recheck every planned target against the current checkout and remove stale helper code.
- [ ] Confirm all created helpers have actual consumers, all assertions describe observable behavior, and failures were not hidden by permissive defaults in sender/storage doubles.
- [ ] Verify the API project reference/framework/packages remain unchanged except the planned friend attribute in source.
- [ ] Verify no network/disk I/O, host startup, EF query simulation, private reflection, sleeps, environment mutation or exporter initialization entered the unit suite.
- [ ] Review declared security metadata for AuthController, QuizzesController, GamesController, ImagesController and both admin controllers against source. If adding a focused public-attribute regression assertion, label it a declaration check only; [Authorize]/[AllowAnonymous] execution must still be tested over HTTP.
- [ ] Run the complete API unit suite, Release solution build, scoped formatting and the narrow assembly-file formatting check.
- [ ] Inspect all new files and the production attribute diff without disturbing unrelated changes.
- [ ] Report actual test counts/outcomes, source/build checks, confirmed implementation gaps and unavailable integration evidence separately.
- [ ] Update the repository's OpenWolf handoff under its current instructions; do not overwrite another agent's accomplishments or create a README.

### API integration backlog

| Area | Required evidence |
| --- | --- |
| MVC request pipeline | Actual routes/verbs, CreatedAtAction URL generation, binding, malformed/empty JSON, enum serialization, automatic ApiController validation and MediatR validation behavior |
| Authentication/authorization | JWT signature/lifetime/issuer/audience, configured algorithm policy, role restrictions, active/suspended users, security-version revocation, resource ownership and anonymous exceptions |
| Cookies/CSRF/CORS | Real Set-Cookie wire contract, browser semantics, Origin/Referer through trusted proxies, credentialed preflight, refresh replay/rotation and logout races |
| REST player authentication | X-Session-Token/Bearer selection for accepted syntax, database SHA-256 lookup/game scoping, revoked/expired/removed sessions and participant-derived dispatch |
| Errors and correlation | Complete exception middleware, default ProblemDetailsFactory/writer customization, final requestId/traceId shape, response-started cases and unavailable dependencies |
| Image uploads/downloads | Real multipart parser and request/form limits (5_242_880+65_536 request cap), ImageSharp/storage validation, successful real FileStream response, MIME/nosniff/immutable caching and file races |
| Health | PostgreSQL/Redis/storage probes, lifecycle acceptance/drain, 3-second cache/coalescing, 450ms dependency budget and `/health/live`, `/health/ready`, `/health` status/plain-text privacy |
| Logs and telemetry | Hosting/ingress query logs before middleware, exported span/log redaction, actual sampler behavior, health-span filtering, OTLP failure isolation and safe resource attributes |
| Startup/shutdown | Program composition, trusted CIDR parsing, directory admission, migrations/seeding, reverse proxy handling, graceful 30-second drain and websocket closure |
| Home/realtime | Real home endpoint, SignalR `/hubs/game`, audience membership/backplane, close codes, reconnect recovery and transport limits |

### Known behavior versus gaps to preserve

1. **Framework trace-correlation findings:** The [ASP.NET Core 10.0.12 default writer](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Http/Http.Extensions/src/DefaultProblemDetailsWriter.cs) replaces an existing traceId with Activity.Current.Id, the full W3C activity ID. Current Program registers AddProblemDetails without customization, while exception/JWT callbacks supply the required 32-character TraceId. Separately, the [default MVC factory](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Mvc/Mvc.Core/src/Infrastructure/DefaultProblemDetailsFactory.cs) inserts traceId before [ControllerBase.Problem](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Mvc/Mvc.Core/src/ControllerBase.cs) adds ApiController's extension with Dictionary.Add; an active Activity therefore creates a duplicate-key failure. Phase 1 includes the intended real-factory regression assertion. The final writer needs an API component/integration assertion against OPS-OBS-004. Report both findings separately and do not hide them with isolated fixtures or implement production fixes under this plan. These conclusions come from current repository/framework source; runtime reproduction was not performed while creating this plan.
2. **Single signing key:** JwtOptions and JwtAuthenticationInstaller currently use a single SigningKey/IssuerSigningKey. Previous-key grace rotation described in authentication docs remains an implementation gap; recheck before reporting it. Do not implement rotation as test setup.
3. **Algorithm-policy distinction:** The token generator signs HS256, while the inspected API installer does not configure ValidAlgorithms explicitly. Issuance and acceptance policy are different evidence; do not claim a security flaw or strict enforcement from that observation alone. Verify the real validator's accepted algorithms separately.
4. **CSRF transport behavior:** A nonblank CSRF header is required even without refresh cookies; body/no-cookie flows need no matching CSRF cookie and can omit Origin/Referer. Cookie flows require matching CSRF cookie/header and an admitted origin. These are implemented branches, not permission to simplify the policy.
5. **Direct EF in SubmitAnswer:** The accepted-token path is implemented but persistence-dependent. Its absence from unit tests is a boundary decision, not evidence the feature is missing.
6. **Concrete readiness dependencies and image file delivery:** These behaviors exist but need real component/integration execution. No production abstraction or filesystem mocking layer is introduced by this plan.
7. **DR workflow:** No new maintenance controller or reconciliation endpoint is planned. Infrastructure currently rejects startup reconciliation against an absent independent ledger; do not generate tests for a documented API that does not exist.

## Phase completion report template

```text
Phase completed: <number/name>
Files added/modified: <exact paths>
API behavior demonstrated: <specific mappings/guards/side effects>
Verification actually run: <commands/results/counts>
Existing/environment failures: <specific evidence, if any>
Confirmed implementation gaps: <source/spec references, if any>
Integration evidence still needed: <bounded list>
Production changes: <none, or Phase 1 friend attribute only>
Next phase: <number; do not implement until requested>
```

## Ready-to-use Gemini prompt

```text
Implement Phase <N> of docs/superpowers/plans/2026-10-01-api-unit-tests.md.
Read its global constraints, helper conventions and selected phase before editing.
Inspect current API source and working-tree changes; preserve other agents' work.
Test only the actual implementation through the specified public/component boundaries.
Use existing xUnit dependencies and the current API project/framework references.
Do not start a server, connect to PostgreSQL/Redis, write/read files, initialize exporters,
simulate IQueryable, expose private methods, implement later phases or add missing production features.
Keep controller adaptation evidence separate from MVC/authentication/persistence integration evidence.
If a required assertion exposes a confirmed defect, report it without silently changing production behavior.
Run targeted tests, all completed API unit tests, Release build and scoped formatting verification.
Do not commit automatically. Finish with the phase completion report from the plan.
```
