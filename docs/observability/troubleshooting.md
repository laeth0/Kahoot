# Observability Troubleshooting Guide

This guide covers diagnostic procedures and remediation steps for common operational issues encountered in the production observability platform.

---

## 1. Missing Telemetry Signals

### A. Missing Application Metrics
1. **Check Backend Configuration:** Verify that `Observability:Enabled` is set to `true` and `Observability:OtlpEndpoint` points to `http://otel-collector:4317`.
2. **Check Collector Receiver:** Verify the OpenTelemetry Collector is listening on port 4317:
   ```bash
   docker compose logs --tail=50 otel-collector
   ```
3. **Check Prometheus Scrape:** Open Prometheus target page at `http://prometheus:9090/targets` (via container or Grafana Explore) and confirm `kahoot-application` and `otel-collector` are in state `UP`.

### B. Missing Structured Logs in Loki
1. **Check Log Format:** The backend must emit structured JSON logs. Ensure console log formatting is active.
2. **Check Loki Ingestion Horizon:** Loki rejects samples older than `reject_old_samples_max_age: 168h`. Check host time synchronization (`timedatectl`).
3. **Verify Stream Labels:** Loki indexes only low-cardinality labels (`service_name`, `deployment_environment_name`, `severity_text`). Structured attributes (`request.id`, `trace_id`) are stored in structured metadata or parsed via LogQL `| json`.

### C. Missing Traces in Jaeger
1. **Understand Tail-Sampling Policy:**
   - **Errors and HTTP 5xx:** 100% retained.
   - **Latency >= 1000ms:** 100% retained.
   - **Normal Successful Traces:** Sampled at 10% (9 out of 10 normal traces are intentionally discarded).
   - If testing trace flow, trigger an endpoint error (e.g. `POST /api/auth/login` with invalid payload) or check a batch of 20 requests.
2. **Check Jaeger Badger Storage:** Ensure `/badger` volume is writable and permissions allow Jaeger user to create keys and data files.

---

## 2. Data Source & Provisioning Failures

### Datasource Reports Error in Grafana
1. Verify internal network DNS resolution:
   ```bash
   docker compose exec -T grafana ping -c 1 prometheus
   docker compose exec -T grafana ping -c 1 loki
   docker compose exec -T grafana ping -c 1 jaeger
   ```
2. Verify service health endpoints:
   - Prometheus: `curl -f http://prometheus:9090/-/healthy`
   - Loki: `curl -f http://loki:3100/ready`
   - Jaeger: `curl -f http://jaeger:16686/`

### Dashboards or Alerts Not Appearing
- Grafana provisions files from `/etc/grafana/provisioning/` on container startup.
- Review Grafana startup logs for YAML parsing errors:
  ```bash
  docker compose logs kahoot-grafana | grep -E "provisioning|error|failed"
  ```
- File permissions must allow reading by Grafana UID 472 (`chmod -R a+r observability/grafana/`).

---

## 3. Web & Proxy Issues

### Subpath Redirect Loops (`/grafana` vs `/grafana/`)
- Nginx issues a 301 redirect from `/grafana` to `/grafana/`.
- If an infinite redirect loop occurs, verify:
  1. `GF_SERVER_ROOT_URL` in `docker-compose.prod.yml` ends with a trailing slash: `http://20.19.48.78/grafana/`.
  2. Nginx proxy location uses rewrite: `rewrite ^/grafana/(.*) /$1 break;`.
  3. `proxy_set_header Host $host;` and `proxy_set_header X-Forwarded-Prefix /grafana;` are present.

### Grafana Live WebSocket Disconnections
- Grafana Live uses WebSockets at `/grafana/api/live/ws`.
- If the browser console shows WebSocket connection drops, ensure Nginx forwards connection upgrades:
  ```nginx
  proxy_set_header Upgrade $http_upgrade;
  proxy_set_header Connection $connection_upgrade;
  ```

---

## 4. Exporter & System Monitoring Issues

### cAdvisor on cgroup v2
- On modern Linux distributions (Ubuntu 22.04+), cgroups v2 is default.
- cAdvisor requires mounting `/sys/fs/cgroup:/sys/fs/cgroup:ro` and `/var/run/docker.sock:/var/run/docker.sock:ro`.
- If container CPU metrics are missing, check cAdvisor logs:
  ```bash
  docker compose logs kahoot-cadvisor
  ```

### `pg_stat_statements` Not Recording Queries
1. Check that the extension is created in the database:
   ```bash
   docker compose exec -T db psql -U kahoot_user -d kahoot -c "SELECT * FROM pg_extension WHERE extname = 'pg_stat_statements';"
   ```
2. Check `shared_preload_libraries`: PostgreSQL must have `shared_preload_libraries = 'pg_stat_statements'` enabled in configuration.
3. If `postgres-exporter` returns permission denied, re-run the role bootstrap:
   ```bash
   docker compose run --rm postgres-monitor-init
   ```

---

## 5. Resource Pressure & Collector Backpressure

### Collector Refusal (`kahoot-collector-dropping` firing)
1. **Inspect Memory Limiter:** Collector limits memory to 256 MiB with a 64 MiB spike buffer. If ingestion spikes exceed this, spans/logs are dropped to protect host memory.
2. **Check Downstream Backends:** If Loki or Jaeger is slow to write to disk, the Collector queue fills up and rejects new telemetry.
3. Check queue latency in Prometheus: `rate(otelcol_exporter_enqueue_failed_spans[5m])`.

### Disk Pressure & Cleanup Without Volume Deletion
> [!CAUTION]
> **Never run `docker compose down -v`.** That will permanently erase the application database (`postgres_data`) and uploaded media (`uploads_data`).

If host root disk usage reaches > 80%:
1. Inspect space usage: `df -h /` and `docker system df`.
2. Prune dangling container images:
   ```bash
   docker image prune -a --filter "until=168h" -f
   ```
3. Prune Docker build cache:
   ```bash
   docker builder prune -a -f
   ```
4. Check container log files: Docker containers are configured with `max-size: "10m"` and `max-file: "3"`, keeping total container logs below 30 MB.
