// Scenario 5 — Duplicate-answer concurrency / idempotency.
//
// For each of DUP_PLAYERS players we open TWO hub connections bound to the SAME
// participant (JoinGame on C1, Reconnect(sessionToken) on C2) and fire several
// near-simultaneous SubmitAnswer calls for the same (gameId, questionId,
// participantId) across both sockets. Two independent INSERTs then race for the
// DB unique index `uq_answer_participant_question` — this exercises the
// database/application concurrency protection, not just a UI guard.
//
// Asserts:
//   * exactly ONE submission per player is a fresh accept (alreadyAnswered:false)
//   * every other submission returns the idempotent {accepted:true, alreadyAnswered:true}
//   * no submission errors unexpectedly (we stay under the 5-per-3s hub guard)
//   * results.answerCount == DUP_PLAYERS  (one row per player, never N x attempts)
//   * every score <= question points       (score applied exactly once)
//
//   k6 run -e ALLOW_LOAD_TEST=true load-tests/scenarios/duplicate-answer.js

import { check } from 'k6';
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, endGame } from '../helpers/rest.js';
import { waitForParticipantCount, verifyClosedQuestion } from '../helpers/orchestration.js';
import { correctnessThresholds, mergeThresholds } from '../config/thresholds.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  answersSubmitted,
  answersAccepted,
  duplicateAnswerViolations,
  answerSubmissionDuration,
  unexpectedAnswerFailures,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const DUP_PLAYERS = intEnv('DUP_PLAYERS', 100);
const ATTEMPTS_PER_CONN = Math.min(3, intEnv('DUP_ATTEMPTS_PER_CONN', 2)); // stay < 5 / 3 s guard
const TIME_LIMIT = Math.min(300, Math.max(15, intEnv('ANSWER_TIME_LIMIT', 60)));
const JOIN_RAMP = __ENV.JOIN_RAMP || '45s';
const HOLD = durationSeconds(JOIN_RAMP) + 90 + TIME_LIMIT;

export const options = {
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: DUP_PLAYERS },
        { duration: `${90 + TIME_LIMIT}s`, target: DUP_PLAYERS },
        { duration: '10s', target: 0 },
      ],
      gracefulRampDown: '20s',
      gracefulStop: '45s',
    },
    director: {
      executor: 'per-vu-iterations',
      exec: 'director',
      vus: 1,
      iterations: 1,
      maxDuration: `${HOLD + 60}s`,
    },
  },
  thresholds: mergeThresholds(correctnessThresholds(), {
    duplicate_answer_violations: ['count<1'],
    unexpected_answer_failures: ['count<1'],
    'checks{scope:director}': ['rate>0.99'],
    answer_submission_duration: ['p(95)<1000'],
  }),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, DUP_PLAYERS);
  const p = provisionGames(env, { questions: 1, timeLimitSeconds: TIME_LIMIT, points: 1000, games: 1 });
  return { env, gameId: p.gameId, pin: p.pin, hostToken: p.hostToken, question: p.firstQuestion };
}

let done = false;

