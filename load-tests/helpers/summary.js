// Compact, dependency-free end-of-test report. Emits:
//   * a human-readable block on stdout
//   * results/<scenario>-summary.json  (raw k6 metrics, for run-all.js)
// Only metrics k6 actually observed are printed. Infra metrics (CPU / RAM /
// Postgres / Railway) are NOT invented here — see README "What k6 cannot see".

function getMetricValue(metricsMap, metricName, fieldName, fallbackDefaultValue) {
  const metricEntry = metricsMap[metricName];
  if (!metricEntry || !metricEntry.values) return fallbackDefaultValue;
  const fieldValue = metricEntry.values[fieldName];
  return fieldValue === undefined || fieldValue === null ? fallbackDefaultValue : fieldValue;
}

function formatMilliseconds(millisecondsValue) {
  return millisecondsValue === undefined || millisecondsValue === null ? 'n/a' : `${Math.round(millisecondsValue)} ms`;
}

export function makeHandleSummary(scenarioName) {
  return function handleSummary(data) {
    const metricsMap = data.metrics || {};
    const line = (label, value) => `  ${label.padEnd(34)} ${value}`;

    const playersJoinedCount = getMetricValue(metricsMap, 'players_joined', 'count', 0);
    const establishedConnections = getMetricValue(metricsMap, 'signalr_connections', 'count', 0);
    const failedConnections = getMetricValue(metricsMap, 'signalr_connection_failures', 'count', 0);
    const submittedAnswersCount = getMetricValue(metricsMap, 'answers_submitted', 'count', 0);
    const acceptedAnswersCount = getMetricValue(metricsMap, 'answers_accepted', 'count', 0);
    const rejectedAnswersCount = getMetricValue(metricsMap, 'answers_rejected', 'count', 0);
    const unexpectedFailuresCount = getMetricValue(metricsMap, 'unexpected_answer_failures', 'count', 0);

    const rows = [
      `================  ${scenarioName}  ================`,
      '',
      'PARTICIPATION',
      line('SignalR connections established', establishedConnections),
      line('SignalR connection failures', failedConnections),
      line('Unexpected disconnects', getMetricValue(metricsMap, 'signalr_unexpected_disconnects', 'count', 0)),
      line('Players joined', playersJoinedCount),
      line('Player join failures', getMetricValue(metricsMap, 'player_join_failures', 'count', 0)),
      '',
      'ANSWERS',
      line('Answers submitted', submittedAnswersCount),
      line('Answers accepted', acceptedAnswersCount),
      line('Answers rejected (expected 4xx)', rejectedAnswersCount),
      line('Unexpected answer failures', unexpectedFailuresCount),
      line('Throughput (submitted/s)', getMetricValue(metricsMap, 'answers_submitted', 'rate', 0).toFixed(1)),
      '',
      'LATENCY',
      line('API p95 {scope:api}', formatMilliseconds(getMetricValue(metricsMap, 'http_req_duration', 'p(95)'))),
      line('Answer submission p50', formatMilliseconds(getMetricValue(metricsMap, 'answer_submission_duration', 'med'))),
      line('Answer submission p90', formatMilliseconds(getMetricValue(metricsMap, 'answer_submission_duration', 'p(90)'))),
      line('Answer submission p95', formatMilliseconds(getMetricValue(metricsMap, 'answer_submission_duration', 'p(95)'))),
      line('Answer submission p99', formatMilliseconds(getMetricValue(metricsMap, 'answer_submission_duration', 'p(99)'))),
      line('Question delivery p95', formatMilliseconds(getMetricValue(metricsMap, 'question_delivery_duration', 'p(95)'))),
      line('SignalR handshake p95', formatMilliseconds(getMetricValue(metricsMap, 'signalr_handshake_duration', 'p(95)'))),
      '',
      'CORRECTNESS INVARIANTS (must all be 0)',
      line('Lost accepted answers', getMetricValue(metricsMap, 'lost_accepted_answers', 'count', 0)),
      line('Duplicate accepted answers', getMetricValue(metricsMap, 'duplicate_answer_violations', 'count', 0)),
      line('Duplicate score violations', getMetricValue(metricsMap, 'duplicate_score_violations', 'count', 0)),
      line('Inconsistent game state', getMetricValue(metricsMap, 'inconsistent_game_state', 'count', 0)),
      line('Session isolation violations', getMetricValue(metricsMap, 'session_isolation_violations', 'count', 0)),
      line('Duplicate participants', getMetricValue(metricsMap, 'duplicate_participants', 'count', 0)),
      line('Reconnection failures', getMetricValue(metricsMap, 'reconnection_failures', 'count', 0)),
      '',
      'ERRORS',
      line('Unexpected errors (total)', getMetricValue(metricsMap, 'unexpected_errors', 'count', 0)),
      line('Unexpected error rate', `${(getMetricValue(metricsMap, 'unexpected_error_rate', 'rate', 0) * 100).toFixed(2)} %`),
      line('HTTP req failed rate', `${(getMetricValue(metricsMap, 'http_req_failed', 'rate', 0) * 100).toFixed(2)} %`),
      line('Checks passed', `${(getMetricValue(metricsMap, 'checks', 'rate', 1) * 100).toFixed(2)} %`),
      '',
      thresholdReport(data),
      '',
    ];

    const breakdown = stressBreakdownReport(data);
    if (breakdown) {
      rows.push(breakdown, '');
    }

    const outputPayload = {};
    outputPayload.stdout = rows.join('\n');
    // handleSummary writes relative to k6's CWD. run-all.js runs from load-tests/
    // and passes an absolute SUMMARY_DIR; for a standalone `k6 run` from the repo
    // root, default to load-tests/results (create it first or `cd load-tests`).
    const directoryPath = (__ENV.SUMMARY_DIR || 'load-tests/results').replace(/[\\/]+$/, '');
    outputPayload[`${directoryPath}/${scenarioName}-summary.json`] = JSON.stringify(data, null, 2);
    return outputPayload;
  };
}

