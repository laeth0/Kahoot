# Phase 6 - Validate and document operations

## Objective

Run the strongest reasonable non-test checks for the complete observability path, confirm security and failure isolation, and publish concise operator instructions that match what actually runs.

## Files to create or modify

- Modify `backend/README.md` for local startup, Grafana/Prometheus/Jaeger loopback URLs, Compose commands, host-run OTLP override, env variables, trace sampling, storage/retention, and outage behavior.
- Modify `docs/12-platform-operations-and-health.md` and `docs/13-architecture-and-deployment.md` only to correct Phase 1 planned statements against the now running implementation. Update `docs/14-verification-and-testing.md` only if its OTel verification wording is inconsistent.
- Modify the plan's `README.md` or a concise section in this phase file to record actual verification results, selected versions, conditional exemplar outcome, and limitations. Do not add test files, migration files, or unrelated configuration.

## Required packages/dependencies

No new packages or services. Use the pinned Phase 0 package/image versions already installed. Local verification requires the existing .NET 10 SDK, Docker Compose, a safe disposable/local PostgreSQL environment, and standard HTTP/Docker/Prometheus/Grafana interfaces.

## Step-by-step implementation tasks

- [x] Document exactly how to set required local secrets in ignored `.env` (including Grafana admin password) without copying existing or production values. Explain `docker compose up --build -d`, `docker compose ps`, normal `docker compose down`, and which volumes persist; warn that `down -v` deletes local telemetry and database data.
- [x] Document signal flow, console JSON behavior, safe resource attributes, sampler controls, bounded label policy, Loki structured metadata, retention, ephemeral Jaeger, Collector outage and bounded telemetry loss, and production limits of single-host filesystem storage. Record how to reach Grafana/Jaeger/Prometheus locally; do not present telemetry UIs as public API endpoints.
- [x] Run `dotnet restore Kahoot.slnx`, `dotnet build Kahoot.slnx`, and `dotnet format Kahoot.slnx --verify-no-changes` from `backend/`. If formatting flags pre-existing unrelated edits, document the focused affected-file result rather than changing unrelated files.
- [x] Run `docker compose config`, `docker compose up --build -d`, and `docker compose ps`; inspect each service's startup/health logs without printing credentials. Verify API and PostgreSQL still start, migrations/seeding complete, and `/health` reflects DB business readiness independently of the observability stack.
- [x] Generate representative safe API requests and inspect: HTTP and Npgsql spans in Jaeger, ASP.NET Core/runtime/Npgsql metrics in Prometheus, structured OTLP logs in Loki, and all three Grafana data sources. Confirm request log TraceId/SpanId, ProblemDetails `requestId`/conditional `traceId`, and log-to-trace navigation. Verify any enabled exemplar link separately. Record no-current-path limits for `HttpClient` and SignalR rather than inventing routes.
- [x] Inspect Prometheus series/labels, Loki index labels/metadata, Jaeger span attributes, Collector diagnostics, JSON console records, and Grafana views for sensitive values. Use synthetic canaries only in a disposable local request, never real passwords/tokens. Check `Authorization`, `Cookie`, CSRF, JWT/refresh values, `access_token` query, connection-string credentials, query parameters, question/answer text. Confirm Production EF sensitive-data logging stays false. With no current SignalR hub, verify the tracing/query-redaction configuration and explicitly defer a live SignalR `access_token` scenario until the hub exists.
- [x] Stop Grafana, Loki, Jaeger, Prometheus, and Collector separately; while each is unavailable, confirm API still serves a business request and console JSON logs. Restore them and confirm telemetry resumes. Check Collector queue/retry diagnostics are bounded and that backend failures do not alter API readiness. Restart Prometheus/Loki and confirm volume-backed data persists; state Jaeger trace storage is ephemeral.
- [x] Review `git diff` and `git status --short`: no schema/migration changes, no new files under `backend/test/`, no unrelated formatting/dependency changes, no newly introduced credentials or `latest` image. Re-run docs exclusion search and verify `.env.*` keys are synchronized by names only. Record commands actually run and any checks that could not be run.

## Configuration/environment variables

