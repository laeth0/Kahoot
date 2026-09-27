# Phase 5 - Refine operational logs and error correlation

## Objective

Make existing operational logs useful and correctly leveled in both JSON console and Loki, and add trace correlation to existing ProblemDetails without changing `requestId`, status codes, or error semantics.

## Files to create or modify

- Modify `backend/src/Kahoot.Infrastructure/Persistence/RefreshTokenCleanupWorker.cs` for intermediate/terminal retry and zero-deletion logging levels.
- Review and modify only justified log calls in `DatabaseMigrationService.cs`, `DatabaseSeeder.cs`, `SuspensionFinalizerWorker.cs`, `backend/src/Kahoot.Application/Features/Auth/Bootstrap/SystemAdminSeeder.cs`, `backend/src/Kahoot.Infrastructure/Realtime/SocketEvictionService.cs`, and `backend/src/Kahoot.Api/Middleware/GlobalExceptionHandler.cs`.
- Modify `backend/src/Kahoot.Api/Controllers/ApiController.cs`, `backend/src/Kahoot.Api/Middleware/GlobalExceptionHandler.cs`, and `backend/src/Kahoot.Api/ServiceCollectionExtension/JwtAuthenticationInstaller.cs` for additive `traceId` in their existing ProblemDetails paths.
- Avoid new helper/framework files unless a small shared function demonstrably reduces error-prone duplication in the actual affected code.

## Required packages/dependencies

No new packages. Use existing `ILogger<T>`, `System.Diagnostics.Activity`, ASP.NET Core ProblemDetails, and built-in `LoggerMessage` source generation only if a recurring event actually benefits. No custom logging service or second logger factory.

## Step-by-step implementation tasks

- [ ] Review every existing `ILogger` use in the above files for level, stable template, data sensitivity, useful bounded context, and duplicate exception reporting. Leave low-frequency useful messages intact; do not convert all calls to generated logging mechanically.
- [ ] Refactor cleanup loop ownership so each failed attempt is logged **Warning** with attempt/retry delay while another attempt remains, and the exhausted pass is logged **Error once**. A later successful retry is Information only when rows were deleted; zero-row pass is Debug. Preserve cancellation behavior, retry count/delays, deletion batching, and useful `DeletedCount`/`ElapsedMilliseconds` context. Do not log token values.
- [ ] Move migration/seeding lock-acquired messages to Debug if they add only low-level detail; retain meaningful startup completion Information and transient migration Warning/terminal Error. Review suspension finalizer startup/terminal errors and account ID use without widening scope. Treat expected authentication/validation/404/409 paths as normal result handling, not Error logs.
- [ ] For all existing ProblemDetails construction paths, preserve `requestId = HttpContext.TraceIdentifier`; add `traceId = Activity.Current.TraceId.ToString()` only when an Activity with a valid trace ID exists. Cover `ApiController.Problem`, validation/rate-limit/DB/unhandled branches in `GlobalExceptionHandler`, and JWT `OnChallenge`/`OnForbidden`. Keep current status, title, detail, type, and code fields. Avoid a new response field when no active trace exists.
- [ ] Confirm OTel exported logs already carry TraceId/SpanId/TraceFlags and structured state. Do not copy IDs into each log template. Keep console JSON scopes/event metadata enabled and inspect one correlated request error in Loki and console.
- [ ] Decide `LoggerMessage` use from evidence: current workers run infrequently, so retaining structured template calls is acceptable. If a genuinely hot recurring event exists, convert just that event with a stable EventId/EventName and document a small ID convention; do not introduce an event catalog.

## Configuration/environment variables

No new variables. Keep Phase 2 log category levels and OTel sampling. This phase must not enable EF sensitive-data logging, Npgsql parameter capture, request/response-body logging, or raw URL/header capture to make correlation easier.

## Instrumentation covered

Existing `ILogger<T>` operational events and automatic log-span correlation, plus trace IDs in HTTP error responses. No custom spans or metrics. No Information log for each successful request/gameplay event.

## Testing and validation

- `dotnet build Kahoot.slnx` and `dotnet format Kahoot.slnx --verify-no-changes` from `backend/`; do not add tests.
- Manually exercise a normal business error, validation error, unauthorized/forbidden response, transient DB failure, and unexpected exception in a safe local environment. Check same status/code/requestId as before and `traceId` only when active. Do not manufacture an unsafe production exception route.
- Exercise cleanup with a safe local scenario or reason through its retry loop when a real failure cannot be induced without disturbing data. Inspect a pass with zero deletions and one with deletions if available; report any unexercised path.
- Inspect console and Loki records for one matching trace ID and one terminal error log. Verify no passwords, token/cookie values, SQL parameters, or raw query strings are present.

## Acceptance criteria

- Intermediate cleanup failures are Warning and only exhausted failure is Error; zero-deletion passes are Debug and useful deletion passes Information.
- Existing ProblemDetails retain `requestId` and add `traceId` only with an active trace across all identified response producers.
- Important errors are logged once at the correct boundary; expected 4xx results are not turned into Error logs.
- Changes remain focused on the existing logger calls and error responses, with no new logging abstraction.

## Dependencies on previous phases

Phases 0–4 complete so the correlation path can be checked in Jaeger, Loki, and Grafana.
