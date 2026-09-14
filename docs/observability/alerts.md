# Grafana Alert Rules & Operational Runbooks

> [!IMPORTANT]
> **Dashboard-Only Visibility in This Phase:** No email, Microsoft Teams, Slack, SMTP, webhooks, or external notification channels are configured. Alerts are evaluated inside Grafana and rendered visually in dashboard alert lists and the Grafana Alerting UI. Alerts cannot notify an operator who is not logged into Grafana and cannot detect if Grafana itself is unreachable.

This document catalogs all 18 provisioned alert rules, their PromQL conditions, duration thresholds, severity levels, diagnostic interpretations, false-positive verification steps, and operational recovery actions.

---

## Alert Rules Summary Table

| UID | Alert Name | Evaluation Group | Condition | For | Severity | No-Data State |
|---|---|---|---|---|---|---|
| `kahoot-backend-down` | Backend unavailable | Availability | probe_success == 0 | 2m | critical | Alerting |
| `kahoot-frontend-down` | Frontend unavailable | Availability | probe_success == 0 | 2m | critical | Alerting |
| `kahoot-container-backend-missing` | Backend container missing | Availability | absent(container_last_seen) | 2m | critical | Alerting |
| `kahoot-container-frontend-missing` | Frontend container missing | Availability | absent(container_last_seen) | 2m | critical | Alerting |
| `kahoot-container-nginx-missing` | Nginx container missing | Availability | absent(container_last_seen) | 2m | critical | Alerting |
| `kahoot-high-error-rate` | HTTP 5xx above 2% | Application | requests > 0.1 and 5xx ratio > 0.02 | 5m | warning | OK |
| `kahoot-high-latency` | API p95 above 1 second | Application | http_latency:p95_5m > 1.0 | 5m | warning | OK |
| `kahoot-answer-latency` | Answer p95 above 500 ms | Realtime | answer_latency:p95_5m > 0.5 | 5m | warning | OK |
| `kahoot-realtime-collapse` | Players present without SignalR | Realtime | players > 0 and connections == 0 | 2m | critical | OK |
| `kahoot-realtime-failures` | Realtime event failures | Realtime | failed events rate > 0.05 | 5m | warning | OK |
| `kahoot-database-down` | PostgreSQL unavailable | Database | up{job="postgres"} == 0 | 1m | critical | Alerting |
| `kahoot-database-slow` | Database p95 above 1 second | Database | db_latency:p95_5m > 1.0 | 5m | warning | OK |
| `kahoot-database-errors` | Database connection/op failures | Database | db_errors:rate5m > 0.05 | 5m | warning | OK |
| `kahoot-vm-cpu-high` | VM CPU above 80% | Infrastructure | vm_cpu:ratio5m > 0.80 | 10m | warning | OK |
| `kahoot-vm-memory-high` | VM memory above 90% | Infrastructure | vm_memory:ratio > 0.90 | 5m | critical | Alerting |
| `kahoot-vm-disk-high` | Root disk above 90% | Infrastructure | vm_disk:ratio > 0.90 | 5m | critical | Alerting |
| `kahoot-network-probe-failed` | Internal route probe failure | Availability | probe_success == 0 | 3m | warning | Alerting |
| `kahoot-collector-dropping` | Collector refusing telemetry | Infrastructure | refused/dropped increase > 0 | 5m | warning | OK |

---

## Detailed Runbooks by Alert

### 1. `kahoot-backend-down` (Backend unavailable)
- **Condition:** Blackbox probe to `http://backend:8080/health` fails (`probe_success == 0`) for 2 minutes.
- **Interpretation:** The ASP.NET Core application is crashing, hung, or stopped.
- **False-Positive Checks:** Check if Nginx or Blackbox Exporter container was restarted or experiencing DNS resolution delay.
- **Runbook:**
  1. Inspect container status: `docker compose ps backend`.
  2. Inspect recent container logs: `docker compose logs --tail=100 backend`.
  3. Verify database connectivity: `docker compose exec -T db pg_isready -U kahoot_user -d kahoot`.
