# Observability Platform Setup & Deployment Guide

This guide describes how to deploy and access the production observability platform on the Azure Linux Virtual Machine (`http://20.19.48.78/`).

> [!IMPORTANT]
> **Zero Manual UI Setup:** All Grafana data sources (Prometheus, Loki, Jaeger), dashboard definitions (6 dashboards), and alert rules (18 rules) are 100% file-provisioned automatically at container startup. No manual Grafana UI configuration is required or supported.

---

## 1. Prerequisites

The deployment host must have:
- Ubuntu Linux 22.04+ (or equivalent Linux distribution)
- Docker Engine 24+ and Docker Compose v2 (`docker compose`)
- Node.js (for offline static dashboard validation)
- Bash (`set -euo pipefail`)
- Port 80 open in Azure Network Security Group (NSG)

---

## 2. Environment Configuration

1. Log into the Azure VM via SSH:
   ```bash
   ssh azureuser@20.19.48.78
   cd /home/azureuser/kahoot
   ```

2. Create the production environment file from template:
   ```bash
   cp .env.production.example .env.production
   ```

3. Generate and set strong passwords in `.env.production`:
   ```bash
   # Generate strong secrets
   openssl rand -hex 32  # Use for JWT_SECRET_KEY
   openssl rand -hex 24  # Use for POSTGRES_PASSWORD
   openssl rand -hex 24  # Use for POSTGRES_MONITOR_PASSWORD
   openssl rand -hex 24  # Use for GRAFANA_ADMIN_PASSWORD
   ```

4. Populate the required observability keys in `.env.production`:
   ```ini
   OBSERVABILITY_ENABLED=true
   OTEL_SERVICE_NAME=kahoot-backend
   OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
   GRAFANA_ADMIN_USER=admin
   GRAFANA_ADMIN_PASSWORD=<your-generated-grafana-password>
   POSTGRES_MONITOR_USER=postgres_exporter
   POSTGRES_MONITOR_PASSWORD=<your-generated-monitor-password>
   ```

---

## 3. Azure Network Security Group (NSG) Exposure

To maintain least-privilege security, verify the Azure NSG rules for the virtual machine:

- **Inbound Port 80 (HTTP):** Allowed (Public access to Nginx).
- **Inbound Port 443 (HTTPS):** Allowed (Optional, only if SSL certificate is active).
- **Inbound Port 22 (SSH):** Restricted to operator IP or bastion host.
- **Inbound Ports 3000, 3100, 4317, 4318, 8888, 8889, 9090, 9100, 9115, 9187, 16686:** **BLOCKED / DENIED.**

None of the observability backends or exporters should ever be exposed directly to the public internet.

---

## 4. Pre-Deployment Static Validation

Before applying changes, execute the deterministic configuration validation suite:

```bash
# 1. Validate Compose syntax
docker compose --env-file .env.production -f docker-compose.prod.yml config --quiet

# 2. Run deterministic static validation
bash observability/scripts/validate-config.sh

# 3. Unit-check Prometheus recording rules
docker run --rm --entrypoint promtool \
  -v "$PWD/observability/prometheus:/etc/prometheus:ro" \
  prom/prometheus:v3.14.0 test rules /etc/prometheus/rules/recording-rules.test.yml
```

Expected output: `PASS: Configuration validation passed.` and `SUCCESS`.

---

## 5. Deployment Execution

Deploy the full stack using Docker Compose:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

During startup:
1. `kahoot-db` starts and initializes the database.
2. `postgres-monitor-init` executes `observability/postgres/init-monitoring-role.sh`, idempotently creating the `postgres_exporter` role with `pg_monitor` grants and `pg_stat_statements` extension.
3. `kahoot-otel-collector`, `kahoot-prometheus`, `kahoot-loki`, and `kahoot-jaeger` initialize with persistent volumes and short retention limits.
4. `kahoot-backend` starts with OpenTelemetry SDK active and connects to the Collector.
5. `kahoot-grafana` automatically provisions data sources, dashboards, and alert rules from `/etc/grafana/provisioning/`.
6. `kahoot-nginx` begins routing `/grafana/` traffic to Grafana port 3000.

---

## 6. Post-Deployment Verification

Run the automated verification suite:

```bash
GRAFANA_ADMIN_USER=admin \
GRAFANA_ADMIN_PASSWORD='<your-generated-grafana-password>' \
GRAFANA_URL=http://20.19.48.78/grafana \
bash observability/scripts/validate-observability.sh
```

### Manual Quick-Check:
1. Check running services:
   ```bash
   docker compose --env-file .env.production -f docker-compose.prod.yml ps
   ```
2. Verify application health:
   ```bash
   curl -I http://20.19.48.78/api/health
   ```
3. Open Grafana in your web browser:
   - Navigate to: `http://20.19.48.78/grafana/`
   - Log in using `admin` and your configured `GRAFANA_ADMIN_PASSWORD`.
   - Access **Dashboards** → **Kahoot Observability** to view all six live dashboards.
