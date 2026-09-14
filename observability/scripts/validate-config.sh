#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

echo "=== 1. Checking required files exist ==="
REQUIRED_FILES=(
  "backend/src/Kahoot.Api/Observability/ObservabilityOptions.cs"
  "backend/src/Kahoot.Api/Observability/ObservabilityExtensions.cs"
  "backend/src/Kahoot.Api/Common/RequestCorrelationMiddleware.cs"
  "backend/src/Kahoot.Application/Common/Observability/IKahootTelemetry.cs"
  "backend/src/Kahoot.Application/Common/Observability/KahootTelemetry.cs"
  "backend/src/Kahoot.Infrastructure/Observability/BusinessMetricsSnapshotHostedService.cs"
  "observability/otel/collector-config.yml"
  "observability/prometheus/prometheus.yml"
  "observability/prometheus/rules/recording-rules.yml"
  "observability/prometheus/rules/recording-rules.test.yml"
  "observability/loki/loki-config.yml"
  "observability/jaeger/jaeger-config.yml"
  "observability/blackbox/blackbox.yml"
  "observability/postgres/init-monitoring-role.sh"
  "observability/grafana/provisioning/datasources/datasources.yml"
  "observability/grafana/provisioning/dashboards/dashboards.yml"
  "observability/grafana/provisioning/alerting/alerts.yml"
  "observability/grafana/dashboards/application.json"
  "observability/grafana/dashboards/realtime.json"
  "observability/grafana/dashboards/infrastructure.json"
  "observability/grafana/dashboards/database.json"
  "observability/grafana/dashboards/logs.json"
  "observability/grafana/dashboards/tracing.json"
  "observability/scripts/validate-config.sh"
  "observability/scripts/validate-observability.sh"
  "observability/scripts/controlled-failures.md"
)

for file in "${REQUIRED_FILES[@]}"; do
  if [[ ! -f "${REPO_ROOT}/${file}" ]]; then
    echo "FAIL: Missing required file: ${file}" >&2
    exit 1
  fi
done
echo "PASS: All required files exist."

echo "=== 2. Parsing dashboards and validating invariants ==="
node -e '
const fs = require("fs");
const path = require("path");
const repoRoot = process.argv[1];
const dashboardsDir = path.join(repoRoot, "observability/grafana/dashboards");
const files = fs.readdirSync(dashboardsDir).filter(f => f.endsWith(".json"));

if (files.length !== 6) {
  console.error(`FAIL: Expected 6 dashboard files, found ${files.length}`);
  process.exit(1);
}

const seenUids = new Set();

for (const file of files) {
  const filePath = path.join(dashboardsDir, file);
  const raw = fs.readFileSync(filePath, "utf8");

  if (/\$\{DS_[^}]+\}/.test(raw)) {
    console.error(`FAIL: Unresolved datasource placeholder in ${file}`);
    process.exit(1);
  }

  let d;
  try {
    d = JSON.parse(raw);
  } catch (err) {
    console.error(`FAIL: JSON parse error in ${file}:`, err.message);
    process.exit(1);
  }

  if (!d.uid) {
    console.error(`FAIL: Missing uid in ${file}`);
    process.exit(1);
  }
  if (seenUids.has(d.uid)) {
    console.error(`FAIL: Duplicate dashboard uid: ${d.uid} in ${file}`);
    process.exit(1);
  }
  seenUids.add(d.uid);

  if (d.editable !== false) {
    console.error(`FAIL: Dashboard ${file} must have editable: false`);
    process.exit(1);
  }

  const hasAlertList = Array.isArray(d.panels) && d.panels.some(p => p.type === "alertlist");
  if (!hasAlertList) {
    console.error(`FAIL: Dashboard ${file} is missing an alertlist panel`);
    process.exit(1);
  }
}
console.log("PASS: All 6 dashboards valid (unique UIDs, immutable, alertlist present, no DS placeholders).");
' "$REPO_ROOT"