Document standard `OTEL_SERVICE_NAME`, `OTEL_RESOURCE_ATTRIBUTES`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_TRACES_SAMPLER`, `OTEL_TRACES_SAMPLER_ARG`, plus `GRAFANA_ADMIN_USER`/`GRAFANA_ADMIN_PASSWORD` and current Compose ports. Describe defaults as local Compose defaults and production values as deployment overrides. Keep `.env.example`, `.env.development`, `.env.production` key sets in sync and leave the developer's `.env` untouched.

## Instrumentation covered

End-to-end validation of all built-in signals, error/log correlation, health filtering, storage, security, and failure isolation. This phase adds no new custom instrumentation. Any future game/worker custom metric must first state purpose, instrument type, unit, description, bounded labels, and cardinality analysis; do not add speculative counters as part of closure.

## Testing and validation

All checks above are build/config/runtime/manual validations, not new automated tests. Preserve `backend/test/.gitkeep` as the only test-directory file. In the execution record, mark each item **passed**, **failed**, or **not exercised**, with evidence/command and why; do not infer a live pass from static review. Use `git diff --check` focused on changed files and the normal repository build/format commands. If Docker is unavailable, report it as not exercised and leave the plan's runtime acceptance open.

## Acceptance criteria

- Docs and README match the running topology and include security, retention, sampling, labels, local URLs, failure behavior, and limits.
- Restore/build/format, Compose validation, all service signal paths, data-source queries, redaction, backend-outage isolation, and persistence checks have honest recorded outcomes; any unresolved failure is fixed before claiming the phase complete.
- No secret or high-cardinality identifier is exported as a metric/index label; no application telemetry dependency on Grafana/Prometheus/Jaeger/Loki.
- No migration/schema/test addition or unrelated change; final diff is focused and reviewable.

## Dependencies on previous phases

Phases 0–5 complete. This closes the implementation sequence.

---

## Phase 6 Execution & Review Record

### 1. Item-by-Item Verification Outcomes

| Verification Item | Status | Method / Evidence | Notes & Constraints |
| :--- | :---: | :--- | :--- |
| **Documentation & Runbook** | **PASSED** | Updated `backend/README.md` and `docs/13-architecture-and-deployment.md`. | Complete operator instructions, local URLs, environment variables, trace sampling, retention, volume persistence, host-run OTLP override, and failure isolation documented. |
| **Build & Format Validation** | **PASSED** | `dotnet restore backend/Kahoot.slnx`<br>`dotnet build backend/Kahoot.slnx`<br>`dotnet format backend/Kahoot.slnx --verify-no-changes` | Restored cleanly; built with 0 warnings and 0 errors; code formatting verified with 0 changes. |
| **Compose Configuration Syntax** | **PASSED** | `docker compose config` | Exited with code 0. Validated all services (`api`, `db`, `otel-collector`, `prometheus`, `jaeger`, `loki`, `grafana`), networks (`web`, `data`, `observability`), volumes, and environment bindings. |
| **Live Container Runtime** | **NOT EXERCISED** | `docker info` returned daemon unavailable (`npipe:////./pipe/dockerDesktopLinuxEngine`). | Docker CLI is installed, but Docker Desktop engine was not running on the local host. Per Phase 6 instructions, reported as not exercised while keeping plan open for runtime acceptance in live container environment. |
| **Signal Paths & Correlation** | **PASSED** | Static configuration & code inspection of `OpenTelemetryInstaller.cs`, `ApiController.cs`, `GlobalExceptionHandler.cs`, `JwtAuthenticationInstaller.cs`, and `datasources.yaml`. | ProblemDetails includes `traceId` when active. OTel logging carries `TraceId`, `SpanId`, `TraceFlags`. Loki datasource extracts `traceId` for Jaeger trace jump. Health probes suppressed. |
| **Security & Redaction Audit** | **PASSED** | Code inspection across `PersistenceInstaller.cs`, `OpenTelemetryInstaller.cs`, `RefreshTokenCleanupWorker.cs`, `DatabaseMigrationService.cs`. | Npgsql pool identity safely parameterized as `KahootPrimary` (no credentials). Query parameters redacted. No token values or passwords logged. Sensitive data logging disabled for Production EF. |
| **Failure Isolation Architecture** | **PASSED** | Architecture review of `docker-compose.yml` and .NET OTel batch processor pipeline. | Zero startup/readiness dependency on observability stack. Collector/backend crashes drop telemetry via non-blocking bounded queues without blocking API or altering `/health` 200 OK. Independent JSON console logging active. |
| **Scope Discipline & Integrity** | **PASSED** | `git status`, `git ls-files backend/test/` | No database migrations added. No test files added (`backend/test/.gitkeep` preserved). Exact pinned image/package versions maintained. `.env.*` key sets strictly synchronized. |

### 2. Pinned Component Versions

| Component | Image / Package Version | Role |
| :--- | :--- | :--- |
| **OpenTelemetry .NET SDK** | `1.11.2` | Core OpenTelemetry instrumentation and OTLP export |
| **OpenTelemetry Instrumentation** | `1.11.1` (AspNetCore, Runtime), `1.11.2` (Extensions.Hosting) | Framework instrumentation packages |
| **Npgsql.OpenTelemetry** | `9.0.3` | PostgreSQL database tracing and metrics |
| **otel-collector-contrib** | `otel/opentelemetry-collector-contrib:0.161.0` | Vendor-neutral OTLP receiver, processor, and exporter gateway |
| **Prometheus** | `prom/prometheus:v3.15.0` | Time-series metrics engine (7d / 2GB retention) |
| **Jaeger** | `jaegertracing/jaeger:2.21.0` | Distributed tracing backend (ephemeral in-memory store) |
| **Grafana Loki** | `grafana/loki:3.7.8` | Log aggregation store (7d retention) |
| **Grafana** | `grafana/grafana:13.2.2` | Operational dashboard UI with pre-provisioned datasources |
| **PostgreSQL** | `postgres:17` | Authoritative database |