export async function player(data) {
  const { env, pin, question } = data;
  if (done) {
    await delay(3000);
    return;
  }
  done = true;

  let c1;
  let c2;
  let recvAt = 0;
  let qid = question.questionId;

  try {
    c1 = new SignalRClient(env);
    c1.on('QuestionStarted', (q) => {
      if (!recvAt) {
        recvAt = Date.now();
        if (q && q.questionId) qid = q.questionId;
      }
    });
    await c1.start();
    const joinRes = await c1.invoke('JoinGame', pin, uniqueNickname('d'));
    if (!joinRes || joinRes.success !== true) {
      recordJoinFailure(joinRes && joinRes.error ? joinRes.error.code : 'no-response', 'dup:join');
      c1.close();
      return;
    }
    const sessionToken = joinRes.data.sessionToken;
    const participantId = joinRes.data.participantId;
    playersJoined.add(1);
    noUnexpected();

    // Second connection for the same participant.
    c2 = new SignalRClient(env);
    await c2.start();
    const rc = await c2.invoke('Reconnect', sessionToken);
    check(rc, {
      'second connection reconnected same participant': (r) =>
        !!r && r.success === true && r.data && r.data.participantId === participantId,
    });
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('dup:setup:exception');
    if (c1) c1.close();
    if (c2) c2.close();
    return;
  }

  // Wait for the question.
  const deadline = Date.now() + HOLD * 1000;
  while (!recvAt && Date.now() < deadline && !c1.closed) {
    await delay(50);
  }
  if (!recvAt) {
    check(null, { 'received QuestionStarted': () => false });
    c1.close();
    c2.close();
    return;
  }

  // The near-simultaneous duplicate burst across BOTH sockets.
  const choiceId = question.correctChoiceId;
  const calls = [];
  const conns = [c1, c2];
  for (const conn of conns) {
    for (let i = 0; i < ATTEMPTS_PER_CONN; i += 1) {
      const t0 = Date.now();
      calls.push(
        conn
          .invoke('SubmitAnswer', qid, choiceId)
          .then((ack) => {
            answerSubmissionDuration.add(Date.now() - t0);
            return ack;
          })
          .catch((e) => ({ __throw: String(e) })),
      );
    }
  }
  const acks = await Promise.all(calls);
  answersSubmitted.add(acks.length);

  let freshAccepts = 0;
  let idempotent = 0;
  let unexpected = 0;
  for (const ack of acks) {
    if (ack && ack.__throw) {
      unexpected += 1;
      continue;
    }
    if (ack && ack.success === true && ack.data && ack.data.accepted === true) {
      if (ack.data.alreadyAnswered === true) idempotent += 1;
      else freshAccepts += 1;
    } else if (ack && ack.success === false && ack.error) {
      // TooManyAnswerAttempts would mean our pacing exceeded the guard — count
      // it as unexpected for this scenario's purposes.
      unexpected += 1;
    } else {
      unexpected += 1;
    }
  }

  answersAccepted.add(freshAccepts + idempotent);
  if (freshAccepts !== 1) {
    // 0 => the one accepted answer was lost; >1 => the unique constraint failed
    duplicateAnswerViolations.add(Math.abs(freshAccepts - 1), { where: 'dup:acks', freshAccepts: String(freshAccepts) });
  }
  if (unexpected > 0) {
    unexpectedAnswerFailures.add(unexpected);
    bumpUnexpected('dup:submit:unexpected');
  }

  check(
    { freshAccepts, idempotent, unexpected, total: acks.length },
    {
      'exactly one fresh accept per player': (x) => x.freshAccepts === 1,
      'all other duplicates were idempotent': (x) => x.idempotent === x.total - 1,
      'no unexpected submission failures': (x) => x.unexpected === 0,
    },
  );

  const lingerUntil = Math.min(deadline, Date.now() + 4000);
  while (Date.now() < lingerUntil && !c1.closed) await delay(500);
  c1.close();
  c2.close();
}

export async function director(data) {
  const { env, hostToken, gameId, question } = data;
  const tags = { scope: 'director' };

  const pre = await waitForParticipantCount(env, hostToken, gameId, DUP_PLAYERS, {
    timeoutMs: (durationSeconds(JOIN_RAMP) + 75) * 1000,
    minFraction: 1,
    intervalMs: 1500,
  });
  const present = pre.participants.length;
  console.log(`[duplicate-answer] starting with ${present}/${DUP_PLAYERS} players`);

  startGame(env, hostToken, gameId);
  await delay((question.timeLimitSeconds + 12) * 1000);
  endQuestion(env, hostToken, gameId);

  const { results } = verifyClosedQuestion(env, hostToken, gameId, question, present, 'duplicate-answer');
  console.log(`[duplicate-answer] answerCount=${results.answerCount} (expected ${present}, one row per player)`);
  check(results, {
    'exactly one accepted answer row per player': (r) => r.answerCount === present,
  }, tags);

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 45;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('duplicate-answer');
