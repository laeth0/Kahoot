# Phase 0 - Analyze backend and map requirements

## Objective

Revalidate the current backend and turn [`prompts/OpenTelemetry.md`](../prompts/OpenTelemetry.md) into a concrete file, dependency, security, and verification map before any application or deployment edit. This phase is read-only for product files; record findings in this plan folder if the repository has moved.

## Observed backend snapshot (2026-09-27)

| Area | Current implementation | Required mapping |
| --- | --- | --- |
| Architecture | `backend/Kahoot.slnx`: Api → Application and Infrastructure; Infrastructure → Application; Application → Domain. | SDK/exporter registration in Api only. Keep `ILogger<T>` in workers/handlers; no Domain dependency. |
| Startup | `backend/src/Kahoot.Api/Program.cs` registers controllers, `GlobalExceptionHandler`, ProblemDetails, database health, Application/Infrastructure, CORS/JWT, then middleware and routes. | Focused `AddObservability` in Api; retain middleware order and startup behavior. |
| Persistence | `PersistenceInstaller.cs` configures EF Core/Npgsql; `DatabaseMigrationService.cs` and `DatabaseSeeder.cs` open separate raw `NpgsqlConnection`s. | Instrument Npgsql tracing/metrics. Use one safely named `NpgsqlDataSource` for EF and raw startup paths before exporting Npgsql pool metrics. Preserve enum mappings and advisory locks. |
| Workers/logging | Migration, seeder, cleanup, suspension finalizer, bootstrap seeder, exception handler, and socket eviction use structured `ILogger<T>` templates. Cleanup logs each pass at Information and each failed attempt at Error. | Keep templates; change cleanup retry/zero-deletion levels in Phase 5. Avoid duplicate exception logging and noisy request logs. |
| HTTP/auth | Controllers and JWT challenge/forbidden responses emit ProblemDetails with `requestId`; `GlobalExceptionHandler` handles expected validation/rate-limit and unexpected/DB failures. | Preserve status/body contracts and add `traceId` when active. No password/cookie/header capture. |
| Health | Only `/health` is implemented and checks database connectivity; `/health/live` and `/health/ready` are specified in docs but absent in code. | Filter existing `/health` from tracing and reserve the other two paths for later implementation; do not add health endpoints as telemetry work. |
| Outbound/realtime | No registered `HttpClient` or SignalR hub in current backend. | Register outbound instrumentation for future calls but report it unexercised now. Do not create a hub or synthetic business path. |
| Logging config | Appsettings set `Logging:LogLevel`; no explicit JSON formatter/OTel provider. Production defaults to Warning with selected startup categories at Information. EF sensitive-data logging is false in all current appsettings files. | JSON console in Production, additional OTLP provider, sane category levels, no `ClearProviders()`. Keep sensitive-data logging disabled. |
| Docker | Root `docker-compose.yml` has `api` and PostgreSQL, `web`/`data` networks, loopback host ports, DB health dependency, and `postgres_data` volume. | Add internal observability network and five pinned services; keep DB and API readiness independent of observability. |
| Requirements | `docs/12-platform-operations-and-health.md` mentions OTel tracing/metrics and local logs; `docs/13-architecture-and-deployment.md` describes baseline topology without observability stack; `docs/14-verification-and-testing.md` treats OTel as conditional. | Phase 1 updates affected requirements before code, retaining existing SLOs. Current docs search found no literal central-observability ban; recheck before editing. |

## Files to create or modify

- Read: `AGENTS.md`, `backend/AGENTS.md`, `prompts/OpenTelemetry.md`, `docs/*.md`, `docker-compose.yml`, three tracked `.env.*` templates, `backend/Kahoot.slnx`, relevant `.csproj`, `Program.cs`, appsettings, middleware, health, auth, migration/seeding/workers, and registration extensions.
- Modify only if needed to record current results: this Phase 0 file and the version/decision section of this folder's `README.md`. Do not touch `.env`, backend code, `docs/`, migrations, or `backend/test/` in Phase 0.

## Required packages/dependencies

