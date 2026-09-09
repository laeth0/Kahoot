#!/usr/bin/env bash
# run.sh - one command to load-test the Kahoot backend.
#
# Usage:
#   ./load-tests/run.sh                     bring up the compose backend + full acceptance suite
#   ./load-tests/run.sh answer-burst        one scenario (any of: connections join-game
#                                           question-broadcast answer-burst duplicate-answer
#                                           reconnection multiple-games ramp
#                                           reconnection-storm endurance)
#   ./load-tests/run.sh all                 acceptance suite + reconnection-storm + endurance
#   ./load-tests/run.sh --verify            also run verify/verify.sql against the DB afterwards
#   ./load-tests/run.sh --no-up answer-burst use a backend that is already running
#   ./load-tests/run.sh --down              tear the compose stack down (-v) and exit
#   ./load-tests/run.sh --down-after ...    run, then tear the stack down
#   ./load-tests/run.sh -- -e PLAYERS=200   pass extra flags through to every k6 run
#
# Env overrides:
#   BASE_URL      (default http://localhost:5000/api - the docker-compose.yml backend)
#   SIGNALR_URL   (default http://localhost:5000)
#   K6_BIN        (path to k6 if it is not on PATH)
#   HEALTH_TIMEOUT (seconds to wait for the API, default 150)

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
COMPOSE_FILE="$ROOT/docker-compose.yml"

BASE_URL="${BASE_URL:-http://localhost:5000/api}"
SIGNALR_URL="${SIGNALR_URL:-http://localhost:5000}"
HEALTH_URL="${SIGNALR_URL%/}/health"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-150}"
export BASE_URL SIGNALR_URL

DO_UP=1
DO_VERIFY=0
DO_DOWN_AFTER=0
SCENARIOS=()
K6_PASSTHROUGH=()

while [ $# -gt 0 ]; do
  case "$1" in
    --no-up)       DO_UP=0 ;;
    --verify)      DO_VERIFY=1 ;;
    --down-after)  DO_DOWN_AFTER=1 ;;
    --down)
      echo ">> docker compose down -v"
      docker compose -f "$COMPOSE_FILE" down -v
      exit $? ;;
    -h|--help)
      sed -n '2,21{s/^# \{0,1\}//;p}' "$0"
      exit 0 ;;
    --) shift; K6_PASSTHROUGH=("$@"); break ;;
    -*) K6_PASSTHROUGH+=("$1") ;;
    *)  SCENARIOS+=("$1") ;;
  esac
  shift
done

# --- locate k6 -------------------------------------------------------------
if [ -z "${K6_BIN:-}" ]; then
  if command -v k6 >/dev/null 2>&1; then
    K6_BIN="$(command -v k6)"
  elif [ -x "/c/Program Files/k6/k6.exe" ]; then
    K6_BIN="/c/Program Files/k6/k6.exe"
  elif [ -x "/c/Program Files/k6/k6" ]; then
    K6_BIN="/c/Program Files/k6/k6"
  else
    echo "!! k6 not found on PATH. Install k6 or set K6_BIN=/path/to/k6" >&2
    exit 127
  fi
fi
export K6_BIN
command -v node >/dev/null 2>&1 || { echo "!! node not found on PATH" >&2; exit 127; }

echo "== Kahoot load test =="
echo "   target   : $BASE_URL"
echo "   k6       : $K6_BIN"
echo "   scenarios: ${SCENARIOS[*]:-<acceptance suite>}"
echo

# --- 1. bring up the backend -------------------------------------------------
if [ "$DO_UP" -eq 1 ]; then
  echo ">> docker compose up -d --build db backend"
  docker compose -f "$COMPOSE_FILE" up -d --build db backend
fi

# --- 2. wait for health --------------------------------------------------
printf ">> waiting for %s " "$HEALTH_URL"
deadline=$(( $(date +%s) + HEALTH_TIMEOUT ))
until [ "$(curl -s -o /dev/null -w '%{http_code}' "$HEALTH_URL" 2>/dev/null)" = "200" ]; do
  if [ "$(date +%s)" -ge "$deadline" ]; then
    echo " TIMEOUT"
    [ "$DO_UP" -eq 1 ] && docker compose -f "$COMPOSE_FILE" logs --tail 40 backend
    exit 1
  fi
  printf '.'
  sleep 2
done
echo " OK"

# --- 3. run the tests --------------------------------------------------
cd "$HERE"
set +e
if [ ${#K6_PASSTHROUGH[@]} -gt 0 ]; then
  node run-all.js "${SCENARIOS[@]}" -- "${K6_PASSTHROUGH[@]}"
else
  node run-all.js "${SCENARIOS[@]}"
fi
RUN_RC=$?
set -e 2>/dev/null || true

# --- 4. optional DB integrity check --------------------------------------
if [ "$DO_VERIFY" -eq 1 ]; then
  echo
  echo ">> verify/verify.sql (every section must print '(0 rows)')"
  docker compose -f "$COMPOSE_FILE" exec -T db \
    psql -U postgres -d kahoot -v ON_ERROR_STOP=1 < "$HERE/verify/verify.sql"
fi

# --- 5. optional teardown ---------------------------------------------
if [ "$DO_DOWN_AFTER" -eq 1 ]; then
  echo
  echo ">> docker compose down -v"
  docker compose -f "$COMPOSE_FILE" down -v
fi

echo
echo "== done (run-all exit $RUN_RC). Per-scenario JSON in load-tests/results/ =="
exit $RUN_RC
