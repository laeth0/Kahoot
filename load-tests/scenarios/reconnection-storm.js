// Scenario 9 — Reconnection storm.
//
// STORM_PLAYERS players join, then (once the join ramp is done) every player
// churns its connection STORM_ROUNDS times in a short window:
//   close socket -> brief jittered outage -> new socket -> Reconnect(token).
// Measures reconnect failures, reconnect latency, duplicate participants, and
// whether the API stays responsive during and recovers after the storm.
//
//   k6 run -e ALLOW_LOAD_TEST=true -e STORM_PLAYERS=500 -e STORM_ROUNDS=3 \
//     load-tests/scenarios/reconnection-storm.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { getHostState, endGame } from '../helpers/rest.js';
import { waitForParticipantCount } from '../helpers/orchestration.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  reconnectionDuration,
  reconnectionFailures,
  duplicateParticipants,
  signalrUnexpectedDisconnects,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const STORM_PLAYERS = intEnv('STORM_PLAYERS', intEnv('PLAYERS', 500));
const STORM_ROUNDS = intEnv('STORM_ROUNDS', 3);
const JOIN_RAMP = __ENV.JOIN_RAMP || '90s';
const JOIN_RAMP_S = durationSeconds(JOIN_RAMP);
const STORM_WINDOW = intEnv('STORM_WINDOW_SECONDS', 45); // total churn window
const STORM_START_S = JOIN_RAMP_S + 15;
const HOLD = STORM_START_S + STORM_WINDOW + 60;

export const options = {
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: STORM_PLAYERS },
        { duration: `${STORM_WINDOW + 75}s`, target: STORM_PLAYERS },
        { duration: '10s', target: 0 },
      ],
      gracefulRampDown: '30s',
      gracefulStop: '60s',
    },
    director: {
      executor: 'per-vu-iterations',
      exec: 'director',
      vus: 1,
      iterations: 1,
      maxDuration: `${HOLD + 60}s`,
    },
  },
  thresholds: {
    reconnection_failures: [`count<${Math.max(1, Math.ceil(STORM_PLAYERS * STORM_ROUNDS * 0.01))}`],
    reconnection_duration: ['p(95)<4000'],
    duplicate_participants: ['count<1'],
    'checks{scope:director}': ['rate>0.99'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, STORM_PLAYERS);
  const p = provisionGames(env, { questions: 1, timeLimitSeconds: 60, games: 1 });
  return { env, gameId: p.gameId, pin: p.pin, hostToken: p.hostToken };
}

let done = false;

export async function player(data) {
  const { env, pin } = data;
  if (done) {
    await delay(3000);
    return;
  }
  done = true;

  let client;
  let token;
  let participantId;
  try {
    client = new SignalRClient(env, { onClose: () => {} });
    await client.start();
    const res = await client.invoke('JoinGame', pin, uniqueNickname('st'));
    if (!res || res.success !== true) {
      recordJoinFailure(res && res.error ? res.error.code : 'no-response', 'storm:join');
      client.close();
      return;
    }
    token = res.data.sessionToken;
    participantId = res.data.participantId;
    playersJoined.add(1);
    noUnexpected();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('storm:connect');
    return;
  }

  // Hold in the lobby until the whole cohort is in, so the churn is concentrated.
  while (exec.instance.currentTestRunDuration < STORM_START_S * 1000 && !client.closed) {
    await delay(500);
  }

  const perRoundGap = Math.floor((STORM_WINDOW * 1000) / STORM_ROUNDS);
  for (let round = 1; round <= STORM_ROUNDS; round += 1) {
    try {
      client.close();
    } catch (_) {
      /* ignore */
    }
    await delay(80 + Math.random() * 700);

    const c = new SignalRClient(env, { onClose: () => {} });
    const t0 = Date.now();
    try {
      await c.start();
      const rc = await c.invoke('Reconnect', token);
      reconnectionDuration.add(Date.now() - t0, { round: String(round) });
      if (!rc || rc.success !== true || !rc.data) {
        reconnectionFailures.add(1, { round: String(round), reason: rc && rc.error ? rc.error.code : 'no-data' });
        bumpUnexpected('storm:reconnect:failed');
      } else if (rc.data.participantId !== participantId) {
        duplicateParticipants.add(1, { where: 'storm' });
        reconnectionFailures.add(1, { round: String(round), reason: 'identity-changed' });
      } else {
        noUnexpected();
      }
      client = c;
    } catch (e) {
      reconnectionFailures.add(1, { round: String(round), reason: 'exception' });
      bumpUnexpected('storm:reconnect:exception');
      client = c;
    }

    const nextRoundAt = Date.now() + perRoundGap;
    while (Date.now() < nextRoundAt && client && !client.closed) await delay(300);
  }

  if (client && !client.closed && client.connected) {
    check(client, { 'connection alive after storm': () => true });
  } else {
    signalrUnexpectedDisconnects.add(1);
    check(null, { 'connection alive after storm': () => false });
  }

  const deadline = Date.now() + 30000;
  while (Date.now() < deadline && client && !client.closed) await delay(1000);
  if (client) client.close();
}

export async function director(data) {
  const { env, hostToken, gameId } = data;
  const tags = { scope: 'director' };

  const pre = await waitForParticipantCount(env, hostToken, gameId, STORM_PLAYERS, {
    timeoutMs: (JOIN_RAMP_S + 60) * 1000,
    minFraction: 0.95,
    intervalMs: 2000,
  });
  const present = pre.participants.length;
  console.log(`[reconnection-storm] ${present}/${STORM_PLAYERS} joined; storm window ${STORM_WINDOW}s x ${STORM_ROUNDS} rounds`);

  // Probe responsiveness THROUGH the storm.
  let probes = 0;
  let probeFails = 0;
  const probeUntil = (STORM_START_S + STORM_WINDOW + 5) * 1000;
  while (exec.instance.currentTestRunDuration < probeUntil) {
    try {
      const s = getHostState(env, hostToken, gameId);
      probes += 1;
      if (s.status === undefined || s.status === null || (typeof s.status !== 'string' && typeof s.status !== 'number')) {
        probeFails += 1;
      }
    } catch (e) {
      probes += 1;
      probeFails += 1;
    }
    await delay(3000);
  }

  // Recovery.
  await delay(10000);
  const post = getHostState(env, hostToken, gameId);
  const ids = new Set(post.participants.map((p) => p.id));
  const dupes = post.participants.length - ids.size;
  if (dupes > 0) duplicateParticipants.add(dupes, { where: 'director:recovery' });

  console.log(
    `[reconnection-storm] probes=${probes} probeFails=${probeFails} roster ${present} -> ${post.participants.length} dupes=${dupes}`,
  );
  check(
    { probeFails, present, post, dupes },
    {
      'API answered every probe during the storm': (x) => x.probeFails === 0,
      'roster size unchanged by the storm': (x) => x.post.participants.length === x.present,
      'no duplicate participants after recovery': (x) => x.dupes === 0,
    },
    tags,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 90;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('reconnection-storm');
