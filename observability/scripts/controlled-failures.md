# Controlled Failure and Recovery Procedures

> **Local/staging only unless an approved Azure maintenance window is active.**
> Never add a production fault endpoint, never fill the OS disk, never fork a CPU bomb, never corrupt a persistent volume, and never execute `docker compose down -v`.

This document specifies safe, controlled fault-injection procedures to verify that Grafana alert rules, Blackbox probing, and OpenTelemetry recovery workflows function as designed. Every procedure lists recovery actions first, explicit prechecks, exact execution triggers, expected firing delays, and post-recovery assertions.

---

## 1. Backend Service Pause (`kahoot-backend-down`)

### Overview
Tests Blackbox probing and container presence alerts when the ASP.NET Core API process stops responding.

- **Target Alerts:**
  - `kahoot-backend-down` (probe_success == 0 for 2m, severity: critical)
  - `kahoot-container-backend-missing` (container_last_seen absent for 2m, severity: critical)
- **Safe Mechanism:** `docker compose pause backend` / `docker compose unpause backend`

### Prechecks
1. Verify stack status: `docker compose ps` shows `backend` running healthy.
2. Verify Blackbox status: `http://localhost/api/health` returns HTTP 200 OK.
3. Verify Grafana Alerting: rule `kahoot-backend-down` is in state `Normal` (`OK`).

### Exact Trigger
```bash
docker compose pause backend
```

### Expected Alert & Firing Delay
- **0s–15s:** Blackbox exporter scrape fails for target `http://backend:8080/health`; `probe_success` drops to `0`.
- **60s:** Grafana evaluation rule `kahoot-backend-down` enters `Pending` state.
- **120s (2m):** Grafana alert `kahoot-backend-down` transitions to `Alerting` state.
- **Dashboard Impact:** Red status indicator on Application and Realtime dashboards; alert appears in `Kahoot Observability` alert lists.

### Immediate Recovery Command
```bash
docker compose unpause backend
```

### Post-Recovery Verification
1. Confirm container is unpaused: `docker compose ps backend`.
2. Verify health endpoint: `curl -f -s http://localhost/api/health` returns 200 OK.
3. Observe alert clearance: Grafana alert transitions to `Normal` within 60s of next evaluation cycle.

---

## 2. Database Service Pause (`kahoot-database-down`)

### Overview
Tests PostgreSQL availability alerting and application database connectivity failure reporting.

- **Target Alerts:**
  - `kahoot-database-down` (up{job="postgres"} == 0 for 1m, severity: critical)
  - `kahoot-database-errors` (db_errors:rate5m > 0.05 for 5m, severity: warning)
- **Safe Mechanism:** `docker compose pause db` / `docker compose unpause db`

### Prechecks
1. Verify PostgreSQL container: `docker compose ps db` shows running and healthy.
2. Verify Postgres exporter: Prometheus target `postgres` is `UP`.
3. Verify Grafana Alerting: rule `kahoot-database-down` is `Normal`.

### Exact Trigger
```bash
docker compose pause db
```

### Expected Alert & Firing Delay
- **15s:** `postgres-exporter` scrape fails; Prometheus `up{job="postgres"}` transitions from `1` to `0`.
- **60s (1m):** Grafana alert `kahoot-database-down` fires (`Alerting`).
- **Dashboard Impact:** Database dashboard shows PostgreSQL unavailable; scrape latency unavailable; alert list surfaces critical state.

### Immediate Recovery Command
```bash
docker compose unpause db
```

### Post-Recovery Verification
1. Confirm PostgreSQL unpaused: `docker compose ps db`.
2. Verify connection: `docker compose exec -T db pg_isready -U kahoot_user -d kahoot`.
3. Observe alert clearance: `kahoot-database-down` resolves to `Normal` within 60s.

---

## 3. Frontend Service Pause (`kahoot-frontend-down`)

### Overview
Tests Blackbox probing and container presence alerts when the Nginx-hosted frontend container stops responding.

- **Target Alerts:**
  - `kahoot-frontend-down` (probe_success == 0 for 2m, severity: critical)
  - `kahoot-container-frontend-missing` (container_last_seen absent for 2m, severity: critical)
- **Safe Mechanism:** `docker compose pause frontend` / `docker compose unpause frontend`

### Prechecks
1. Verify frontend container: `docker compose ps frontend` running.
2. Verify frontend root: `curl -f -s -o /dev/null http://localhost/` returns 200.
3. Verify Grafana Alerting: rule `kahoot-frontend-down` is `Normal`.

### Exact Trigger
```bash
docker compose pause frontend
```

