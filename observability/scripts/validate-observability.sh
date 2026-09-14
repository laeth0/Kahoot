#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${GRAFANA_ADMIN_USER:-}" ]]; then
  echo "FAIL: GRAFANA_ADMIN_USER environment variable is required" >&2
  exit 1
fi

if [[ -z "${GRAFANA_ADMIN_PASSWORD:-}" ]]; then
  echo "FAIL: GRAFANA_ADMIN_PASSWORD environment variable is required" >&2
  exit 1
fi

GRAFANA_URL="${GRAFANA_URL:-http://20.19.48.78/grafana}"
# Normalize trailing slash
GRAFANA_URL="${GRAFANA_URL%/}"
GRAFANA_HOST=$(echo "$GRAFANA_URL" | awk -F[/:] '{print $4}')
HOST_TARGET="${GRAFANA_HOST:-localhost}"

echo "Starting post-deploy observability validation..."
echo "Target Grafana URL: ${GRAFANA_URL}/"

# 1. Assert /grafana redirects (301) to /grafana/
REDIRECT_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${GRAFANA_URL}")
if [[ "$REDIRECT_STATUS" != "301" ]]; then
  echo "FAIL: Expected HTTP 301 from ${GRAFANA_URL}, got ${REDIRECT_STATUS}" >&2
  exit 1
fi
echo "PASS: /grafana correctly redirects to /grafana/ (301)"

# 2. Assert login page loads (200)
LOGIN_PAGE_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${GRAFANA_URL}/login")
if [[ "$LOGIN_PAGE_STATUS" != "200" ]]; then
  echo "FAIL: Expected HTTP 200 from ${GRAFANA_URL}/login, got ${LOGIN_PAGE_STATUS}" >&2
  exit 1
fi
echo "PASS: Grafana login page loads (200)"

# 3. Assert unauthenticated /grafana/api/user returns 401
UNAUTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${GRAFANA_URL}/api/user")
if [[ "$UNAUTH_STATUS" != "401" ]]; then
  echo "FAIL: Expected HTTP 401 for unauthenticated API access, got ${UNAUTH_STATUS}" >&2
  exit 1
fi
echo "PASS: Unauthenticated API access denied (401)"

# 4. Assert authenticated /grafana/api/user returns 200
AUTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -u "${GRAFANA_ADMIN_USER}:${GRAFANA_ADMIN_PASSWORD}" "${GRAFANA_URL}/api/user")
if [[ "$AUTH_STATUS" != "200" ]]; then
  echo "FAIL: Expected HTTP 200 for authenticated API access, got ${AUTH_STATUS}" >&2
  exit 1
fi
echo "PASS: Authenticated Grafana API access succeeded (200)"

# 5. Assert data sources prometheus, loki, jaeger exist
for ds in prometheus loki jaeger; do
  DS_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -u "${GRAFANA_ADMIN_USER}:${GRAFANA_ADMIN_PASSWORD}" "${GRAFANA_URL}/api/datasources/uid/${ds}")
  if [[ "$DS_STATUS" != "200" ]]; then
    echo "FAIL: Datasource ${ds} not found (HTTP ${DS_STATUS})" >&2
    exit 1
  fi
done
echo "PASS: All three datasources (prometheus, loki, jaeger) exist"

# 6. Assert all six dashboard UIDs exist
DASHBOARDS=(
  "kahoot-application"
  "kahoot-realtime"
  "kahoot-infrastructure"
  "kahoot-database"
  "kahoot-logs"
  "kahoot-tracing"
)
for uid in "${DASHBOARDS[@]}"; do
  DASH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -u "${GRAFANA_ADMIN_USER}:${GRAFANA_ADMIN_PASSWORD}" "${GRAFANA_URL}/api/dashboards/uid/${uid}")
  if [[ "$DASH_STATUS" != "200" ]]; then
    echo "FAIL: Dashboard ${uid} not found (HTTP ${DASH_STATUS})" >&2
    exit 1
  fi
done
echo "PASS: All six provisioned dashboards exist"

# 7. Assert alert rules exist
ALERTS_JSON=$(curl -s -u "${GRAFANA_ADMIN_USER}:${GRAFANA_ADMIN_PASSWORD}" "${GRAFANA_URL}/api/v1/provisioning/alert-rules")
EXPECTED_ALERTS=(
  "kahoot-backend-down"
  "kahoot-frontend-down"
  "kahoot-container-backend-missing"
  "kahoot-container-frontend-missing"
  "kahoot-container-nginx-missing"
  "kahoot-high-error-rate"
  "kahoot-high-latency"
  "kahoot-answer-latency"
  "kahoot-realtime-collapse"
  "kahoot-realtime-failures"
  "kahoot-database-down"
  "kahoot-database-slow"
  "kahoot-database-errors"
  "kahoot-vm-cpu-high"
  "kahoot-vm-memory-high"
  "kahoot-vm-disk-high"
  "kahoot-network-probe-failed"
  "kahoot-collector-dropping"
)
for alert_uid in "${EXPECTED_ALERTS[@]}"; do
  if ! echo "$ALERTS_JSON" | grep -q "\"uid\":\"${alert_uid}\""; then
    echo "FAIL: Provisioned alert rule ${alert_uid} not found" >&2
    exit 1
  fi
done
echo "PASS: All 18 provisioned alert rules verified in Grafana"

# 8. Assert host connections to private ports fail
PRIVATE_PORTS=(9090 3100 16686 3000 4317 4318 9100 9115 9187)
for port in "${PRIVATE_PORTS[@]}"; do
  if curl -s --connect-timeout 1 "http://${HOST_TARGET}:${port}" >/dev/null 2>&1; then
    echo "FAIL: Host port ${port} is reachable from outside; expected closed" >&2
    exit 1
  fi
done
echo "PASS: All private observability and database ports remain unreachable"

# 9. Assert application endpoints preserve behavior
APP_ROOT_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://${HOST_TARGET}/")
if [[ "$APP_ROOT_STATUS" != "200" && "$APP_ROOT_STATUS" != "304" ]]; then
  echo "FAIL: Application root returned HTTP ${APP_ROOT_STATUS}, expected 200" >&2
  exit 1
fi

APP_HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://${HOST_TARGET}/health")
if [[ "$APP_HEALTH_STATUS" != "200" ]]; then
  echo "FAIL: Application /health returned HTTP ${APP_HEALTH_STATUS}, expected 200" >&2
  exit 1
fi

APP_LOGIN_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "http://${HOST_TARGET}/api/auth/login" -H "Content-Type: application/json" -d '{}')
if [[ "$APP_LOGIN_STATUS" != "400" && "$APP_LOGIN_STATUS" != "401" ]]; then
  echo "FAIL: /api/auth/login returned unexpected HTTP ${APP_LOGIN_STATUS}, expected 400 or 401" >&2
  exit 1
fi

APP_HUB_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "http://${HOST_TARGET}/hubs/game/negotiate?negotiateVersion=1")
if [[ "$APP_HUB_STATUS" != "200" && "$APP_HUB_STATUS" != "401" ]]; then
  echo "FAIL: SignalR negotiate returned unexpected HTTP ${APP_HUB_STATUS}, expected 200 or 401" >&2
  exit 1
fi
echo "PASS: Existing application endpoints (root, /health, /api/auth/login, /hubs/game/negotiate) behave as expected"

echo "PASS: All observability validation checks succeeded."
