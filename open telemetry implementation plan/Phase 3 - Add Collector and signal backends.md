# Phase 3 - Add Collector and signal backends

## Objective

Provide the three local/single-host telemetry paths in Compose without creating an application availability dependency: API → Collector; Collector → Jaeger and Loki; Prometheus scrapes the Collector.

## Files to create or modify

- Create `observability/otel-collector.yaml`, `observability/prometheus.yaml`, and `observability/loki.yaml` at repository root.
- Modify root `docker-compose.yml` to add `otel-collector`, `prometheus`, `jaeger`, `loki`, and an `observability` network plus `prometheus_data` and `loki_data` volumes. Grafana service/provisioning is Phase 4.
- Modify `.env.example`, `.env.development`, `.env.production` together only if new host-port or retention override variable keys are truly needed. Prefer explicit non-secret Compose/config values for local ports and retention; do not add variables merely to parameterize constants.
- Do not modify `backend/Dockerfile` unless a verified compatibility or health-check requirement demands it.

## Required packages/dependencies

No new NuGet packages. Pin the exact Phase 0 stable image tags for `otel/opentelemetry-collector-contrib`, `prom/prometheus`, `jaegertracing/jaeger` (current supported all-in-one/single-node image), and `grafana/loki`. Do not use `latest`, deprecated Collector Loki/Jaeger exporters, Prometheus remote-write receiver, or extra storage servers.

## Step-by-step implementation tasks

- [x] Extend Compose with one internal `observability` network. Attach API to it in addition to its existing networks; attach Collector, Prometheus, Jaeger, and Loki to it. Keep PostgreSQL only on `data`. Add API `OTEL_*` pass-through from the synchronized templates; do **not** add API `depends_on` for Collector/Jaeger/Loki/Prometheus.
- [x] Configure Collector contrib OTLP receivers for gRPC `0.0.0.0:4317` and HTTP `0.0.0.0:4318`. Use separate traces, metrics, logs pipelines with `memory_limiter` before `batch`. Set explicit bounded processor/exporter memory and retry/queue settings supported by the pinned version; favor bounded telemetry loss during prolonged backend failure. Enable a supported internal health mechanism and nonrecursive Collector diagnostics.
- [x] Send traces from Collector to Jaeger over internal OTLP (prefer gRPC `jaeger:4317` if supported by the pinned Jaeger image). Use Jaeger's local all-in-one/single-node mode. Expose only its UI/query port `16686` on `127.0.0.1`; document in-memory/ephemeral trace storage.
- [x] Send logs via Collector standard `otlphttp` exporter to Loki base endpoint `http://loki:3100/otlp` (HTTP exporter appends `/v1/logs`). Enable structured metadata. Override Loki's OTLP default indexed-resource-label mapping to an explicit small set such as `service.name`, `service.namespace`, and `deployment.environment.name`; keep `service.instance.id`, trace IDs, request IDs, account/game IDs, URL, and exceptions out of index labels. Confirm the exact pinned Loki syntax before starting it.
- [x] Configure Loki single-binary TSDB/filesystem storage with `loki_data` volume, compactor retention enabled, and 7-day local retention. Use a supported schema start date and compactor delete-request store per the pinned Loki docs. Note time-based retention does not guarantee a disk byte cap; monitor local volume usage. Do not expose Loki HTTP to the host.
- [x] Export application metrics from Collector using its Prometheus exporter on internal port `9464`; configure Prometheus to scrape `otel-collector:9464` on a 15-second interval. Do not convert every resource attribute into a metric label; specifically keep `service.instance.id` out of series dimensions. Also scrape Collector self-metrics from its supported internal telemetry endpoint if available, without recursive export. Set `prometheus_data` volume and 7-day or 2-GiB retention ceiling (whichever trips first); do not enable remote-write receiving. Expose Prometheus UI `9090` on loopback only.
- [x] Keep Collector `4317/4318`, `9464`, Loki `3100`, and Jaeger OTLP internal. Add Docker health checks only after confirming the pinned image contains a reliable health probe command/tool; otherwise use container status plus documented HTTP endpoints, without a false unhealthy status. Do not tie API readiness to any such check.
- [x] Validate configuration and bring up services with the existing DB/API. Inspect Collector startup diagnostics for unsupported component names or retry queue settings and correct the version-specific config without changing the topology.

## Configuration/environment variables

API receives `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4318`, `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`, service name, and sampler settings through Compose. Internal destination endpoints are literal Docker service names in `otel-collector.yaml`/Prometheus config. No telemetry credentials are needed for local internal networking. External production Collector endpoints and auth, if later deployed, belong in deployment secrets/overrides rather than committed files.

