// Compact, dependency-free end-of-test report. Emits:
//   * a human-readable block on stdout
//   * results/<scenario>-summary.json  (raw k6 metrics, for run-all.js)
// Only metrics k6 actually observed are printed. Infra metrics (CPU / RAM /
// Postgres / Railway) are NOT invented here — see README "What k6 cannot see".

function n(metrics, name, field, dflt) {
  const m = metrics[name];
  if (!m || !m.values) return dflt;
  const v = m.values[field];
  return v === undefined || v === null ? dflt : v;
}

function ms(v) {
  return v === undefined || v === null ? 'n/a' : `${Math.round(v)} ms`;
}

export function makeHandleSummary(scenarioName) {
  return function handleSummary(data) {
    const m = data.metrics || {};
    const line = (label, value) => `  ${label.padEnd(34)} ${value}`;

    const players = n(m, 'players_joined', 'count', 0);
    const conns = n(m, 'signalr_connections', 'count', 0);
    const connFail = n(m, 'signalr_connection_failures', 'count', 0);
    const submitted = n(m, 'answers_submitted', 'count', 0);
    const accepted = n(m, 'answers_accepted', 'count', 0);
    const rejected = n(m, 'answers_rejected', 'count', 0);
    const unexpected = n(m, 'unexpected_answer_failures', 'count', 0);

    const rows = [
      `================  ${scenarioName}  ================`,
      '',
      'PARTICIPATION',
      line('SignalR connections established', conns),
      line('SignalR connection failures', connFail),
      line('Unexpected disconnects', n(m, 'signalr_unexpected_disconnects', 'count', 0)),
      line('Players joined', players),
      line('Player join failures', n(m, 'player_join_failures', 'count', 0)),
      '',
      'ANSWERS',
      line('Answers submitted', submitted),
      line('Answers accepted', accepted),
      line('Answers rejected (expected 4xx)', rejected),
      line('Unexpected answer failures', unexpected),
      line('Throughput (submitted/s)', n(m, 'answers_submitted', 'rate', 0).toFixed(1)),
      '',
      'LATENCY',
      line('API p95 {scope:api}', ms(n(m, 'http_req_duration', 'p(95)'))),
      line('Answer submission p50', ms(n(m, 'answer_submission_duration', 'med'))),
      line('Answer submission p90', ms(n(m, 'answer_submission_duration', 'p(90)'))),
      line('Answer submission p95', ms(n(m, 'answer_submission_duration', 'p(95)'))),
      line('Answer submission p99', ms(n(m, 'answer_submission_duration', 'p(99)'))),
      line('Question delivery p95', ms(n(m, 'question_delivery_duration', 'p(95)'))),
      line('SignalR handshake p95', ms(n(m, 'signalr_handshake_duration', 'p(95)'))),
      '',
      'CORRECTNESS INVARIANTS (must all be 0)',
      line('Lost accepted answers', n(m, 'lost_accepted_answers', 'count', 0)),
      line('Duplicate accepted answers', n(m, 'duplicate_answer_violations', 'count', 0)),
      line('Duplicate score violations', n(m, 'duplicate_score_violations', 'count', 0)),
      line('Inconsistent game state', n(m, 'inconsistent_game_state', 'count', 0)),
      line('Session isolation violations', n(m, 'session_isolation_violations', 'count', 0)),
      line('Duplicate participants', n(m, 'duplicate_participants', 'count', 0)),
      line('Reconnection failures', n(m, 'reconnection_failures', 'count', 0)),
      '',
      'ERRORS',
      line('Unexpected errors (total)', n(m, 'unexpected_errors', 'count', 0)),
      line('Unexpected error rate', `${(n(m, 'unexpected_error_rate', 'rate', 0) * 100).toFixed(2)} %`),
      line('HTTP req failed rate', `${(n(m, 'http_req_failed', 'rate', 0) * 100).toFixed(2)} %`),
      line('Checks passed', `${(n(m, 'checks', 'rate', 1) * 100).toFixed(2)} %`),
      '',
      thresholdReport(data),
      '',
    ];

    const out = {};
    out.stdout = rows.join('\n');
    // handleSummary writes relative to k6's CWD. run-all.js runs from load-tests/
    // and passes an absolute SUMMARY_DIR; for a standalone `k6 run` from the repo
    // root, default to load-tests/results (create it first or `cd load-tests`).
    const dir = (__ENV.SUMMARY_DIR || 'load-tests/results').replace(/[\\/]+$/, '');
    out[`${dir}/${scenarioName}-summary.json`] = JSON.stringify(data, null, 2);
    return out;
  };
}

function thresholdReport(data) {
  const m = data.metrics || {};
  const failed = [];
  for (const name of Object.keys(m)) {
    const t = m[name].thresholds;
    if (!t) continue;
    for (const expr of Object.keys(t)) {
      if (t[expr] && t[expr].ok === false) failed.push(`${name}: ${expr}`);
    }
  }
  if (failed.length === 0) return 'THRESHOLDS: all passed';
  return `THRESHOLDS FAILED (${failed.length}):\n` + failed.map((f) => `  - ${f}`).join('\n');
}