function thresholdReport(data) {
  const metricsMap = data.metrics || {};
  const failedThresholds = [];
  for (const metricName of Object.keys(metricsMap)) {
    const thresholdEntries = metricsMap[metricName].thresholds;
    if (!thresholdEntries) continue;
    for (const expression of Object.keys(thresholdEntries)) {
      if (thresholdEntries[expression] && thresholdEntries[expression].ok === false) {
        failedThresholds.push(`${metricName}: ${expression}`);
      }
    }
  }
  if (failedThresholds.length === 0) return 'THRESHOLDS: all passed';
  return `THRESHOLDS FAILED (${failedThresholds.length}):\n` + failedThresholds.map((failure) => `  - ${failure}`).join('\n');
}

function stressBreakdownReport(data) {
  const metricsMap = data.metrics || {};
  const loadLevels = new Set();

  for (const metricKey of Object.keys(metricsMap)) {
    const match = /\{load:(\d+)\}/.exec(metricKey);
    if (match) {
      loadLevels.add(parseInt(match[1], 10));
    }
  }

  if (loadLevels.size === 0) return '';

  const sortedLevels = Array.from(loadLevels).sort((a, b) => a - b);
  const rows = [
    '================  STRESS & BREAKPOINT ANALYSIS (تحليل نقطة الانهيار)  ================',
    '',
    '  Load Level      Latency (p95)    Submitted     Accepted    Errors (%)    Status',
    '  ---------------------------------------------------------------------------------',
  ];

  let breakingPoint = null;

  for (const level of sortedLevels) {
    const durationKey = `answer_submission_duration{load:${level}}`;
    const errorRateKey = `unexpected_error_rate{load:${level}}`;
    const submittedKey = `answers_submitted{load:${level}}`;
    const acceptedKey = `answers_accepted{load:${level}}`;

    const p95Duration = getMetricValue(metricsMap, durationKey, 'p(95)');
    const p95Formatted = formatMilliseconds(p95Duration);
    const submittedCount = getMetricValue(metricsMap, submittedKey, 'count', 0);
    const acceptedCount = getMetricValue(metricsMap, acceptedKey, 'count', 0);
    const errorRate = getMetricValue(metricsMap, errorRateKey, 'rate', 0);
    const errorRateFormatted = `${(errorRate * 100).toFixed(1)} %`;

    let status = 'HEALTHY (سليم ومستقر)';
    if (p95Duration === undefined && submittedCount === 0) {
      status = 'NO DATA';
    } else if (errorRate >= 0.20 || (p95Duration && p95Duration >= 3000) || (submittedCount > 0 && acceptedCount === 0)) {
      status = '💥 CRASHED / BROKEN (الموقع وقع)';
      if (!breakingPoint) breakingPoint = level;
    } else if (errorRate >= 0.03 || (p95Duration && p95Duration >= 1000)) {
      status = '⚠️ DEGRADED (ضغط عالي / اختناق)';
    }

    const columnLevel = `${level} players`.padEnd(16);
    const columnP95 = p95Formatted.padEnd(17);
    const columnSubmitted = String(submittedCount).padEnd(14);
    const columnAccepted = String(acceptedCount).padEnd(12);
    const columnErrorRate = errorRateFormatted.padEnd(14);

    rows.push(`  ${columnLevel}${columnP95}${columnSubmitted}${columnAccepted}${columnErrorRate}${status}`);
  }

  rows.push('  ---------------------------------------------------------------------------------');
  if (breakingPoint) {
    rows.push(`  🚨 BREAKING POINT DETECTED: السيرفر انهار أو اختنق عند وصول الحمل إلى ${breakingPoint} لاعب!`);
  } else {
    rows.push(`  ✅ PASSED: الموقع صمد بنجاح أمام كافة مستويات الضغط حتى ${sortedLevels[sortedLevels.length - 1]} لاعب.`);
  }
  rows.push('===================================================================================');

  return rows.join('\n');
}