echo "=== 3. Validating Compose configs and port isolation ==="
VAL_ENV="GRAFANA_ADMIN_PASSWORD=val_grafana_pass POSTGRES_MONITOR_PASSWORD=val_mon_pass JWT_SECRET_KEY=12345678901234567890123456789012 SEEDING_HOST_PASSWORD=val_seed_pass POSTGRES_PASSWORD=val_pg_pass"

# Check dev compose
env $VAL_ENV docker compose -f "${REPO_ROOT}/docker-compose.yml" config --quiet

# Check prod compose and verify only Nginx publishes ports
node -e '
const { execSync } = require("child_process");
const repoRoot = process.argv[1];
const env = {
  ...process.env,
  GRAFANA_ADMIN_PASSWORD: "val_grafana_pass",
  POSTGRES_MONITOR_PASSWORD: "val_mon_pass",
  JWT_SECRET_KEY: "12345678901234567890123456789012",
  SEEDING_HOST_PASSWORD: "val_seed_pass",
  POSTGRES_PASSWORD: "val_pg_pass"
};

const output = execSync(`docker compose -f "${repoRoot}/docker-compose.prod.yml" config --format json`, { env }).toString();
const config = JSON.parse(output);

for (const [name, service] of Object.entries(config.services)) {
  if (name !== "nginx" && service.ports && service.ports.length > 0) {
    console.error(`FAIL: Service "${name}" in docker-compose.prod.yml exposes public ports:`, service.ports);
    process.exit(1);
  }
  if (name === "nginx") {
    const published = (service.ports || []).map(p => typeof p === "object" ? String(p.published) : String(p));
    for (const p of published) {
      if (!p.includes("80") && !p.includes("443")) {
        console.error(`FAIL: Nginx in docker-compose.prod.yml exposes unexpected port: ${p}`);
        process.exit(1);
      }
    }
  }
}
console.log("PASS: Compose rendered; only Nginx 80/443 exposed in production.");
' "$REPO_ROOT"

echo "=== 4. Pinned tool validations (Prometheus, Loki, Collector, Nginx, Jaeger) ==="
if command -v docker.exe >/dev/null 2>&1 && command -v wslpath >/dev/null 2>&1; then
  DOCKER_BIN="docker.exe"
  HOST_DIR="$(wslpath -m "${REPO_ROOT}")"
elif command -v cygpath >/dev/null 2>&1; then
  DOCKER_BIN="docker"
  HOST_DIR="$(cygpath -m "${REPO_ROOT}")"
else
  DOCKER_BIN="docker"
  HOST_DIR="${REPO_ROOT}"
fi

# promtool check config
$DOCKER_BIN run --rm --entrypoint promtool \
  -v "${HOST_DIR}/observability/prometheus:/etc/prometheus:ro" \
  prom/prometheus:v3.14.0 check config /etc/prometheus/prometheus.yml >/dev/null

# promtool check rules
$DOCKER_BIN run --rm --entrypoint promtool \
  -v "${HOST_DIR}/observability/prometheus:/etc/prometheus:ro" \
  prom/prometheus:v3.14.0 check rules /etc/prometheus/rules/recording-rules.yml >/dev/null

# Loki verify config
$DOCKER_BIN run --rm --entrypoint /usr/bin/loki \
  -v "${HOST_DIR}/observability/loki/loki-config.yml:/etc/loki/loki-config.yml:ro" \
  grafana/loki:3.7.7 -verify-config -config.file=/etc/loki/loki-config.yml >/dev/null 2>&1

# Collector validate
$DOCKER_BIN run --rm \
  -v "${HOST_DIR}/observability/otel/collector-config.yml:/etc/otelcol-contrib/config.yaml:ro" \
  otel/opentelemetry-collector-contrib:0.160.0 validate --config=/etc/otelcol-contrib/config.yaml >/dev/null 2>&1

