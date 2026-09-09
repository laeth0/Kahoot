// Scenario 8 — Ramp / stress: 50 -> 100 -> 250 -> 500 -> 750 concurrent players.
//
// Players hold a live SignalR connection for their whole lifetime. The director
// runs a continuous question cycle on a long multi-question quiz
// (start -> end-question -> leaderboard -> advance -> ...). On every new question
// each player submits exactly one answer. answer_submission_duration and error
// rate are tagged by load bucket so you can see where p95 crosses 500 ms.
//
//   500 users is the acceptance target; 750 is there to expose the margin.
//
//   k6 run -e ALLOW_LOAD_TEST=true load-tests/scenarios/ramp.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, showLeaderboard, advance, endGame } from '../helpers/rest.js';
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
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const LEVELS = (__ENV.RAMP_LEVELS || '50,100,250,500,750').split(',').map((s) => parseInt(s.trim(), 10));
const PEAK = Math.max(...LEVELS);
const STEP_HOLD = __ENV.RAMP_STEP_HOLD || '90s';
// Step-up ramps must stay within the global per-IP limiter (~4 req/s refill,
// 1 req per connection with SIGNALR_SKIP_NEGOTIATION). 90 s to add 250 VUs
// (250->500) is ~2.8/s. Shorten only when running distributed / limits relaxed.
const STEP_RAMP = __ENV.RAMP_STEP_RAMP || '90s';
const Q_LEN = Math.min(300, Math.max(20, intEnv('RAMP_QUESTION_SECONDS', 40)));
const Q_COUNT = intEnv('RAMP_QUESTIONS', 18);

const stages = [];
for (const lvl of LEVELS) {
  stages.push({ duration: STEP_RAMP, target: lvl });
  stages.push({ duration: STEP_HOLD, target: lvl });
}
stages.push({ duration: '15s', target: 0 });

function loadBucket() {
  const active = exec.instance.vusActive;
  let nearest = LEVELS[0];
  for (const l of LEVELS) if (Math.abs(l - active) < Math.abs(nearest - active)) nearest = l;
  return String(nearest);
}

const perLevel = {};
for (const l of LEVELS) {
  perLevel[`answer_submission_duration{load:${l}}`] = l <= 500 ? ['p(95)<500'] : [{ threshold: 'p(95)<100000', abortOnFail: false }];
  perLevel[`unexpected_error_rate{load:${l}}`] = l <= 500 ? ['rate<0.01'] : [{ threshold: 'rate<1', abortOnFail: false }];
}

export const options = {
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages,
      gracefulRampDown: '15s',
      gracefulStop: '30s',
    },
    director: {
      executor: 'per-vu-iterations',
      exec: 'director',
      vus: 1,
      iterations: 1,
      maxDuration: `${stages.reduce((s, st) => s + durationSeconds(st.duration), 0) + 120}s`,
    },
  },
  thresholds: Object.assign(
    {
      'answer_submission_duration{load:500}': ['p(95)<500'],
      checks: ['rate>0.95'],
    },
    perLevel,
  ),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, PEAK);
  const p = provisionGames(env, { questions: Q_COUNT, timeLimitSeconds: Q_LEN, points: 1000, games: 1 });
  console.log(`[ramp] game ${p.gameId} pin ${p.pin} with ${p.questions.length} questions`);
  return { env, gameId: p.gameId, pin: p.pin, hostToken: p.hostToken, questions: p.questions };
}

let joinState = 'pending'; // 'pending' | 'ok' | 'failed'
let client = null;
let currentQ = null;
let answeredQ = null;

export async function player(data) {
  const { env, pin } = data;

  if (joinState === 'pending') {
    client = new SignalRClient(env, { onClose: () => {} });
    client.on('QuestionStarted', (q) => {
      if (q && q.questionId) currentQ = q.questionId;
    });
    try {
      await client.start();
      const res = await client.invoke('JoinGame', pin, uniqueNickname('s'));
      if (!res || res.success !== true) {
        recordJoinFailure(res && res.error ? res.error.code : 'no-response', 'ramp:join');
        client.close();
        joinState = 'failed';
        return;
      }
      playersJoined.add(1);
      noUnexpected();
      joinState = 'ok';
    } catch (e) {
      playerJoinFailures.add(1, { reason: 'connect' });
      bumpUnexpected('ramp:connect');
      joinState = 'failed';
      return;
    }
  }

  if (joinState !== 'ok' || !client || client.closed) {
    await delay(2000);
    return;
  }

  // Answer the current question once.
  if (currentQ && currentQ !== answeredQ) {
    answeredQ = currentQ;
    const q = data.questions.find((x) => x.questionId === currentQ);
    const choiceId = q ? q.correctChoiceId : null;
    if (choiceId) {
      const t0 = Date.now();
      const tags = { load: loadBucket() };
      try {
        const ack = await client.invoke('SubmitAnswer', currentQ, choiceId);
        answerSubmissionDuration.add(Date.now() - t0, tags);
        answersSubmitted.add(1, tags);
        if (ack && ack.success === true && ack.data && ack.data.accepted === true) {
          answersAccepted.add(1, tags);
          noUnexpected();
        } else if (ack && ack.success === false && ack.error) {
          answersRejected.add(1, { code: ack.error.code });
        } else {
          unexpectedAnswerFailures.add(1, tags);
          bumpUnexpected('ramp:submit:malformed');
        }
      } catch (e) {
        unexpectedAnswerFailures.add(1, tags);
        bumpUnexpected('ramp:submit:exception');
      }
    }
  }

  await delay(1000);
}

export async function director(data) {
  const { env, hostToken, gameId, questions } = data;

  // Wait briefly for the first cohort to connect.
  await delay(durationSeconds(STEP_RAMP) * 1000 + 5000);

  let started = false;
  for (let i = 0; i < questions.length; i += 1) {
    try {
      if (!started) {
        startGame(env, hostToken, gameId);
        started = true;
      } else {
        advance(env, hostToken, gameId);
      }
    } catch (e) {
      console.log(`[ramp] director stop at question ${i}: ${e}`);
      break;
    }
    await delay((questions[i].timeLimitSeconds + 3) * 1000);
    try {
      endQuestion(env, hostToken, gameId);
      showLeaderboard(env, hostToken, gameId);
    } catch (e) {
      console.log(`[ramp] director close/leaderboard error at q${i}: ${e}`);
      break;
    }
    await delay(2000);
  }

  check(started, { 'director drove at least one question': (s) => s === true });
  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 30;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('ramp');
