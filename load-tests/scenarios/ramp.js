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
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
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
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const LEVELS = (__ENV.RAMP_LEVELS || '100,250,500').split(',').map((s) => parseInt(s.trim(), 10));
const PEAK = Math.max(...LEVELS);
const JOIN_RAMP = __ENV.RAMP_JOIN_RAMP || __ENV.JOIN_RAMP || '60s';
const Q_LEN = Math.min(300, Math.max(10, intEnv('RAMP_QUESTION_SECONDS', 20)));
const Q_COUNT = Math.max(LEVELS.length, intEnv('RAMP_QUESTIONS', LEVELS.length));

const stages = [
  { duration: JOIN_RAMP, target: PEAK },
  { duration: `${Q_COUNT * (Q_LEN + 6) + 10}s`, target: PEAK },
  { duration: '10s', target: 0 },
];

const perLevel = {};
for (const l of LEVELS) {
  perLevel[`answer_submission_duration{load:${l}}`] = l <= 500 ? ['p(95)<1000'] : [{ threshold: 'p(95)<100000', abortOnFail: false }];
  perLevel[`unexpected_error_rate{load:${l}}`] = l <= 500 ? ['rate<0.05'] : [{ threshold: 'rate<1', abortOnFail: false }];
  perLevel[`answers_submitted{load:${l}}`] = ['count>=0'];
  perLevel[`answers_accepted{load:${l}}`] = ['count>=0'];
}

export const options = {
  hosts: hostsOverride(),
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
      checks: ['rate>0.90'],
    },
    perLevel,
  ),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, PEAK);
  const p = provisionGames(env, { questions: Q_COUNT, timeLimitSeconds: Q_LEN, points: 1000, games: 1 });
  console.log(`[ramp] game ${p.gameId} pin ${p.pin} with ${p.questions.length} questions for levels: ${LEVELS.join(', ')}`);
  return { env, gameId: p.gameId, pin: p.pin, hostToken: p.hostToken, questions: p.questions };
}

export async function player(data) {
  const { env, pin, questions } = data;
  const vuIndex = exec.vu.idInTest;

  const client = new SignalRClient(env, { onClose: () => {} });
  let currentQ = null;
  let answeredQ = null;

  client.on('QuestionStarted', (q) => {
    const qid = q && (q.questionId || q.QuestionId);
    if (qid) currentQ = String(qid).toLowerCase();
  });

  try {
    await client.start();
    const res = await client.invoke('JoinGame', pin, uniqueNickname('s'));
    if (!res || res.success !== true) {
      const code = res && res.error ? res.error.code : 'no-response';
      recordJoinFailure(code, `ramp:join:${code}`);
      client.close();
      return;
    }
    playersJoined.add(1);
    noUnexpected();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('ramp:connect');
    client.close();
    return;
  }

  // Keep client alive and respond to questions throughout the game
  const maxHoldMs = (durationSeconds(JOIN_RAMP) + Q_COUNT * (Q_LEN + 10) + 30) * 1000;
  const deadline = Date.now() + maxHoldMs;

  while (Date.now() < deadline && !client.closed) {
    if (currentQ && currentQ !== answeredQ) {
      answeredQ = currentQ;
      const qObj = questions.find((x) => String(x.questionId).toLowerCase() === currentQ);
      const qIndex = qObj ? qObj.orderIndex : 0;
      const activeLevel = LEVELS[Math.min(qIndex, LEVELS.length - 1)];

      // Players up to activeLevel submit answers for this question
      if (vuIndex <= activeLevel + 1) {
        const choiceId = qObj ? qObj.correctChoiceId : null;
        if (choiceId) {
          const t0 = Date.now();
          const tags = { load: String(activeLevel) };
          try {
            const ack = await client.invoke('SubmitAnswer', currentQ, choiceId);
            answerSubmissionDuration.add(Date.now() - t0, tags);
            answersSubmitted.add(1, tags);
            if (ack && ack.success === true && ack.data && ack.data.accepted === true) {
              answersAccepted.add(1, tags);
              noUnexpected(tags);
            } else if (ack && ack.success === false && ack.error) {
              answersRejected.add(1, { code: ack.error.code, ...tags });
            } else {
              unexpectedAnswerFailures.add(1, tags);
              bumpUnexpected('ramp:submit:malformed', tags);
            }
          } catch (e) {
            unexpectedAnswerFailures.add(1, tags);
            bumpUnexpected('ramp:submit:exception', tags);
          }
        }
      }
    }
    await delay(100);
  }

  try {
    client.close();
  } catch (_) {}
}

export async function director(data) {
  const { env, hostToken, gameId, questions } = data;

  // 1. Wait for cohort to assemble in the lobby (WaitingForPlayers state)
  console.log(`[ramp] waiting for players to join lobby (target: ${PEAK})...`);
  const pre = await waitForParticipantCount(env, hostToken, gameId, PEAK, {
    timeoutMs: (durationSeconds(JOIN_RAMP) + 40) * 1000,
    minFraction: 0.85,
    intervalMs: 1500,
  });
  const present = pre && pre.participants ? pre.participants.length : 0;
  console.log(`[ramp] lobby ready with ${present} players. Starting questions...`);

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
