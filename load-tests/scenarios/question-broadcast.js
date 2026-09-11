// Scenario 3 — Question broadcast with 500 connected players + group isolation.
//
// PLAYERS players join game A. A smaller ISOLATION_PLAYERS group joins game B
// (same quiz, never started). The director starts A's first question and, after
// the window, ends it. We assert:
//   * every game-A player received QuestionStarted   (question_delivery_failures)
//   * end-to-end delivery latency p95 (approx, server->client clock)
//   * NO game-B player ever received any game-A event (session_isolation_violations)
//   * the QuestionStarted payload players get contains no correct answer
//
//   k6 run -e ALLOW_LOAD_TEST=true load-tests/scenarios/question-broadcast.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, endGame } from '../helpers/rest.js';
import { waitForParticipantCount } from '../helpers/orchestration.js';
import { questionDeliveryThresholds, mergeThresholds } from '../config/thresholds.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  questionDeliveryDuration,
  questionDeliveryFailures,
  questionDelivered,
  sessionIsolationViolations,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const PLAYERS = intEnv('PLAYERS', 500);
const ISOLATION_PLAYERS = intEnv('ISOLATION_PLAYERS', 25);
const JOIN_RAMP = __ENV.JOIN_RAMP || '90s';
const TIME_LIMIT = Math.min(300, Math.max(5, intEnv('ANSWER_TIME_LIMIT', 60)));
const CLOCK_SKEW_MS = Number(__ENV.CLOCK_SKEW_MS || 0);
const READY_FRACTION = Number(__ENV.READY_FRACTION || 0.98);

const HOLD = durationSeconds(JOIN_RAMP) + 90 + TIME_LIMIT;

export const options = {
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: PLAYERS + ISOLATION_PLAYERS },
        { duration: `${90 + TIME_LIMIT}s`, target: PLAYERS + ISOLATION_PLAYERS },
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
  thresholds: mergeThresholds(questionDeliveryThresholds(), {
    session_isolation_violations: ['count<1'],
    question_delivery_failures: [`count<${Math.ceil(PLAYERS * 0.01)}`],
    'checks{scope:director}': ['rate>0.99'],
  }),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, PLAYERS + ISOLATION_PLAYERS);
  const p = provisionGames(env, { questions: 1, timeLimitSeconds: TIME_LIMIT, points: 1000, games: 2 });
  return {
    env,
    hostToken: p.hostToken,
    gameA: p.games[0],
    gameB: p.games[1],
    question: p.questions[0],
  };
}

let done = false;

export async function player(data) {
  const { env, gameA, gameB } = data;
  if (done) {
    await delay(3000);
    return;
  }
  done = true;

  // First ISOLATION_PLAYERS VUs are the game-B control group.
  const isControl = exec.vu.idInTest <= ISOLATION_PLAYERS;
  const target = isControl ? gameB : gameA;

  let client;
  let recvAt = 0;
  let payload = null;
  let leaked = false;

  try {
    client = new SignalRClient(env);
    const flag = (name) => (args) => {
      if (isControl) {
        if (name !== 'GameEnded' || (args && args.gameId === gameA.gameId)) {
          leaked = true;
          sessionIsolationViolations.add(1, { event: name });
        }
      } else if (name === 'QuestionStarted' && !recvAt) {
        recvAt = Date.now();
        payload = args;
      }
    };
    client.on('QuestionStarted', flag('QuestionStarted'));
    client.on('QuestionStartedForHost', flag('QuestionStartedForHost'));
    client.on('QuestionEnded', flag('QuestionEnded'));
    client.on('LeaderboardUpdated', flag('LeaderboardUpdated'));
    client.on('GameEnded', flag('GameEnded'));
    await client.start();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('broadcast:connect');
    return;
  }

  try {
    const res = await client.invoke('JoinGame', target.pin, uniqueNickname(isControl ? 'ctl' : 'a'));
    if (!res || res.success !== true) {
      const code = res && res.error ? res.error.code : 'no-response';
      recordJoinFailure(code, `broadcast:join:${code}`);
      client.close();
      return;
    }
    playersJoined.add(1, { group: isControl ? 'B' : 'A' });
    noUnexpected();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('broadcast:join:exception');
    client.close();
    return;
  }

  const deadline = Date.now() + HOLD * 1000;
  while (Date.now() < deadline && !client.closed) {
    if (!isControl && recvAt) break;
    await delay(50);
  }

  if (isControl) {
    check(null, { 'control-group player received NO game-A event': () => !leaked });
  } else if (recvAt) {
    if (payload && payload.endsAt) {
      const skew = Number.isFinite(data.clockSkewMs) ? data.clockSkewMs : CLOCK_SKEW_MS;
      const serverStart = Date.parse(payload.endsAt) - data.question.timeLimitSeconds * 1000;
      const deliveryMs = recvAt - serverStart - skew;
      if (deliveryMs > -2000 && deliveryMs < 60000) questionDeliveryDuration.add(Math.max(1, deliveryMs));
    }
    questionDelivered.add(1);
    check(payload, {
      'received QuestionStarted': (q) => !!q,
      'no correct answer in player payload': (q) =>
        q && q.correctChoiceId === undefined && !(q.choices || []).some((c) => 'isCorrect' in c),
    });
    // linger so we can still catch an isolation leak arriving late
    const lingerUntil = Math.min(deadline, Date.now() + 8000);
    while (Date.now() < lingerUntil && !client.closed) await delay(500);
  } else {
    questionDeliveryFailures.add(1);
    check(null, { 'received QuestionStarted': () => false });
  }

  client.close();
}

export async function director(data) {
  const { env, hostToken, gameA } = data;
  const tags = { scope: 'director' };

  const pre = await waitForParticipantCount(env, hostToken, gameA.gameId, PLAYERS, {
    timeoutMs: (durationSeconds(JOIN_RAMP) + 75) * 1000,
    minFraction: READY_FRACTION,
    intervalMs: 2000,
  });
  console.log(`[question-broadcast] starting with ${pre.participants.length}/${PLAYERS} game-A players`);
  check(pre, {
    [`>= ${Math.ceil(PLAYERS * READY_FRACTION)} game-A players present`]: (s) =>
      s.participants.length >= Math.ceil(PLAYERS * READY_FRACTION),
  }, tags);

  const started = startGame(env, hostToken, gameA.gameId);
  check(started, { 'question started': (s) => !!s && !!s.player }, tags);

  await delay((data.question.timeLimitSeconds + 10) * 1000);
  endQuestion(env, hostToken, gameA.gameId);

  try {
    endGame(env, hostToken, gameA.gameId);
    endGame(env, hostToken, data.gameB.gameId);
  } catch (_) {
    /* best-effort */
  }
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 90;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('question-broadcast');