### Expected Alert & Firing Delay
- **0s–15s:** Blackbox probe to `http://frontend:80/` fails; `probe_success` drops to `0`.
- **60s:** Alert enters `Pending`.
- **120s (2m):** Grafana alert `kahoot-frontend-down` transitions to `Alerting`.
- **Dashboard Impact:** Application dashboard shows Frontend Availability `DOWN`.

### Immediate Recovery Command
```bash
docker compose unpause frontend
```

### Post-Recovery Verification
1. Confirm container is unpaused: `docker compose ps frontend`.
2. Verify HTTP response: `curl -f -s -o /dev/null http://localhost/` returns 200.
3. Observe alert clearance: `kahoot-frontend-down` resolves to `Normal`.

---

## 4. OpenTelemetry Collector Pause (`kahoot-collector-dropping`)

### Overview
Tests telemetry pipeline resilience and Collector health monitoring when the OpenTelemetry Collector container is paused.

- **Target Alerts:**
  - `kahoot-collector-dropping` (refused/dropped telemetry increase > 0 for 5m, severity: warning)
  - `kahoot-network-probe-failed` (probe_success == 0 for 3m, severity: warning)
- **Safe Mechanism:** `docker compose pause otel-collector` / `docker compose unpause otel-collector`

### Prechecks
1. Verify Collector health endpoint: `curl -f -s http://localhost:13133/` (from inside network) returns healthy.
2. Verify Prometheus target `otel-collector` is `UP`.
3. Verify memory and queue depth on Infrastructure dashboard are nominal.

### Exact Trigger
```bash
docker compose pause otel-collector
```

### Expected Alert & Firing Delay
- **15s:** Prometheus scrape for `otel-collector` (port 8888) times out; target drops to `DOWN`.
- **60s–120s:** Backend async OTLP export buffer accumulates samples in memory without blocking request threads.
- **180s (3m):** If network probes target collector health, `kahoot-network-probe-failed` transitions toward alerting.
- **300s (5m):** Collector refusal/drop alert condition fires if export drops occur.
- **Dashboard Impact:** Infrastructure dashboard shows Collector scrape failure and flatline telemetry ingestion.

### Immediate Recovery Command
```bash
docker compose unpause otel-collector
```

### Post-Recovery Verification
1. Confirm Collector is unpaused: `docker compose ps otel-collector`.
2. Verify Collector logs: `docker compose logs --tail=20 otel-collector`.
3. Verify Prometheus scrape recovers: Prometheus target `otel-collector` returns to `UP`.
4. Confirm backend drains backlog: Application request metrics and spans resume flowing to Prometheus, Loki, and Jaeger.

---

## 5. Temporary CPU and Memory Threshold Simulation (Rule Fixture Verification)

### Overview
To prevent dangerous resource exhaustion on the single-host Azure VM (2 vCPU, 8 GB RAM), CPU and memory alerts are verified using deterministic `promtool test rules` offline fixtures rather than hazardous synthetic stress tools (`stress-ng`, fork bombs, or `/dev/zero` allocations).

- **Target Alerts:**
  - `kahoot-vm-cpu-high` (vm_cpu:ratio5m > 0.80 for 10m, severity: warning)
  - `kahoot-vm-memory-high` (vm_memory:ratio > 0.90 for 5m, severity: critical)
  - `kahoot-vm-disk-high` (vm_disk:ratio > 0.90 for 5m, severity: critical)

### Exact Simulation Command
```bash
docker run --rm --entrypoint promtool \
  -v "$PWD/observability/prometheus:/etc/prometheus:ro" \
  prom/prometheus:v3.14.0 test rules /etc/prometheus/rules/recording-rules.test.yml
```

### Validated Threshold Boundaries
| Resource | Safe Level Fixture | Threshold | Trigger Level Fixture | Evaluation Result |
|---|---|---|---|---|
| CPU Ratio | 79% (`0.79`) | 80% (`0.80`) | 81% (`0.81`) | Normal at 79%, Alerting at 81% |
| Memory Ratio | 89% (`0.89`) | 90% (`0.90`) | 91% (`0.91`) | Normal at 89%, Alerting at 91% |
| Disk Ratio | 89% (`0.89`) | 90% (`0.90`) | 91% (`0.91`) | Normal at 89%, Alerting at 91% |

### Verification Assertions
1. Both safe boundary fixtures (`0.79` CPU, `0.89` RAM/disk) evaluate below alert threshold values.
2. Both breach boundary fixtures (`0.81` CPU, `0.91` RAM/disk) evaluate above alert threshold values.
3. Zero host resources are stressed, avoiding noisy neighbor penalties or OOM kills on the production VM.
