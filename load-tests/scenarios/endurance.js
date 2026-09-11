// Scenario 10 — Endurance / soak.
//
// A realistic game loop repeated for ENDURANCE_MINUTES:
//   question start -> players answer -> end question -> leaderboard -> advance.
// ENDURANCE_PLAYERS players each hold ONE SignalR connection for the whole run
// and answer every question. Configurable down to 1 minute for dev.
//
// What k6 can see here: latency drift (answer p95 first half vs second half),
// error-rate drift, unexpected disconnects, reconnect need over time.
// What k6 CANNOT see: process RSS, GC, Npgsql pool size, Railway container
// metrics — read those from Railway / PostgreSQL dashboards for the same window
// (see README "What k6 cannot see").
//
//   k6 run -e ALLOW_LOAD_TEST=true -e ENDURANCE_MINUTES=10 -e ENDURANCE_PLAYERS=200 \
//     load-tests/scenarios/endurance.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, showLeaderboard, advance, endGame } from '../helpers/rest.js';
import { waitForParticipantCount } from '../helpers/orchestration.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  answersSubmitted,
  answersAccepted,
  answersRejected,
  unexpectedAnswerFailures,
  answerSubmissionDuration,
  signalrUnexpectedDisconnects,
  reconnectionFailures,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const MINUTES = Math.max(1, intEnv('ENDURANCE_MINUTES', 10));
const PLAYERS = intEnv('ENDURANCE_PLAYERS', 200);
const Q_LEN = Math.min(300, Math.max(8, intEnv('ENDURANCE_QUESTION_SECONDS', 15)));
const GAP = intEnv('ENDURANCE_GAP_SECONDS', 8);
const PERIOD = Q_LEN + GAP;
const RUN_S = MINUTES * 60;
const Q_COUNT = Math.min(120, Math.ceil(RUN_S / PERIOD) + 3);
const HALF_MS = (RUN_S / 2) * 1000;

export const options = {
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: '60s', target: PLAYERS },
        { duration: `${RUN_S}s`, target: PLAYERS },
        { duration: '15s', target: 0 },
      ],
      gracefulRampDown: '20s',
      gracefulStop: '45s',
    },
    director: {
      executor: 'per-vu-iterations',
      exec: 'director',
      vus: 1,
      iterations: 1,
      maxDuration: `${RUN_S + 180}s`,
    },
  },
  thresholds: {
    answer_submission_duration: ['p(95)<500'],
    'answer_submission_duration{half:first}': ['p(95)<500'],
    'answer_submission_duration{half:second}': ['p(95)<500'],
    unexpected_error_rate: ['rate<0.01'],
    signalr_unexpected_disconnects: [`count<${Math.ceil(PLAYERS * 0.05)}`],
    checks: ['rate>0.98'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, PLAYERS);
  const p = provisionGames(env, { questions: Q_COUNT, timeLimitSeconds: Q_LEN, points: 1000, games: 1 });
  console.log(`[endurance] ${MINUTES} min, ${PLAYERS} players, ${p.questions.length} questions, period ${PERIOD}s`);
  return { env, gameId: p.gameId, pin: p.pin, hostToken: p.hostToken, questions: p.questions };
}

let joinState = 'pending';
let client = null;
let currentQ = null;
let answeredQ = null;
let sessionToken = null;
let disconnects = 0;

export async function player(data) {
  const { env, pin } = data;

  if (joinState === 'pending') {
    client = new SignalRClient(env, {
      onClose: () => {
        disconnects += 1;
        signalrUnexpectedDisconnects.add(1);
      },
    });
    client.on('QuestionStarted', (q) => {
      if (q && q.questionId) currentQ = q.questionId;
    });
    try {
      await client.start();
      const res = await client.invoke('JoinGame', pin, uniqueNickname('e'));
      if (!res || res.success !== true) {
        recordJoinFailure(res && res.error ? res.error.code : 'no-response', 'endurance:join');
        client.close();
        joinState = 'failed';
        return;
      }
      sessionToken = res.data.sessionToken;
      playersJoined.add(1);
      noUnexpected();
      joinState = 'ok';
    } catch (e) {
      playerJoinFailures.add(1, { reason: 'connect' });
      bumpUnexpected('endurance:connect');
      joinState = 'failed';
      return;
    }
  }

  if (joinState !== 'ok') {
    await delay(3000);
    return;
  }

  // Self-heal a dropped connection (this is a soak test — drops over hours matter).
  if (!client || client.closed) {
    const c = new SignalRClient(env, {
      onClose: () => {
        signalrUnexpectedDisconnects.add(1);
      },
    });
    c.on('QuestionStarted', (q) => {
      if (q && q.questionId) currentQ = q.questionId;
    });
    try {
      await c.start();
      const rc = await c.invoke('Reconnect', sessionToken);
      if (!rc || rc.success !== true) {
        reconnectionFailures.add(1, { where: 'endurance' });
        bumpUnexpected('endurance:reconnect');
      }
      client = c;
    } catch (e) {
      reconnectionFailures.add(1, { where: 'endurance:exception' });
      bumpUnexpected('endurance:reconnect:exception');
      await delay(2000);
      return;
    }
  }

  if (currentQ && currentQ !== answeredQ) {
    answeredQ = currentQ;
    const q = data.questions.find((x) => x.questionId === currentQ);
    if (q) {
      const half = exec.instance.currentTestRunDuration < 60000 + HALF_MS ? 'first' : 'second';
      const t0 = Date.now();
      try {
        const ack = await client.invoke('SubmitAnswer', currentQ, q.correctChoiceId);
        answerSubmissionDuration.add(Date.now() - t0, { half });
        answersSubmitted.add(1, { half });
        if (ack && ack.success === true && ack.data && ack.data.accepted === true) {
          answersAccepted.add(1, { half });
          noUnexpected();
        } else if (ack && ack.success === false && ack.error) {
          answersRejected.add(1, { code: ack.error.code });
        } else {
          unexpectedAnswerFailures.add(1, { half });
          bumpUnexpected('endurance:submit:malformed');
        }
      } catch (e) {
        unexpectedAnswerFailures.add(1, { half });
        bumpUnexpected('endurance:submit:exception');
      }
    }
  }

  await delay(1000);
}

export async function director(data) {
  const { env, hostToken, gameId, questions } = data;
  console.log(`[endurance] waiting for players to assemble in lobby (target: ${PLAYERS})...`);
  await waitForParticipantCount(env, hostToken, gameId, PLAYERS, {
    timeoutMs: 90000,
    minFraction: 0.90,
    intervalMs: 1500,
  });

  let started = false;
  let cycles = 0;
  const stopAt = RUN_S * 1000 + 70000;
  for (let i = 0; i < questions.length; i += 1) {
    if (exec.instance.currentTestRunDuration > stopAt) break;
    try {
      if (!started) {
        startGame(env, hostToken, gameId);
        started = true;
      } else {
        advance(env, hostToken, gameId);
      }
      await delay((questions[i].timeLimitSeconds + 3) * 1000);
      endQuestion(env, hostToken, gameId);
      showLeaderboard(env, hostToken, gameId);
      cycles += 1;
      await delay(Math.max(1000, (GAP - 3) * 1000));
    } catch (e) {
      console.log(`[endurance] director stop at cycle ${cycles}: ${e}`);
      break;
    }
  }

  console.log(`[endurance] completed ${cycles} question cycles`);
  check({ cycles }, { 'ran a realistic number of question cycles': (x) => x.cycles >= Math.min(3, questions.length) });
  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

export const handleSummary = makeHandleSummary('endurance');
