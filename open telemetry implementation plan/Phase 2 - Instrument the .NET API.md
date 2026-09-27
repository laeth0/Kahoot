# Phase 2 - Instrument the .NET API

## Objective

Emit vendor-neutral OTLP logs, traces, and metrics from `Kahoot.Api` while retaining JSON console logs. Protect Npgsql connection strings before enabling database metrics, and preserve the existing database startup flow.

## Files to create or modify

- Create `backend/src/Kahoot.Api/ServiceCollectionExtension/ObservabilityInstaller.cs` with one focused `AddObservability` registration method called from `Program.cs`.
- Modify `backend/src/Kahoot.Api/Program.cs` for the one registration call; do not alter middleware order or routes.
- Modify `backend/src/Kahoot.Api/HealthChecks/DatabaseHealthCheck.cs` only if the health-check database query creates orphan Npgsql spans after the HTTP trace filter is applied.
- Modify `backend/src/Kahoot.Api/Kahoot.Api.csproj` for the six Phase 0 pinned packages listed below. No OTel SDK package in Application, Infrastructure, or Domain.
- Modify `backend/src/Kahoot.Api/appsettings.json`, `.Development.json`, `.Production.json` for console formatter and category levels only.
- Modify `.env.example`, `.env.development`, `.env.production` **together** for new standard OTel variable keys. Preserve their existing values and do not edit `.env`.
- Modify `backend/src/Kahoot.Infrastructure/ServiceCollectionExtension/PersistenceInstaller.cs`, `backend/src/Kahoot.Infrastructure/Persistence/DatabaseMigrationService.cs`, and `DatabaseSeeder.cs` to share one named `NpgsqlDataSource`. No schema file, migration, or business query change.

## Required packages/dependencies

Add exact stable Phase 0 versions to **Api only**: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`, and `Npgsql.OpenTelemetry`. Use existing Npgsql/EF Core references for `NpgsqlDataSource`; add no separate direct Npgsql reference unless compilation with the current project graph proves it is required. No Serilog, NLog, Aspire, Prometheus/Jaeger/Loki/Grafana .NET client, or duplicate instrumentation package.

## Step-by-step implementation tasks

- [ ] Add `AddObservability` to Api's focused service-registration folder. Register one OpenTelemetry resource identity shared across logs, traces, and metrics: `service.name` from `OTEL_SERVICE_NAME` or `Kahoot.Api`, assembly version, namespace `Kahoot`, deployment environment from `IHostEnvironment`, and one process-lifetime instance ID. Allow standard `OTEL_RESOURCE_ATTRIBUTES` to supply non-sensitive deployment attributes; check precedence rather than silently overriding operator values.
- [ ] Configure OTLP/HTTP protobuf exporters for logs, traces, and metrics using standard endpoint/protocol environment variables. Set no Prometheus, Jaeger, or Loki address in application code. Keep export asynchronous/bounded by supported SDK defaults; do not fail startup when Collector is unreachable.
- [ ] Enable inbound ASP.NET Core tracing/metrics, `HttpClient` tracing/metrics, .NET runtime metrics, and Npgsql tracing (`AddNpgsql`) plus the Npgsql meter `Npgsql`. Avoid extra `AddMeter` for framework meters already supplied by instrumentation. Verify actual instrument names against the Phase 0 selected versions before finalizing.
- [ ] Filter `/health` from ASP.NET Core **traces only**, with the same path guard ready for `/health/live` and `/health/ready` if added later. Inspect whether its database connectivity query still emits an orphan Npgsql root span. If it does, use a supported OTel instrumentation-suppression scope around that Api health-check operation, while leaving database metrics and the health response intact; do not make Infrastructure depend on HTTP context. Do not add custom spans, per-request logs, or custom counters.
- [ ] Honor standard `OTEL_TRACES_SAMPLER` and `OTEL_TRACES_SAMPLER_ARG`: Development parent-based always-on; Production `parentbased_traceidratio` with a documented initial ratio of `0.10`, overridable by deployment. Do not set a fixed sampler in code that masks the environment variables. Confirm this works with the pinned .NET SDK.
- [ ] In `PersistenceInstaller`, register one application-lifetime `NpgsqlDataSource` whose diagnostics `Name` is a fixed bounded value such as `KahootPrimary` and whose connection string comes from validated `DatabaseOptions`. Map the four PostgreSQL enums at the data-source level as well as the existing EF provider level. Pass that same data source to EF `UseNpgsql`, and inject it into migration and seeding services for `OpenConnectionAsync`; preserve transaction/advisory-lock semantics, timeouts, and disposal ownership.
- [ ] Confirm there is no remaining `new NpgsqlConnection(connectionString)` path producing an unnamed pool. Review emitted Npgsql spans and metrics for SQL/parameter/connection-string exposure. Keep span names stable, SQL parameter capture disabled, and `Database:EnableSensitiveDataLogging=false` in Production.
- [ ] Configure the existing console provider with `Logging:Console:FormatterName=json` and `IncludeScopes=true` in Production, with a single-line UTC timestamp. Keep Production `Default=Warning`, set `Kahoot=Information`, and retain/adjust `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `Npgsql`, and `OpenTelemetry` at Warning after inspecting emitted categories; Development can retain its readable formatter. Register OTel logging as an additional provider with structured state/scopes and active TraceId/SpanId; do not call `ClearProviders()` or register a second console provider.
- [ ] Add the same new variable names to all three `.env.*` templates: `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_TRACES_SAMPLER`, `OTEL_TRACES_SAMPLER_ARG`. Use `http://otel-collector:4318` and `http/protobuf` for Compose defaults. Keep Development sampled fully; use `parentbased_traceidratio`/`0.10` as the Production example. Add `OTEL_RESOURCE_ATTRIBUTES` only if actually used by the selected resource setup; never place secrets there.