- **Recovery:**
  - Restart backend: `docker compose restart backend`.

### 2. `kahoot-frontend-down` (Frontend unavailable)
- **Condition:** Blackbox probe to `http://frontend:80/` fails for 2 minutes.
- **Interpretation:** Nginx static file container for the React SPA is down or unhealthy.
- **Runbook:**
  1. Check container: `docker compose ps frontend`.
  2. View logs: `docker compose logs --tail=50 frontend`.
- **Recovery:**
  - Restart frontend: `docker compose restart frontend`.

### 3. Container Missing Alerts (`kahoot-container-*-missing`)
- **Condition:** cAdvisor `container_last_seen` metric is completely absent for 2 minutes.
- **Interpretation:** The container was removed, stopped, or OOM-killed by Docker daemon.
- **Runbook:**
  1. Check stopped containers: `docker ps -a --filter "status=exited"`.
  2. Inspect exit code: `docker inspect --format='{{.State.ExitCode}} {{.State.OOMKilled}}' <container-name>`.
- **Recovery:**
  - Recreate service: `docker compose up -d <service-name>`.

### 4. `kahoot-high-error-rate` (HTTP 5xx above 2%)
- **Condition:** `kahoot:http_requests:rate5m > 0.1` and `kahoot:http_5xx:ratio5m > 0.02` for 5 minutes.
- **Interpretation:** More than 2% of incoming HTTP requests are returning 5xx server errors under active traffic.
- **Runbook:**
  1. Open **Logs & Exceptions** dashboard.
  2. Filter logs for level `Error` or `Critical`.
  3. Check **Failed Requests by Route** panel on Application dashboard to identify the failing endpoint.
- **Recovery:**
  - If database connection pool is exhausted, restart backend or verify PostgreSQL performance.

### 5. `kahoot-high-latency` (API p95 above 1 second)
- **Condition:** 95th percentile HTTP request duration exceeds 1.0 second for 5 minutes.
- **Interpretation:** Application endpoints are responding slowly due to database contention, CPU throttling, or external network latency.
- **Runbook:**
  1. Open **Application Health** dashboard and click on latency exemplar dots to view traces in Jaeger.
  2. Open **Database & Queries** dashboard to see if SQL query latency is elevated.
- **Recovery:**
  - Identify and optimize long-running queries via `pg_stat_statements`.

### 6. `kahoot-answer-latency` (Answer p95 above 500 ms)
- **Condition:** 95th percentile answer processing duration exceeds 500 ms for 5 minutes.
- **Interpretation:** The live game answer submission pipeline is lagging behind incoming student answers.
- **Runbook:**
  1. Check database locks and active transactions on **Database & Queries** dashboard.
  2. Check CPU utilization on **Infrastructure** dashboard.
- **Recovery:**
  - Verify that database concurrency limits are not saturated.

### 7. `kahoot-realtime-collapse` (Players present without SignalR connections)
- **Condition:** `kahoot_players_connected > 0` but `kahoot:signalr_connections == 0` for 2 minutes.
- **Interpretation:** Connected players are in game sessions in the database, but zero active WebSocket connections exist on the SignalR hub.
- **Runbook:**
  1. Check Nginx WebSocket proxy configuration: verify `proxy_set_header Upgrade` and `Connection $connection_upgrade`.
  2. Check SignalR disconnect spikes on **Realtime** dashboard.
- **Recovery:**
  - Reload Nginx: `docker compose exec nginx nginx -s reload`.

### 8. `kahoot-realtime-failures` (Realtime event failures)
- **Condition:** Failure rate of SignalR broadcast events exceeds 0.05 events/sec for 5 minutes.
- **Interpretation:** SignalR hub is unable to push game state changes to connected players.
- **Runbook:**
  1. Check backend logs for `GameNotifier` broadcast exceptions.
- **Recovery:**
  - Verify client connection health and restart backend if hub transport is stalled.

