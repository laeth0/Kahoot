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
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
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
const JOIN_RAMP_SECONDS = parseDurationSeconds(JOIN_RAMP);
const STORM_WINDOW_SECONDS = intEnv('STORM_WINDOW_SECONDS', 45); // total churn window
const STORM_START_SECONDS = JOIN_RAMP_SECONDS + 15;
const TOTAL_HOLD_DURATION_SECONDS = STORM_START_SECONDS + STORM_WINDOW_SECONDS + 60;

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: STORM_PLAYERS },
        { duration: `${STORM_WINDOW_SECONDS + 75}s`, target: STORM_PLAYERS },
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
      maxDuration: `${TOTAL_HOLD_DURATION_SECONDS + 60}s`,
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
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, STORM_PLAYERS);
  const provisionedGame = provisionGames(environmentConfig, { questions: 1, timeLimitSeconds: 60, games: 1 });
  return { env: environmentConfig, gameId: provisionedGame.gameId, pin: provisionedGame.pin, hostToken: provisionedGame.hostToken };
}

export async function player(data) {
  const { env, pin } = data;
  const holdDeadlineTimestampMs = Date.now() + TOTAL_HOLD_DURATION_SECONDS * 1000;

  let signalrClient;
  let sessionToken;
  let participantId;
  try {
    signalrClient = new SignalRClient(env, { onClose: () => {} });
    await signalrClient.start();
    const joinResult = await signalrClient.invoke('JoinGame', pin, uniqueNickname('st'));
    if (!joinResult || joinResult.success !== true) {
      const errorCode = joinResult && joinResult.error ? joinResult.error.code : 'no-response';
      recordJoinFailure(errorCode, 'storm:join');
      signalrClient.close();
      return;
    }
    sessionToken = joinResult.data.sessionToken;
    participantId = joinResult.data.participantId;
    playersJoined.add(1);
    noUnexpected();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('storm:connect');
    return;
  }

  // Hold in the lobby until the whole cohort is in, so the churn is concentrated.
  while (exec.instance.currentTestRunDuration < STORM_START_SECONDS * 1000 && !signalrClient.closed) {
    await delay(500);
  }

  const perRoundGapMs = Math.floor((STORM_WINDOW_SECONDS * 1000) / STORM_ROUNDS);
  for (let roundNumber = 1; roundNumber <= STORM_ROUNDS; roundNumber += 1) {
    try {
      signalrClient.close();
    } catch (_) {
      /* ignore */
    }
    await delay(80 + Math.random() * 700);

    const reconnectedClient = new SignalRClient(env, { onClose: () => {} });
    const reconnectionStartTimeMs = Date.now();
    try {
      await reconnectedClient.start();
      const reconnectResult = await reconnectedClient.invoke('Reconnect', sessionToken);
      reconnectionDuration.add(Date.now() - reconnectionStartTimeMs, { round: String(roundNumber) });
      if (!reconnectResult || reconnectResult.success !== true || !reconnectResult.data) {
        const failureCode = reconnectResult && reconnectResult.error ? reconnectResult.error.code : 'no-data';
        reconnectionFailures.add(1, { round: String(roundNumber), reason: failureCode });
        bumpUnexpected('storm:reconnect:failed');
      } else if (reconnectResult.data.participantId !== participantId) {
        duplicateParticipants.add(1, { where: 'storm' });
        reconnectionFailures.add(1, { round: String(roundNumber), reason: 'identity-changed' });
      } else {
        noUnexpected();
      }
      signalrClient = reconnectedClient;
    } catch (reconnectException) {
      reconnectionFailures.add(1, { round: String(roundNumber), reason: 'exception' });
      bumpUnexpected('storm:reconnect:exception');
      signalrClient = reconnectedClient;
    }

    const nextRoundTimestampMs = Date.now() + perRoundGapMs;
    while (Date.now() < nextRoundTimestampMs && signalrClient && !signalrClient.closed) {
      await delay(300);
    }
  }

  if (signalrClient && !signalrClient.closed && signalrClient.connected) {
    check(signalrClient, { 'connection alive after storm': () => true });
  } else {
    signalrUnexpectedDisconnects.add(1);
    check(null, { 'connection alive after storm': () => false });
  }

  while (Date.now() < holdDeadlineTimestampMs && signalrClient && !signalrClient.closed) {
    await delay(1000);
  }
  if (signalrClient) signalrClient.close();
}

export async function director(data) {
  const { env, hostToken, gameId } = data;
  const directorTags = { scope: 'director' };

  const preStormLobbyState = await waitForParticipantCount(env, hostToken, gameId, STORM_PLAYERS, {
    timeoutMs: (JOIN_RAMP_SECONDS + 60) * 1000,
    minFraction: 0.95,
    intervalMs: 2000,
  });
  const presentParticipantCount = preStormLobbyState.participants.length;
  console.log(`[reconnection-storm] ${presentParticipantCount}/${STORM_PLAYERS} joined; storm window ${STORM_WINDOW_SECONDS}s x ${STORM_ROUNDS} rounds`);

  // Probe responsiveness THROUGH the storm.
  let probeAttemptCount = 0;
  let probeFailureCount = 0;
  const probeUntilMs = (STORM_START_SECONDS + STORM_WINDOW_SECONDS + 5) * 1000;
  while (exec.instance.currentTestRunDuration < probeUntilMs) {
    try {
      const probeState = getHostState(env, hostToken, gameId);
      probeAttemptCount += 1;
      if (probeState.status === undefined || probeState.status === null || (typeof probeState.status !== 'string' && typeof probeState.status !== 'number')) {
        probeFailureCount += 1;
      }
    } catch (probeError) {
      probeAttemptCount += 1;
      probeFailureCount += 1;
    }
    await delay(3000);
  }

  // Recovery verification.
  await delay(10000);
  const postStormState = getHostState(env, hostToken, gameId);
  const participantIds = new Set(postStormState.participants.map((participant) => participant.id));
  const duplicateParticipantCount = postStormState.participants.length - participantIds.size;
  if (duplicateParticipantCount > 0) {
    duplicateParticipants.add(duplicateParticipantCount, { where: 'director:recovery' });
  }

  console.log(
    `[reconnection-storm] probes=${probeAttemptCount} probeFails=${probeFailureCount} roster ${presentParticipantCount} -> ${postStormState.participants.length} dupes=${duplicateParticipantCount}`,
  );
  check(
    { probeFailureCount, presentParticipantCount, postStormState, duplicateParticipantCount },
    {
      'API answered every probe during the storm': (ctx) => ctx.probeFailureCount === 0,
      'roster size unchanged by the storm': (ctx) => ctx.postStormState.participants.length === ctx.presentParticipantCount,
      'no duplicate participants after recovery': (ctx) => ctx.duplicateParticipantCount === 0,
    },
    directorTags,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 90;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('reconnection-storm');