None installed. Inventory current .NET 10, EF Core 10.0.12, and Npgsql EF provider 10.0.3 packages; inspect transitive Npgsql version before selecting `Npgsql.OpenTelemetry`. Record exact stable compatible NuGet versions for `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `.Http`, `.Runtime`, and `Npgsql.OpenTelemetry`, plus exact image tags for Collector contrib, Prometheus, Jaeger, Loki, Grafana. Use official release notes/package pages and reject prerelease tags. These are the fixed inputs to later phases, not a reason to redesign their topology.

## Step-by-step implementation tasks

- [x] Capture `git status --short` and preserve unrelated edits. Read the two AGENTS files and relevant docs; do not inspect `frontend/` or print values from secret-bearing `.env.*` files.
- [x] Re-run targeted `rg` searches for `ILogger`, log methods, `LoggerMessage`, `BeginScope`, `EventId`, `TraceIdentifier`, `ActivitySource`, `Meter`, OTel/backend names, and observability requirements in `backend/src`, `docs`, root Compose/config files, and the prompt. Search filenames or keys only for `.env.*`.
- [x] Draw the current startup and telemetry flow from `Program.cs`, `PersistenceInstaller`, `DatabaseMigrationService`, `DatabaseSeeder`, `SystemAdminSeeder`, `RefreshTokenCleanupWorker`, `SuspensionFinalizerWorker`, auth controllers, JWT installer, health check, and `GlobalExceptionHandler`. Confirm direct Npgsql connection creation and any new `HttpClient`/SignalR code.
- [x] Review every requirement section in the prompt (architecture, resources, all three signals, security, sampling, storage, correlation, Compose, docs, verification) against the phases in README; add a missing task to the owning phase if found.
- [x] Resolve and record exact package/image tags and compatible configuration syntax from versioned official sources. Confirm Npgsql meter/instrument names and its default pool-name behavior; confirm Collector exporter names, Loki OTLP label mapping, and Jaeger OTLP listener.
- [x] Record any new findings and the locked version matrix in this file. Mark current `docs/` statements as confirmed conflict, incomplete, or already compatible; preserve all unrelated requirements/SLOs.

## Locked Stable Version Matrix (2026-09-27)

### NuGet Packages (.NET 10)
| Package | Version | Justification & Target |
| --- | --- | --- |
| `Npgsql.OpenTelemetry` | `10.0.3` | Matches `Npgsql` and `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3` installed in `Kahoot.Infrastructure`. |
| `OpenTelemetry.Extensions.Hosting` | `1.19.1` | Latest stable .NET hosting integration for OpenTelemetry SDK. |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | `1.19.1` | Latest stable OTLP gRPC/HTTP exporter. |
| `OpenTelemetry.Instrumentation.AspNetCore` | `1.19.0` | Latest stable ASP.NET Core inbound HTTP tracing & metrics. |
| `OpenTelemetry.Instrumentation.Http` | `1.19.0` | Latest stable `HttpClient` outbound tracing & metrics. |
| `OpenTelemetry.Instrumentation.Runtime` | `1.19.0` | Latest stable .NET runtime metrics (GC, memory, thread pool). |

### Docker Images (Pinned)
| Component | Image & Tag | Ports & Role |
| --- | --- | --- |
| OpenTelemetry Collector | `otel/opentelemetry-collector-contrib:0.161.0` | Receives OTLP HTTP (4318) and gRPC (4317); exports Prometheus (9464), OTLP/Jaeger, and OTLP/Loki. |
| Prometheus | `prom/prometheus:v3.15.0` | Scrapes Collector port 9464; persistent volume retention (15d); queries on 9090. |
| Jaeger | `jaegertracing/jaeger:2.21.0` | Jaeger v2 native OTLP receiver (4317); UI and query API on 16686. |
| Loki | `grafana/loki:3.7.8` | Ingests OTLP HTTP logs (`/otlp/v1/logs`) on port 3100; retention 7d. |
| Grafana | `grafana/grafana:13.2.2` | Provisions Prometheus, Jaeger, and Loki data sources; UI on 3000. |

## Detailed Analysis & Findings

1. **Architecture & Clean Boundaries**:
   - Telemetry registration belongs strictly in `Kahoot.Api` via a focused `AddObservability` extension method.
   - `Kahoot.Domain` and `Kahoot.Application` remain free of any telemetry SDKs.
   - Existing `ILogger<T>` calls across all application and infrastructure services seamlessly feed the OpenTelemetry logger provider.

2. **Npgsql Connection Pool Identity & Direct Connections**:
   - Npgsql 10 publishes metrics under the `Npgsql` meter name.
   - By default, Npgsql uses connection strings as pool identifiers, which risks exposing credentials in Prometheus labels.
   - **Resolution for Phase 2**: Register a singleton `NpgsqlDataSource` configured with safe pool identity (`kahoot-db`) in `PersistenceInstaller.cs`. Both EF Core (`UseNpgsql(dataSource)`) and direct startup tools (`DatabaseMigrationService`, `DatabaseSeeder`) will consume this shared data source, preventing connection string leakage and sharing pool efficiency.

3. **ProblemDetails Trace Correlation**:
   - Current ProblemDetails generation in `ApiController`, `GlobalExceptionHandler`, and `JwtAuthenticationInstaller` (OnChallenge & OnForbidden) uses `["requestId"] = httpContext.TraceIdentifier`.
   - **Resolution for Phase 2 & 5**: Add `["traceId"] = Activity.Current.TraceId.ToString()` conditionally when `Activity.Current != null`, keeping `requestId` intact for 100% backward compatibility.

4. **Health Check Trace Filtering**:
   - Current health check `/health` will be filtered out in `AspNetCoreInstrumentationOptions.Filter` (`httpContext => !httpContext.Request.Path.StartsWithSegments("/health")`) to prevent log/trace pollution.

5. **Documentation Alignment (Phase 1 Target)**:
   - `docs/12-platform-operations-and-health.md`: Update Section 1 & Section 2.6 to document central OTLP log forwarding alongside JSON console logging.
   - `docs/13-architecture-and-deployment.md`: Add Section documenting the observability stack topology and `observability` network.
   - `docs/14-verification-and-testing.md`: Document verification criteria for the three signals.
   - Confirm: Zero conflicts with business logic or performance SLOs (`ACCT-SLO-*`, `OPS-SLO-*`).

## Configuration/environment variables

Inventory only. Later phases use `OTEL_SERVICE_NAME`, `OTEL_RESOURCE_ATTRIBUTES`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_TRACES_SAMPLER`, and `OTEL_TRACES_SAMPLER_ARG`; verify .NET SDK support for the selected version. Inspect `.env.example`, `.env.development`, and `.env.production` by **variable name only**. Do not read or reproduce their existing values in the record.

