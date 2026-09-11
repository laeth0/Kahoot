// Scenario 2 — 500 unique players join ONE game.
//
// Each VU connects to the hub and calls JoinGame(pin, <unique nickname>) once,
// then holds the connection in the lobby. teardown() reads the host game state
// and asserts:
//     expected players            = PLAYERS
//     actual players              = PLAYERS
//     duplicate participant ids   = 0
//     duplicate nicknames         = 0
//     unexpected join failures    = 0   (NicknameTaken etc. are unexpected here)
//
//   k6 run -e ALLOW_LOAD_TEST=true load-tests/scenarios/join-game.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { endGame, getHostState } from '../helpers/rest.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  signalrConnectionSuccessRate,
  playerJoinDuration,
  playerJoinFailures,
  playersJoined,
  recordJoinFailure,
  duplicateParticipants,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const PLAYERS = intEnv('PLAYERS', 500);
const JOIN_RAMP = __ENV.JOIN_RAMP || '120s';
const HOLD_SECONDS = intEnv('HOLD_SECONDS', 45);
const TOTAL_SCENARIO_DURATION_MS = (parseDurationSeconds(JOIN_RAMP) + HOLD_SECONDS + 10) * 1000;

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    join: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: PLAYERS },
        { duration: `${HOLD_SECONDS}s`, target: PLAYERS },
        { duration: '10s', target: 0 },
      ],
      gracefulRampDown: '20s',
      gracefulStop: '40s',
    },
  },
  thresholds: {
    player_join_failures: ['count<1'],
    duplicate_participants: ['count<1'],
    signalr_connection_success_rate: ['rate>0.99'],
    checks: ['rate>0.99'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, PLAYERS);
  const provisionedGame = provisionGames(environmentConfig, { questions: 1, timeLimitSeconds: 60, games: 1 });
  return {
    env: environmentConfig,
    gameId: provisionedGame.gameId,
    pin: provisionedGame.pin,
    hostToken: provisionedGame.hostToken,
  };
}

export default async function (data) {
  if (exec.vu.iterationInScenario > 0) {
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  const { env, pin } = data;

  let signalrClient;
  const joinStartTimeMs = Date.now();
  try {
    signalrClient = new SignalRClient(env);
    await signalrClient.start();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('join:connect');
    check(null, { 'player joined': () => false });
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  try {
    const playerNickname = uniqueNickname('p');
    const joinResult = await signalrClient.invoke('JoinGame', pin, playerNickname);
    if (!joinResult || joinResult.success !== true) {
      const errorCode = joinResult && joinResult.error ? joinResult.error.code : 'no-response';
      recordJoinFailure(errorCode, `join:${errorCode}`);
      check(null, { 'player joined': () => false });
      signalrClient.close();
      const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
      await delay(remainingTimeMs);
      return;
    } else {
      playerJoinDuration.add(Date.now() - joinStartTimeMs);
      playersJoined.add(1);
      noUnexpected();
      check(joinResult.data, {
        'player joined': (participantInfo) => !!participantInfo && !!participantInfo.participantId,
        'server issued a session token': (participantInfo) =>
          typeof participantInfo.sessionToken === 'string' && participantInfo.sessionToken.length > 0,
      });
    }
  } catch (joinException) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('join:exception');
    signalrClient.close();
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  while (exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS && !signalrClient.closed) {
    await delay(1000);
  }
  signalrClient.close();
}

export function teardown(data) {
  const { env, hostToken, gameId } = data;
  const hostState = getHostState(env, hostToken, gameId);

  const uniqueParticipantIds = new Set(hostState.participants.map((participant) => participant.id));
  const uniqueNicknames = new Set(hostState.participants.map((participant) => participant.nickname.toLowerCase()));
  const duplicateIdCount = hostState.participants.length - uniqueParticipantIds.size;
  const duplicateNicknameCount = hostState.participants.length - uniqueNicknames.size;

  if (duplicateIdCount > 0) duplicateParticipants.add(duplicateIdCount, { where: 'teardown:ids' });
  if (duplicateNicknameCount > 0) duplicateParticipants.add(duplicateNicknameCount, { where: 'teardown:nicknames' });

  check(hostState, {
    [`host sees exactly ${PLAYERS} participants`]: (state) => state.participants.length === PLAYERS,
    'no duplicate participant ids': () => duplicateIdCount === 0,
    'no duplicate nicknames': () => duplicateNicknameCount === 0,
  });

  console.log(
    `[join-game] host reports ${hostState.participants.length}/${PLAYERS} participants, ` +
      `duplicateIds=${duplicateIdCount} duplicateNicknames=${duplicateNicknameCount}`,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort cleanup */
  }
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 120;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('join-game');