# Nginx check syntax
$DOCKER_BIN run --rm \
  --add-host backend:127.0.0.1 --add-host frontend:127.0.0.1 --add-host grafana:127.0.0.1 \
  -v "${HOST_DIR}/nginx/nginx.conf:/etc/nginx/nginx.conf:ro" \
  -v "${HOST_DIR}/nginx/default.conf:/etc/nginx/conf.d/default.conf:ro" \
  nginx:alpine nginx -t -c /etc/nginx/nginx.conf >/dev/null 2>&1

# Jaeger validate
$DOCKER_BIN run --rm \
  -v "${HOST_DIR}/observability/jaeger/jaeger-config.yml:/etc/jaeger/config.yml:ro" \
  jaegertracing/jaeger:2.20.0 validate --config=/etc/jaeger/config.yml >/dev/null 2>&1

echo "PASS: Pinned images verified Prometheus, Loki, Collector, Nginx, and Jaeger configs."

echo "=== 5. Secret audit and forbidden integrations check ==="
node -e '
const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");
const repoRoot = process.argv[1];

const trackedFiles = execSync("git ls-files", { cwd: repoRoot, encoding: "utf8" })
  .split("\n")
  .map(f => f.trim())
  .filter(f => f.length > 0)
  .map(f => path.join(repoRoot, f));

// Secret patterns check
const secretRegex = new RegExp("(?:GRAFANA_ADMIN_PASSWORD|POSTGRES_MONITOR_PASSWORD|HOST_PASSWORD)=([^\\s,`\"\\x27\\)\\r\\n]+)|(?:SigningKey|Password)\"\\s*:\\s*\"([^\"]+)\"");
const ignoredFiles = [
  "validate-config.sh",
  "controlled-failures.md",
  "architecture.md",
  "setup.md",
  "troubleshooting.md",
  "README.md"
];

for (const file of trackedFiles) {
  if (!fs.existsSync(file)) continue;
  const base = path.basename(file);
  if (base.startsWith(".env") || file.includes("/docs/") || file.includes("\\docs\\") || file.includes("/.wolf/") || file.includes("\\.wolf\\") || ignoredFiles.some(f => file.endsWith(f))) continue;
  const content = fs.readFileSync(file, "utf8");
  const lines = content.split("\n");
  for (let i = 0; i < lines.length; i++) {
    const match = lines[i].match(secretRegex);
    if (match) {
      const val = (match[1] || match[2] || "").trim();
      if (val && val !== "\"\"" && val !== "..." && !val.startsWith("$") && !val.startsWith("<") && !val.startsWith("val_")) {
        console.error(`FAIL: Committed secret detected in ${file}:${i + 1}: ${lines[i].trim()}`);
        process.exit(1);
      }
    }
  }
}

// Forbidden integration settings
const forbiddenPatterns = [
  "auth.generic_oauth",
  "auth.google",
  "auth.github",
  "auth.azuread",
  "smtp.host",
  "webhook_configs",
  "slack_configs",
  "msteams_configs"
];

for (const file of trackedFiles) {
  if (!fs.existsSync(file)) continue;
  if (file.includes("/docs/") || file.includes("\\docs\\") || file.includes("/observability/scripts/") || file.includes("\\observability\\scripts\\")) continue;
  const content = fs.readFileSync(file, "utf8");
  for (const pattern of forbiddenPatterns) {
    if (content.includes(pattern)) {
      console.error(`FAIL: Forbidden integration setting found in ${file}: ${pattern}`);
      process.exit(1);
    }
  }
}

console.log("PASS: Secret hygiene clean; zero forbidden OAuth/SMTP/webhook settings found.");
' "$REPO_ROOT"

echo "=== 6. Checking Grafana root URL, anonymous auth, and pinned images ==="
node -e '
const fs = require("fs");
const path = require("path");
const repoRoot = process.argv[1];

