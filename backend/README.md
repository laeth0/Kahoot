# Backend setup

## Run locally with Docker Compose

From the repository root, copy `.env.example` to `.env` and configure your local secrets. For example, in PowerShell:

```powershell
Copy-Item .env.example .env
```

Set the required passwords in `.env`:
- `POSTGRES_PASSWORD`: Use a unique password without connection-string delimiters (`;` or `=`).
- `GRAFANA_ADMIN_PASSWORD`: A unique administrator password for the local Grafana dashboard.

Then build and launch all containers:

```sh
docker compose up --build -d
docker compose ps
```

### Local Service & Telemetry Endpoints

All telemetry user interfaces bind exclusively to loopback (`127.0.0.1`) and are intended for local development and operational administration—they are never public API endpoints:

| Service | Internal / Host URL | Description |
| :--- | :--- | :--- |
| **API** | `http://localhost:8080` | Platform REST API |
| **Health Check** | `http://localhost:8080/health` | Database-aware readiness probe |
| **PostgreSQL** | `localhost:5433` (container :5432) | Relational database (mapped to host `5433` by default) |
| **Grafana** | `http://localhost:3000` | Unified exploration UI (User: `admin`, Password: `GRAFANA_ADMIN_PASSWORD`) |
| **Jaeger UI** | `http://localhost:16686` | Distributed trace search and dependency graphs |
| **Prometheus UI** | `http://localhost:9090` | Time-series metrics and PromQL query console |
| **OTel Collector** | `http://localhost:4318` (OTLP HTTP) | Vendor-neutral OpenTelemetry gateway; OTLP gRPC is available only inside the Compose network |

Set `API_PORT` or `POSTGRES_PORT` in `.env` if those host ports are occupied. Compose starts PostgreSQL, waits for it to become healthy, and starts the API, which applies pending EF Core migrations during startup.

### Volume Persistence & Data Retention

The following Docker named volumes persist across normal `docker compose down`:
- `postgres_data`: Relational database storage.
- `image_uploads`: Sanitized question images and temporary upload staging under `/app/wwwroot/uploads`.
- `prometheus_data`: Time-series metrics with a retention window of 7 days (`--storage.tsdb.retention.time=7d`) and a 2GB ceiling.
- `loki_data`: Structured application logs with a retention window of 7 days (`retention_period: 168h`).
- `grafana_data`: Grafana state, dashboard preferences, and credentials.

> [!WARNING]
> Running `docker compose down -v` permanently destroys local database data, uploaded images, and collected telemetry storage volumes. Use `docker compose down` for normal stopping and restarts.

When running API replicas on different hosts, mount the same durable, writable image volume at `/app/wwwroot/uploads` on every replica. Each replica serves public image URLs and runs orphan cleanup; independent local volumes would produce missing images and incomplete cleanup. The staging directory must remain inaccessible to public static-file servers.

> [!NOTE]
> In the single-host baseline deployment, Jaeger uses its default in-memory trace storage. Traces are ephemeral and reset whenever the Jaeger container restarts.

---

## Observability Architecture & Signal Flow

The platform uses a vendor-neutral, three-signal telemetry pipeline conforming to OpenTelemetry standards:

```
[ Kahoot.Api (.NET) ]
    │  ├─► OTLP HTTP (:4318) ────────┐
    │  └─► JSON Formatted Console ───┼─► stdout / stderr
    ▼                                ▼
[ OpenTelemetry Collector Contrib ]
    ├──► Prometheus Exporter (:9464) ───► Scraped by Prometheus (:9090)
    ├──► OTLP gRPC (:4317) ─────────────► Jaeger (:16686)
    └──► OTLP HTTP (/otlp) ─────────────► Loki (:3100)
                                              │
[ Grafana Dashboard (:3000) ] ◄───────────────┘ (Prometheus, Jaeger, Loki provisioned)
```

1. **Application Telemetry**: `Kahoot.Api` exports distributed traces (ASP.NET Core, Npgsql), metrics (ASP.NET Core, .NET Runtime, Npgsql), and structured logs over standard OTLP (HTTP/protobuf) exclusively to the OpenTelemetry Collector gateway.
2. **Collector Routing**: The Collector batches and routes traces to Jaeger, exposes a Prometheus pull endpoint (`:9464`) scraped every 15s by Prometheus, and forwards logs to Loki via native OTLP HTTP ingestion.
3. **Unified Exploration in Grafana**: Grafana is pre-provisioned with Prometheus, Jaeger, and Loki data sources. Loki logs link to Jaeger traces through their `trace_id` structured metadata when the trace was sampled and retained.
4. **ProblemDetails Trace Correlation (`OPS-OBS-004`)**: When an active distributed trace exists, RFC 7807 `ProblemDetails` error responses include `["traceId"] = Activity.Current.TraceId.ToString()`, alongside `requestId` (`TraceIdentifier`), enabling instant lookup in Grafana and Jaeger from client-reported errors.
5. **Telemetry Failure Isolation (`OPS-OBS-002`)**:
   - The application does not depend on the Collector or observability backends for readiness or startup.
   - The .NET OTel SDK uses bounded batch queues and asynchronous export. If the Collector or backends become unavailable, telemetry may be dropped; repeated export failures still consume some CPU and network resources but do not determine `/health` readiness.
   - Local structured JSON console logs to `stdout`/`stderr` remain fully operational during any telemetry outage.

