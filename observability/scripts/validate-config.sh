#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

REQUIRED_FILES=(
  "observability/otel/collector-config.yml"
  "observability/prometheus/prometheus.yml"
  "observability/prometheus/rules/recording-rules.yml"
  "observability/loki/loki-config.yml"
  "observability/jaeger/jaeger-config.yml"
  "observability/blackbox/blackbox.yml"
  "observability/postgres/init-monitoring-role.sh"
  "observability/grafana/provisioning/datasources/datasources.yml"
  "observability/grafana/provisioning/dashboards/dashboards.yml"
  "observability/grafana/dashboards/application.json"
  "observability/grafana/dashboards/realtime.json"
  "observability/grafana/dashboards/infrastructure.json"
  "observability/grafana/dashboards/database.json"
  "observability/grafana/dashboards/logs.json"
  "observability/grafana/dashboards/tracing.json"
  "observability/grafana/provisioning/alerting/alerts.yml"
)

for file in "${REQUIRED_FILES[@]}"; do
  if [[ ! -f "${REPO_ROOT}/${file}" ]]; then
    echo "FAIL: Missing required file: ${file}" >&2
    exit 1
  fi
done

node -e '
const fs = require("fs");
const path = require("path");
const repoRoot = process.argv[1];
const prodComposePath = path.join(repoRoot, "docker-compose.prod.yml");

if (fs.existsSync(prodComposePath)) {
  const content = fs.readFileSync(prodComposePath, "utf8");
  const forbiddenServices = [
    "grafana",
    "prometheus",
    "loki",
    "jaeger",
    "otel-collector",
    "node-exporter",
    "cadvisor",
    "postgres-exporter",
    "blackbox-exporter"
  ];

  for (const service of forbiddenServices) {
    const serviceRegex = new RegExp(`^\\s*${service}:[\\s\\S]*?(?:^\\s*\\w+:|$)`, "m");
    const match = content.match(serviceRegex);
    if (match && /\\bports:\\s*\\n\\s*-\\s*["\x27]?[0-9]+/m.test(match[0])) {
      console.error(`FAIL: Public ports forbidden for service ${service} in docker-compose.prod.yml`);
      process.exit(1);
    }
  }
}
' "$REPO_ROOT"

echo "PASS: Configuration validation passed."