for (const composeFile of ["docker-compose.yml", "docker-compose.prod.yml"]) {
  const content = fs.readFileSync(path.join(repoRoot, composeFile), "utf8");

  if (!/GF_SERVER_ROOT_URL:\s*["\x27]?http:\/\/[^/]+\/grafana\/["\x27]?/.test(content)) {
    console.error(`FAIL: ${composeFile} does not configure GF_SERVER_ROOT_URL with trailing /grafana/`);
    process.exit(1);
  }

  if (!/GF_AUTH_ANONYMOUS_ENABLED:\s*["\x27]?false["\x27]?/.test(content)) {
    console.error(`FAIL: ${composeFile} must set GF_AUTH_ANONYMOUS_ENABLED to false`);
    process.exit(1);
  }

  // Verify newly introduced observability images are pinned (no :latest)
  const images = content.match(/image:\s*["\x27]?([^"\x27\s]+)/g) || [];
  for (const imgStr of images) {
    const img = imgStr.replace(/image:\s*["\x27]?/, "").trim();
    if (img.includes("grafana") || img.includes("prometheus") || img.includes("loki") || img.includes("jaeger") || img.includes("otel") || img.includes("exporter") || img.includes("cadvisor")) {
      if (img.endsWith(":latest") || !img.includes(":")) {
        console.error(`FAIL: Unpinned observability image in ${composeFile}: ${img}`);
        process.exit(1);
      }
    }
  }
}
console.log("PASS: Grafana root URL /grafana/, anonymous auth false, and pinned images verified.");
' "$REPO_ROOT"

echo "=== 7. Checking retention invariants (Prometheus 7d/4GB, Loki 168h, Jaeger 72h) ==="
node -e '
const fs = require("fs");
const path = require("path");
const repoRoot = process.argv[1];

const prodCompose = fs.readFileSync(path.join(repoRoot, "docker-compose.prod.yml"), "utf8");
if (!prodCompose.includes("--storage.tsdb.retention.time=7d") || (!prodCompose.includes("--storage.tsdb.retention.size=4GB") && !prodCompose.includes("--storage.tsdb.retention.max-bytes=4GB"))) {
  console.error("FAIL: Prometheus in docker-compose.prod.yml missing 7d retention or 4GB cap");
  process.exit(1);
}

const lokiConfig = fs.readFileSync(path.join(repoRoot, "observability/loki/loki-config.yml"), "utf8");
if (!/retention_period:\s*168h/.test(lokiConfig)) {
  console.error("FAIL: Loki config missing retention_period: 168h");
  process.exit(1);
}

const jaegerConfig = fs.readFileSync(path.join(repoRoot, "observability/jaeger/jaeger-config.yml"), "utf8");
if (!/ttl:[\s\S]*?spans:\s*72h/.test(jaegerConfig)) {
  console.error("FAIL: Jaeger config missing ttl: spans: 72h");
  process.exit(1);
}
console.log("PASS: Retention invariants verified (Prometheus 7d/4GB, Loki 168h, Jaeger 72h).");
' "$REPO_ROOT"

echo "=== 8. Checking metric label dimension boundaries ==="
node -e '
const fs = require("fs");
const path = require("path");
const repoRoot = process.argv[1];

const forbiddenLabels = [
  "game_id", "game\\.id", "gameId",
  "participant_id", "participant\\.id", "participantId",
  "question_id", "question\\.id", "questionId",
  "connection_id", "connection\\.id", "connectionId",
  "request_id", "request\\.id", "requestId",
  "trace_id", "trace\\.id", "traceId",
  "nickname",
  "pin", "PIN",
  "token"
];

const filesToCheck = [
  "observability/prometheus/rules/recording-rules.yml",
  "observability/otel/collector-config.yml"
];

for (const relPath of filesToCheck) {
  const content = fs.readFileSync(path.join(repoRoot, relPath), "utf8");
  for (const label of forbiddenLabels) {
    const regex = new RegExp(`\\b(by|without)\\s*\\([^)]*\\b${label}\\b`, "i");
    if (regex.test(content)) {
      console.error(`FAIL: Forbidden high-cardinality metric label "${label}" in ${relPath}`);
      process.exit(1);
    }
  }
}
console.log("PASS: Zero high-cardinality ID labels in metric dimensions.");
' "$REPO_ROOT"

echo "PASS: Configuration validation passed."