---

## Running the API on the Host (`dotnet run`)

To run the API directly on your development workstation while keeping PostgreSQL and the observability stack in Docker Compose:

1. Start the supporting infrastructure in Compose:
   ```sh
   docker compose up -d db otel-collector prometheus jaeger loki grafana
   ```
2. Provide your connection string pointing to the published PostgreSQL port (`5433` by default) and override the OTLP endpoint to localhost:
   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5433;Database=kahoot;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
   $env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4318"
   $env:OTEL_EXPORTER_OTLP_PROTOCOL = "http/protobuf"
   dotnet run --project src/Kahoot.Api
   ```
   (In Compose, `OTEL_EXPORTER_OTLP_ENDPOINT` defaults to `http://otel-collector:4318`; running on the host requires `http://localhost:4318`).

---

## Configuration & Environment Variables

| Variable | Default (Dev) | Production Pattern | Purpose |
| :--- | :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Production` | Runtime mode. Production disables Swagger and debug endpoints. |
| `ConnectionStrings__DefaultConnection` | Appsettings / Env | `Host=...;Pooling=true;...` | Npgsql connection string with bounded pool. |
| `OTEL_SERVICE_NAME` | `kahoot-api` | `kahoot-api` | OpenTelemetry logical service identity. |
| `OTEL_RESOURCE_ATTRIBUTES` | `deployment.environment.name=development` | `deployment.environment.name=production` | Standard resource metadata tags; do not put secrets or private user data here. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://otel-collector:4318` | `http://otel-collector:4318` | OTLP HTTP receiver endpoint (`http://localhost:4318` when host-run). |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` | `http/protobuf` | Telemetry transmission protocol. |
| `OTEL_TRACES_SAMPLER` | `always_on` | `parentbased_traceidratio` | Distributed trace sampling policy. |
| `OTEL_TRACES_SAMPLER_ARG` | `1.0` | `0.10` | Sampling ratio (e.g., `0.10` = 10% in production). |
| `GRAFANA_ADMIN_USER` | `admin` | `admin` | Grafana administrator login username. |
| `GRAFANA_ADMIN_PASSWORD` | Configured in `.env` | Injected secret | Grafana administrator password; Compose requires a nonempty value. |

### Data Protection & Redaction Guardrails
- **Sensitive Data**: HTTP spans omit raw inbound paths and queries, outbound full URLs, inbound host headers, and user agents. Route templates, methods, status codes, and outbound dependency hosts remain available. No request or response headers or bodies are captured. Do not log credentials, tokens, cookies, or private user data in application messages or resource attributes.
- **Connection String Protection**: Npgsql data source is registered with a safe parameter-free name (`KahootPrimary`). Raw connection strings with passwords are never exposed to metrics or trace attributes.
- **Health Check Filtering**: High-frequency synthetic health checks (`/health`, `/health/live`, `/health/ready`) are filtered out of distributed tracing to prevent trace volume bloat.
- **Low Cardinality**: Prometheus copies only selected resource attributes into labels; the service instance remains distinct across replicas. Metric dimensions and Loki index labels avoid high-cardinality values (such as game PINs, player nicknames, answer IDs, or timestamps).
- **Deployment Boundary**: Compose binds observability UIs and the OTLP HTTP receiver to loopback. The single Collector and Jaeger's in-memory storage are a single-host reference setup; multi-host production requires protected transport, access control, and durable trace storage.

---

## Database Migrations & Multi-Replica Startup

The API requires a valid database connection at startup and automatically applies pending EF Core migrations. Migration failures stop the application.

This branch currently has no EF migration files. A fresh database cannot be initialized from this branch until a baseline migration is generated and reviewed.

Replicas coordinate migrations using a PostgreSQL transaction-level advisory lock (`pg_advisory_xact_lock`). A waiting replica begins serving traffic only after it acquires the lock and EF Core confirms migrations are applied. Transient connection failures before migration begins are retried with bounded backoff. Migration commands use `Database:MigrationCommandTimeoutSeconds` (300 seconds by default); lock acquisition timeout during migration is bounded to 5 seconds.

---

## Commit Formatting

Enable the shared pre-commit hook once per clone from the repository root:

```sh
git config --local core.hooksPath .githooks
```

The hook runs `dotnet format` on `backend/Kahoot.slnx`. If formatting changes files, the commit stops so you can review and stage the formatted files before retrying.
