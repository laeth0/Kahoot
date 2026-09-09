// Scenario 1 — 500 concurrent SignalR connections.
//
// Each VU establishes ONE connection (negotiate + WebSocket + JSON handshake
// against the real hub, or a direct WS upgrade when SIGNALR_SKIP_NEGOTIATION)
// and HOLDS it, staying responsive to keep-alive, until the scenario is nearly
// over. So at the top of the hold window ~PLAYERS connections are open at once.
//
// IMPORTANT — single load-generator IP: the API's global per-IP rate limiter
// (240 burst + 120 / 30 s) throttles how fast one machine can open connections.
// CONNECT_RAMP is deliberately long so the negotiate/upgrade traffic fits that
// budget; the client retries a 429 after a real token refill. This measures the
// app's ability to *hold* 500 connections, with onboarding paced by the limiter
// (not the app). For an instantaneous 500-distinct-client join, run k6
// distributed. See README "single load-generator IP".
//
//   k6 run -e ALLOW_LOAD_TEST=true -e SIGNALR_SKIP_NEGOTIATION=true \
//     load-tests/scenarios/connections.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { connectionThresholds } from '../config/thresholds.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  signalrConnectionsActive,
  signalrUnexpectedDisconnects,
  noUnexpected,
  bumpUnexpected,
} from '../helpers/metrics.js';

const PLAYERS = intEnv('PLAYERS', 500);
const CONNECT_RAMP = __ENV.CONNECT_RAMP || '240s';
const HOLD_SECONDS = intEnv('HOLD_SECONDS', 90);
const DOWN_SECONDS = 15;
const TOTAL_MS = (durationSeconds(CONNECT_RAMP) + HOLD_SECONDS + DOWN_SECONDS) * 1000;

export const options = {
  scenarios: {
    connections: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: CONNECT_RAMP, target: PLAYERS },
        { duration: `${HOLD_SECONDS}s`, target: PLAYERS },
        { duration: `${DOWN_SECONDS}s`, target: 0 },
      ],
      gracefulRampDown: '20s',
      gracefulStop: '45s',
    },
  },
  thresholds: connectionThresholds(Number(__ENV.MAX_CONN_FAILURE_RATE || 0.02)),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, PLAYERS);
  return { env };
}

// One connection per VU, held for the rest of the run.
let connected = false;

export default async function (data) {
  if (connected) {
    await delay(3000);
    return;
  }
  connected = true;

  let droppedEarly = false;
  const client = new SignalRClient(data.env, {
    onClose: () => {
      droppedEarly = true;
    },
  });

  try {
    await client.start();
  } catch (e) {
    bumpUnexpected('connections:connect');
    check(null, { 'connection established': () => false });
    return;
  }

  check(client, { 'connection established': (c) => c.connected === true });
  noUnexpected();

  // Hold until ~the end of the scenario, sampling the live count as we go.
  const holdUntil = TOTAL_MS - 8000;
  while (exec.instance.currentTestRunDuration < holdUntil && !client.closed) {
    signalrConnectionsActive.add(exec.instance.vusActive);
    await delay(2000);
  }

  if (droppedEarly && exec.instance.currentTestRunDuration < holdUntil - 3000) {
    signalrUnexpectedDisconnects.add(1);
    check(null, { 'held the connection for the whole window': () => false });
  } else {
    check(client, { 'held the connection for the whole window': () => true });
  }

  client.close();
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 240;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('connections');
