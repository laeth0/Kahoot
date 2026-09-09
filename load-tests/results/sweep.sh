set -u
K6="/c/Program Files/k6/k6"
B="-e BASE_URL=http://localhost:5000/api -e SIGNALR_URL=http://localhost:5000 -e ALLOW_LOAD_TEST=true -e SAFE_VU_LIMIT=40 -e SIGNALR_SKIP_NEGOTIATION=true -e SUMMARY_DIR=$PWD/results -e ANSWER_TIME_LIMIT=20 -e SETTLE_SECONDS=12 -e CONFIRM_FRACTION=0.2"
for P in 50 100 150 200 250; do
  docker ps --format '{{.Names}}' | grep -q kahoot-backend || { echo "BACKEND DOWN at P=$P"; break; }
  RAMP=$(( P/3 )); [ $RAMP -lt 12 ] && RAMP=12
  echo "===== P=$P ramp=${RAMP}s $(date +%H:%M:%S) $(docker stats --no-stream --format '{{.Name}}:{{.CPUPerc}}/{{.MemUsage}}' kahoot-backend 2>/dev/null) ====="
  "$K6" run --summary-mode compact -q $B -e PLAYERS=$P -e JOIN_RAMP=${RAMP}s scenarios/answer-burst.js 2>&1 \
    | grep -E "players present|results: answerCount|Answers (submitted|accepted)|Answer submission p(50|90|95|99)|Question delivery p95|Throughput|Lost accepted|Duplicate (accepted|score)|Inconsistent game|Unexpected errors \(|HTTP req failed|Checks passed|THRESHOLDS"
  [ -f results/answer-burst-summary.json ] && mv results/answer-burst-summary.json results/burst-P${P}.json
  echo "peak backend: $(docker stats --no-stream --format '{{.CPUPerc}} {{.MemUsage}}' kahoot-backend 2>/dev/null)"
  sleep 35
done
echo "===== SWEEP DONE $(date +%H:%M:%S) ====="
