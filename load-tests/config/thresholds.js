// Threshold sets derived from non-functional-requirements.md (NFR-1 / NFR-2).
// k6 exits non-zero (code 99) if any threshold fails, so a violated requirement
// makes the whole run FAIL — the brief's "must exit as failed" rule.

// Normal REST API latency + global error budget.
export function apiThresholds() {
  return {
    'http_req_duration{scope:api}': ['p(95)<300'],
    'http_req_failed{scope:api}': ['rate<0.01'],
    unexpected_error_rate: ['rate<0.01'],
    checks: ['rate>0.99'],
  };
}

// Answer submission latency (NFR-1: answer p95 < 500 ms).
export function answerThresholds() {
  return {
    answer_submission_duration: ['p(95)<500', 'p(99)<2000'],
    'http_req_failed{scope:api}': ['rate<0.01'],
    unexpected_error_rate: ['rate<0.01'],
  };
}

// Zero-tolerance correctness invariants. `abortOnFail` stops the run the moment
// one is breached — a lost/duplicated answer is never "acceptable under load".
export function correctnessThresholds() {
  const zero = (name) => ({ [name]: [{ threshold: 'count<1', abortOnFail: false }] });
  return Object.assign(
    {},
    zero('lost_accepted_answers'),
    zero('duplicate_answer_violations'),
    zero('duplicate_score_violations'),
    zero('inconsistent_game_state'),
    zero('session_isolation_violations'),
    zero('duplicate_participants'),
  );
}

// SignalR connection reliability (NFR-1: 500 concurrent connections).
export function connectionThresholds(maxFailureRate = 0.01) {
  return {
    signalr_connection_success_rate: [`rate>${1 - maxFailureRate}`],
    signalr_handshake_duration: ['p(95)<3000'],
  };
}

export function questionDeliveryThresholds() {
  return {
    // "hundreds of ms under expected load" — allow headroom, flag regressions.
    question_delivery_duration: ['p(95)<2000', 'p(99)<5000'],
    question_delivery_failures: ['count<1'],
  };
}

export function reconnectionThresholds() {
  return {
    reconnection_duration: ['p(95)<3000'],
    reconnection_failures: ['count<1'],
    duplicate_participants: ['count<1'],
  };
}

export function mergeThresholds(...sets) {
  const out = {};
  for (const s of sets) {
    for (const k of Object.keys(s)) {
      out[k] = (out[k] || []).concat(s[k]);
    }
  }
  return out;
}
