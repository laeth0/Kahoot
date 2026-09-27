# Phase 4 - Provision Grafana and correlation

## Objective

Make the running three-signal stack usable without manual Grafana setup, and provide a direct path from an OTLP log's trace ID to its Jaeger trace. Evaluate metric exemplars without introducing unstable or duplicate metric infrastructure.

## Files to create or modify

- Create `observability/grafana/provisioning/datasources/datasources.yaml` for three pinned-UID data sources and log-to-trace linking.
- Modify `docker-compose.yml` for pinned Grafana service, internal observability network, loopback UI port `3000`, provisioning mount, and `grafana_data` volume if needed for local preferences.
- Modify `.env.example`, `.env.development`, and `.env.production` together for `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` (and `GRAFANA_PORT` only if a configurable host port is justified). Leave password empty in committed templates and require a value in the local `.env`/deployment environment. Do not change existing secret values.
- Modify `observability/prometheus.yaml` or Collector config only if verified exemplar support requires a supported setting. Optionally create one small operational dashboard under `observability/grafana/provisioning/dashboards/` only if actual emitted metric names have been checked.

## Required packages/dependencies

No NuGet package. Pin the Phase 0 stable `grafana/grafana` image. Use built-in Prometheus, Loki, and Jaeger data sources; install no plugin unless the pinned Grafana version lacks a required built-in capability.

## Step-by-step implementation tasks

- [ ] Add Grafana service to the existing observability network. Set admin user/password from environment with no production fallback password; expose `127.0.0.1:3000:3000` and add optional `grafana_data` volume for local user state. Ensure absent credentials fail Grafana configuration clearly without affecting the API.
- [ ] Provision `Prometheus` (`http://prometheus:9090`), `Loki` (`http://loki:3100`), and `Jaeger` (`http://jaeger:16686`) data sources with stable UIDs `prometheus`, `loki`, `jaeger`. Check the pinned Grafana/Jaeger URL requirement rather than assuming OTLP ingestion port is a query URL.
- [ ] Configure a Loki derived field/internal link using the OTLP log `trace_id` structured metadata (or the supported LogRecord trace field) and target Jaeger UID. Verify the real field name in Grafana Explore, including Loki's dot-to-underscore normalization; do not add custom TraceId strings to application log calls.
- [ ] Evaluate trace-based metric exemplars end to end: verify the selected .NET SDK attaches exemplars, Collector Prometheus exporter emits them in a scrape format Prometheus accepts, Prometheus stores them, and Grafana can link exemplar trace IDs to Jaeger. If all pass with stable pinned capabilities, enable the minimal required settings (possibly OpenMetrics/exemplar storage) and provision the Prometheus exemplar link. If any link is unsupported or disproportionately complex, document that decision in the operator guide; keep normal metrics working. Never turn exemplar IDs into regular metric labels.
- [ ] If a dashboard materially helps initial operations, provision one small dashboard using observed series only: HTTP request rate/errors/duration, runtime/GC, Npgsql pool, and Collector accepted/dropped/export-failed metrics. Do not create speculative game/business dashboards or custom metrics for it.
- [ ] Verify Grafana's data sources survive `docker compose down`/`up` without manual re-entry and that container health, if configured, uses a probe available in the pinned image. Do not delete volumes.

## Configuration/environment variables

`GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` are deployment settings, passed to Grafana's supported `GF_SECURITY_ADMIN_*` environment variables. Local docs tell developers to set their own password in ignored `.env`; production obtains it from deployment secrets. Existing OTel variables remain unchanged. Internal data-source URLs are committed, non-secret Compose network addresses.

## Instrumentation covered

Presentation and cross-signal navigation for existing logs, traces, and metrics. No application instrumentation is added. Exemplars are conditional on a verified stable end-to-end path.

## Testing and validation

- `docker compose config`, `docker compose up -d grafana`, `docker compose ps`, and Grafana provisioning logs.
- Query each provisioned data source in Explore after generating a normal API request. Select a Loki record with a trace ID, follow the derived link, and confirm the same trace in Jaeger.
- If exemplars are enabled, verify an actual Prometheus exemplar through its API/UI and click through from a Grafana metric visualization to a matching Jaeger trace. If absent, record the exact missing capability rather than claiming success.
- Restart Grafana and verify provisioning remains automatic; inspect generated dashboards for only real metrics and bounded label use.

## Acceptance criteria

- Grafana starts with all three functioning data sources, no manual provisioning, and no committed admin password.
- Log-to-trace navigation works using existing OTel trace context where supported by the pinned versions; otherwise the unsupported link and evidence are documented without adding custom logging hacks.
- Exemplar support is either proven end to end or explicitly deferred; trace IDs never become metric labels.
- Grafana outage has no effect on API availability.

## Dependencies on previous phases

Phases 0–3 complete, with real logs/traces/metrics available to query.

## References for execution

- [Grafana Jaeger data source](https://grafana.com/docs/grafana/latest/datasources/jaeger/configure/), [Prometheus exemplar configuration](https://grafana.com/docs/grafana/latest/datasources/prometheus/configure/), and [Loki structured metadata](https://grafana.com/docs/loki/latest/get-started/labels/structured-metadata/).