## Configuration/environment variables

The standard `OTEL_*` variables above are process configuration. `Logging:Console` and `Logging:LogLevel` remain ASP.NET Core configuration. `ConnectionStrings__DefaultConnection` and existing `Database` options remain the only database configuration source. In this phase the .NET app can be verified against a reachable external/local Collector; Compose pass-through and local Collector are added in Phase 3. Host-run API must use a host-reachable endpoint override instead of Docker DNS.

## Instrumentation covered

Inbound HTTP and outbound `HttpClient` spans/metrics, Npgsql database spans and pool/operation metrics, .NET runtime/process metrics supported by the pinned package, and `ILogger<T>` log export/correlation. The current backend has no real `HttpClient` or SignalR call path, so configuration can be built now but those path-specific runtime claims wait for an actual call path.

## Testing and validation

- From `backend/`: `dotnet restore Kahoot.slnx`, `dotnet build Kahoot.slnx`, and `dotnet format Kahoot.slnx --verify-no-changes`; use repository's available .NET executable. Do not create automated tests.
- Inspect `dotnet list src/Kahoot.Api/Kahoot.Api.csproj package --include-transitive` for intended versions and accidental duplicate exporter/instrumentation packages.
- With a reachable Collector, make one normal API request and one DB-backed request; inspect one JSON console record, a request span with a DB child, HTTP/runtime/Npgsql metrics, and an OTLP log with trace context. Confirm `/health` remains available without a request span or orphan DB span.
- Inspect pool labels and span attributes for connection strings/credentials. Exercise migration and seeding startup against a disposable database or existing safe local DB, verifying advisory locks and enum mappings still work. Do not reset user data or run `docker compose down -v`.
- Disconnect Collector and confirm API health/business response and JSON console logging continue; any telemetry errors stay bounded and do not leak endpoint credentials.

## Acceptance criteria

- API has one coherent OTel registration and only the Api project owns OTel packages/exporters.
- Logs/traces/metrics use the same resource identity and reach one configurable OTLP endpoint; console logging is independent.
- Npgsql metrics use a safe bounded pool name and no raw startup connection path exposes a connection string in telemetry.
- Production sampling is configurable and parent-consistent; health tracing is filtered without disabling `/health`.
- Existing PostgreSQL migration/seeding behavior, API routes, and schema remain unchanged.

## Dependencies on previous phases

Phase 0 version record and Phase 1 documentation alignment.

## References for execution

- [OpenTelemetry .NET logs](https://opentelemetry.io/docs/languages/dotnet/logs/getting-started-aspnetcore/), [sampling](https://opentelemetry.io/docs/languages/dotnet/sampling/), and [OTLP endpoint semantics](https://opentelemetry.io/docs/languages/sdk-configuration/otlp-exporter/).
- [Microsoft JSON console formatter configuration](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/console-log-formatter).
- [Npgsql metrics and pool-name warning](https://www.npgsql.org/doc/diagnostics/metrics.html); [external data source with EF Core](https://www.npgsql.org/efcore/).
