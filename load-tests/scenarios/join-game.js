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
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay, JoinError } from '../helpers/signalr.js';
import { provisionGames } from '../helpers/testdata.js';
import { uniqueNickname } from '../helpers/testdata.js';
import { getHostState, endGame } from '../helpers/rest.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  playerJoinDuration,
  duplicateParticipants,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const PLAYERS = intEnv('PLAYERS', 500);
const JOIN_RAMP = __ENV.JOIN_RAMP || '120s';
const HOLD_SECONDS = intEnv('HOLD_SECONDS', 45);

export const options = {
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
  const env = resolveEnv();
  assertLoadAllowed(env, PLAYERS);
  const provisioned = provisionGames(env, { questions: 1, timeLimitSeconds: 60, games: 1 });
  return { env, gameId: provisioned.gameId, pin: provisioned.pin, hostToken: provisioned.hostToken };
}

let joined = false;

export default async function (data) {
  const { env, pin } = data;
  if (joined) {
    await delay(2000);
    return;
  }
  joined = true;

  let client;
  const t0 = Date.now();
  try {
    client = new SignalRClient(env);
    await client.start();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('join:connect');
    check(null, { 'player joined': () => false });
    return;
  }

  try {
    const nickname = uniqueNickname('p');
    const res = await client.invoke('JoinGame', pin, nickname);
    if (!res || res.success !== true) {
      const code = res && res.error ? res.error.code : 'no-response';
      recordJoinFailure(code, `join:${code}`);
      check(null, { 'player joined': () => false });
    } else {
      playerJoinDuration.add(Date.now() - t0);
      playersJoined.add(1);
      noUnexpected();
      check(res.data, {
        'player joined': (d) => !!d && !!d.participantId,
        'server issued a session token': (d) => typeof d.sessionToken === 'string' && d.sessionToken.length > 0,
      });
    }
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('join:exception');
  }

  const deadline = Date.now() + HOLD_SECONDS * 1000;
  while (Date.now() < deadline && !client.closed) {
    await delay(1000);
  }
  client.close();
}

export function teardown(data) {
  const { env, hostToken, gameId } = data;
  const state = getHostState(env, hostToken, gameId);

  const ids = new Set(state.participants.map((p) => p.id));
  const nicks = new Set(state.participants.map((p) => p.nickname.toLowerCase()));
  const dupIds = state.participants.length - ids.size;
  const dupNicks = state.participants.length - nicks.size;
  if (dupIds > 0) duplicateParticipants.add(dupIds, { where: 'teardown:ids' });
  if (dupNicks > 0) duplicateParticipants.add(dupNicks, { where: 'teardown:nicknames' });

  check(state, {
    [`host sees exactly ${PLAYERS} participants`]: (s) => s.participants.length === PLAYERS,
    'no duplicate participant ids': () => dupIds === 0,
    'no duplicate nicknames': () => dupNicks === 0,
  });

  console.log(
    `[join-game] host reports ${state.participants.length}/${PLAYERS} participants, ` +
      `dupIds=${dupIds} dupNicks=${dupNicks}`,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort cleanup */
  }
}

export const handleSummary = makeHandleSummary('join-game');