## Instrumentation covered

Mapping only: ASP.NET Core inbound, future HttpClient outbound, .NET runtime, Npgsql spans and pool metrics, `ILogger<T>` logs, health-trace exclusion, ProblemDetails correlation, and optional exemplars. No collector/exporter is enabled in this phase.

## Testing and validation

- Compare the map to `prompts/OpenTelemetry.md` sections 1–54 and the core objective; check that each requirement belongs to a later phase or is explicitly conditional.
- Check the plan's file paths against `rg --files backend -g '!**/bin/**' -g '!**/obj/**'`, excluding `frontend/`.
- `git status --short` must show only this plan folder as the phase's new work; no backend/test or migration edit.

## Acceptance criteria

- Current architecture and docs conflicts are accurately recorded; no obsolete ban is assumed without an actual source line.
- Exact stable version matrix and any version-specific syntax decision are recorded before Phase 1/2/3 execution.
- Npgsql connection-string-as-pool-name exposure is addressed by the Phase 2 task, based on [Npgsql metrics documentation](https://www.npgsql.org/doc/diagnostics/metrics.html); direct startup connections are included.
- Every prompt requirement has an owning phase or a documented conditional/deferred outcome.

## Dependencies on previous phases

None. Execute first.

## Reference checks already established

- [Npgsql tracing](https://www.npgsql.org/doc/diagnostics/tracing.html) uses `Npgsql.OpenTelemetry`; [Npgsql 10 metrics](https://www.npgsql.org/doc/diagnostics/metrics.html) come from meter `Npgsql`, and the default pool-name dimension can be the connection string.
- [Npgsql EF Core](https://www.npgsql.org/efcore/) supports a provided `NpgsqlDataSource` for `UseNpgsql`.
- [OpenTelemetry .NET sampling](https://opentelemetry.io/docs/languages/dotnet/sampling/) documents `parentbased_traceidratio` and standard sampler variables.