### 9. `kahoot-database-down` (PostgreSQL unavailable)
- **Condition:** `up{job="postgres"} == 0` for 1 minute.
- **Interpretation:** PostgreSQL container is stopped, crashing, or out of disk space.
- **Runbook:**
  1. Check PostgreSQL container: `docker compose ps db`.
  2. Check logs: `docker compose logs --tail=100 db`.
  3. Verify disk space: `df -h /`.
- **Recovery:**
  - Restart database: `docker compose restart db`.

### 10. `kahoot-database-slow` (Database p95 above 1 second)
- **Condition:** 95th percentile database operation latency exceeds 1.0 second for 5 minutes.
- **Interpretation:** Heavy queries, lock contention, or disk I/O wait is slowing PostgreSQL transactions.
- **Runbook:**
  1. Open **Database & Queries** dashboard.
  2. Inspect **Top 20 Slow Queries** from `pg_stat_statements`.
- **Recovery:**
  - Terminate hung queries if necessary via `SELECT pg_terminate_backend(pid)`.

### 11. `kahoot-database-errors` (Database connection or operation failures)
- **Condition:** Failed database client spans rate exceeds 0.05 errors/sec for 5 minutes.
- **Interpretation:** EF Core or Npgsql queries are throwing PostgreSQL exceptions (e.g. unique constraint violations, connection timeouts).
- **Runbook:**
  1. Open **Logs & Exceptions** dashboard.
  2. Filter logs for `NpgsqlException` or `PostgresException`.

### 12. `kahoot-vm-cpu-high` (VM CPU above 80%)
- **Condition:** Host CPU usage exceeds 80% for 10 minutes continuously.
- **Interpretation:** Sustained heavy load or runaway process is consuming host compute resources.
- **Runbook:**
  1. Open **Infrastructure** dashboard.
  2. Check **Container CPU by Service** panel to identify the process using CPU.
  3. Run `docker stats --no-stream` on the host.

### 13. `kahoot-vm-memory-high` (VM memory above 90%)
- **Condition:** Host memory usage exceeds 90% (< 10% available) for 5 minutes.
- **Interpretation:** Host is nearing exhaustion and at risk of triggering the Linux kernel OOM Killer.
- **Runbook:**
  1. Run `free -m` and `docker stats --no-stream`.
  2. Identify memory growth in Prometheus, Loki, or backend.
- **Recovery:**
  - If TSDB memory is bloated, Prometheus will prune automatically. If application leaks memory, restart backend.

### 14. `kahoot-vm-disk-high` (Root disk above 90%)
- **Condition:** Root filesystem disk usage exceeds 90% for 5 minutes.
- **Interpretation:** OS disk is running out of space, threatening database writes and Docker operation.
- **Runbook:**
  1. Check disk utilization: `df -h /`.
  2. Clean dangling Docker images: `docker image prune -f`.
  3. Clean Docker build cache: `docker builder prune -f`.
  4. Never delete named volumes (`postgres_data`, `uploads_data`, `prometheus_data`, `loki_data`, `jaeger_data`).

### 15. `kahoot-network-probe-failed` (Internal route probe failure)
- **Condition:** Internal Blackbox probe to one of the critical service endpoints fails for 3 minutes.
- **Runbook:**
  1. Check Blackbox Exporter logs: `docker compose logs --tail=50 blackbox-exporter`.
  2. Verify internal network DNS resolution.

### 16. `kahoot-collector-dropping` (Collector refusing telemetry)
- **Condition:** Increase in OpenTelemetry Collector refused or dropped spans/metrics/logs > 0 for 5 minutes.
- **Interpretation:** Collector memory limiter is actively shedding load or backends (Prometheus, Loki, Jaeger) are applying backpressure.
- **Runbook:**
  1. Check Collector logs: `docker compose logs --tail=100 otel-collector`.
  2. Check queue size and memory in Collector health panels on Infrastructure dashboard.
- **Recovery:**
  - Ensure Loki and Jaeger are running healthy and accepting connections.