## Instrumentation covered

Transport/storage for all signals from Phase 2. Prometheus receives ASP.NET Core/runtime/Npgsql metrics; Jaeger receives HTTP/Npgsql traces; Loki receives `ILogger<T>` OTLP logs. This phase adds no custom application spans, counters, or log calls.

## Testing and validation

- `docker compose config` and pinned Collector/Prometheus/Loki config validation mechanisms available in the chosen images; verify exact commands in Phase 0 version record.
- `docker compose up --build -d` then `docker compose ps`; inspect service logs and health without printing credentials.
- Use normal API requests to verify OTLP HTTP receive, Jaeger trace search, Prometheus `up{job=...}` and request/runtime/Npgsql series, and Loki query for `service_name="Kahoot.Api"` (allow for normalized Loki label names).
- Inspect Prometheus labels and Loki series labels for bounded values; in particular verify no connection string, `service_instance_id`, request/user/game ID, or token appears.
- Restart Prometheus and Loki normally and confirm their data survives; stop each backend and the Collector in turn and confirm API still serves business requests and writes JSON console logs. Do not remove volumes.

## Acceptance criteria

- All three signals arrive at their backends through Collector; no API-to-backend direct endpoint exists.
- Prometheus scrapes Collector; no API Prometheus endpoint or remote-write receiver is added.
- Local ports are loopback-only where exposed; DB isolation, pinned versions, retention, and persistent Prometheus/Loki volumes are present.
- Backend/Collector failure does not gate API startup/readiness; no unbounded queue is configured.

## Dependencies on previous phases

Phases 0–2 complete, especially API OTLP HTTP configuration and the safe Npgsql pool name.

## References for execution

- [Loki native OTLP ingestion and label mapping](https://grafana.com/docs/loki/latest/send-data/otel/), [Collector to Loki endpoint example](https://grafana.com/docs/loki/latest/send-data/otel/otel-collector-getting-started/).
- [Loki filesystem retention](https://grafana.com/docs/loki/latest/operations/storage/filesystem/) and [Prometheus local retention](https://prometheus.io/docs/prometheus/latest/storage/).

---

## Phase 3 Execution & Review Record

### 1. Implemented Components
- **`observability/otel-collector.yaml`**:
  - Pinned `otel/opentelemetry-collector-contrib:0.161.0`.
  - Configured OTLP receivers on gRPC `0.0.0.0:4317` and HTTP `0.0.0.0:4318`.
  - Configured `memory_limiter` (75% limit, 20% spike limit) and `batch` (send_batch_size: 512, timeout: 5s, max: 1024) across all pipelines.
  - Configured `resource/metrics` processor to strip `service.instance.id` from metric dimensions, preventing cardinality explosion.
  - Exporters:
    - Traces -> Jaeger over OTLP gRPC (`jaeger:4317`) with bounded retry and queue size 1000.
    - Logs -> Loki over OTLP HTTP (`http://loki:3100/otlp`) with bounded retry and queue size 1000.
    - Metrics -> Prometheus scrape exporter on `0.0.0.0:9464` with `resource_to_telemetry_conversion.enabled: true`.
  - Extensions & telemetry: `health_check` on port 13133; internal telemetry metrics on `:8888`.
- **`observability/prometheus.yaml`**:
  - Pinned `prom/prometheus:v3.15.0`.
  - Scrapes `otel-collector:9464` and `otel-collector:8888` on 15s interval.
- **`observability/loki.yaml`**:
  - Pinned `grafana/loki:3.7.8`.
  - Configured TSDB single-binary filesystem storage with `v13` schema and 24h index period.
  - Enabled compactor retention with `retention_period: 168h` (7 days) and `delete_request_store: filesystem`.
  - Configured `otlp_config.default_resource_attributes_as_index_labels` strictly limited to `service.name`, `service.namespace`, `deployment.environment`, and `deployment.environment.name`, preventing high-cardinality index streams.
- **`docker-compose.yml`**:
  - Added internal `observability` network, attached to `api`, `otel-collector`, `prometheus`, `jaeger`, and `loki`. `db` remains exclusively isolated on `data`.
  - Injected standard `OTEL_*` environment variables into `api`.
  - Zero application `depends_on` coupling: `api` depends only on `db: condition: service_healthy`.
  - Published Jaeger UI on `127.0.0.1:16686:16686` and Prometheus UI on `127.0.0.1:9090:9090` (loopback only). Collector receivers, Prometheus scraper, and Loki ports remain purely internal.
  - Added persistent volumes `prometheus_data` and `loki_data`.

### 2. Verification Results
- `docker compose config`: Validated complete topology, networks, volumes, and service configs with exit code 0.

