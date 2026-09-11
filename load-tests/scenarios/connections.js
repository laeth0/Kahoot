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
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
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
const RAMP_DOWN_SECONDS = 15;
const TOTAL_DURATION_MS = (parseDurationSeconds(CONNECT_RAMP) + HOLD_SECONDS + RAMP_DOWN_SECONDS) * 1000;

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    connections: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: CONNECT_RAMP, target: PLAYERS },
        { duration: `${HOLD_SECONDS}s`, target: PLAYERS },
        { duration: `${RAMP_DOWN_SECONDS}s`, target: 0 },
      ],
      gracefulRampDown: '20s',
      gracefulStop: '45s',
    },
  },
  thresholds: connectionThresholds(Number(__ENV.MAX_CONN_FAILURE_RATE || 0.02)),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, PLAYERS);
  return { env: environmentConfig };
}

export default async function (data) {
  if (exec.vu.iterationInScenario > 0) {
    const remainingTimeMs = Math.max(100, TOTAL_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  let wasConnectionDroppedEarly = false;
  const signalrClient = new SignalRClient(data.env, {
    onClose: () => {
      wasConnectionDroppedEarly = true;
    },
  });

  try {
    await signalrClient.start();
  } catch (connectionError) {
    bumpUnexpected('connections:connect');
    check(null, { 'connection established': () => false });
    const remainingTimeMs = Math.max(100, TOTAL_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  check(signalrClient, { 'connection established': (activeClient) => activeClient.connected === true });
  noUnexpected();

  // Hold until the end of the scenario, sampling the live count as we go.
  const targetHoldUntilMs = TOTAL_DURATION_MS - 8000;
  while (exec.instance.currentTestRunDuration < targetHoldUntilMs && !signalrClient.closed) {
    signalrConnectionsActive.add(exec.instance.vusActive);
    await delay(2000);
  }

  if (wasConnectionDroppedEarly && exec.instance.currentTestRunDuration < targetHoldUntilMs - 3000) {
    signalrUnexpectedDisconnects.add(1);
    check(null, { 'held the connection for the whole window': () => false });
  } else {
    check(signalrClient, { 'held the connection for the whole window': () => true });
  }

  while (exec.instance.currentTestRunDuration < TOTAL_DURATION_MS && !signalrClient.closed) {
    await delay(1000);
  }
  signalrClient.close();
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 240;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('connections');
