// Scenario 6 — Reconnection preserves identity and state.
//
// PLAYERS players join and answer the current question. Then the first
// RECONNECT_PLAYERS of them drop their socket and reconnect using their existing
// session token. We assert per reconnecting player:
//   * Reconnect returns the SAME participantId          (identity restored)
//   * status + currentQuestion + questionEndsAt present (game state restored)
//   * alreadyAnsweredCurrentQuestion == true            (answered flag preserved)
//   * totalScore unchanged across repeated reconnects   (score preserved)
// and globally:
//   * host participant count stays == PLAYERS  (no second Participant row: not 500 -> 600)
//   * results.answerCount == PLAYERS           (no accepted answer lost in the churn)
//
//   k6 run -e ALLOW_LOAD_TEST=true -e PLAYERS=500 -e RECONNECT_PLAYERS=100 \
//     load-tests/scenarios/reconnection.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, endGame, getHostState } from '../helpers/rest.js';
import { waitForParticipantCount, verifyClosedQuestion } from '../helpers/orchestration.js';
import { reconnectionThresholds, mergeThresholds } from '../config/thresholds.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  answersSubmitted,
  answersAccepted,
  reconnectionDuration,
  reconnectionFailures,
  duplicateParticipants,
  inconsistentGameState,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const PLAYERS = intEnv('PLAYERS', 500);
const RECONNECT_PLAYERS = Math.min(PLAYERS, intEnv('RECONNECT_PLAYERS', 100));
const TIME_LIMIT = Math.min(300, Math.max(30, intEnv('ANSWER_TIME_LIMIT', 120)));
const JOIN_RAMP = __ENV.JOIN_RAMP || '90s';
const RECONNECT_AFTER_MS = intEnv('RECONNECT_AFTER_MS', 4000);
const HOLD = durationSeconds(JOIN_RAMP) + 120 + TIME_LIMIT;

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: PLAYERS },
        { duration: `${120 + TIME_LIMIT}s`, target: PLAYERS },
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
  thresholds: mergeThresholds(reconnectionThresholds(), {
    lost_accepted_answers: ['count<1'],
    duplicate_participants: ['count<1'],
    inconsistent_game_state: ['count<1'],
    'checks{scope:director}': ['rate>0.99'],
  }),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, PLAYERS);
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

  const isReconnector = exec.vu.idInTest <= RECONNECT_PLAYERS;
  let c1;
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
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('recon:connect');
    return;
  }

  let sessionToken;
  let participantId;
  try {
    const res = await c1.invoke('JoinGame', pin, uniqueNickname('r'));
    if (!res || res.success !== true) {
      recordJoinFailure(res && res.error ? res.error.code : 'no-response', 'recon:join');
      c1.close();
      return;
    }
    sessionToken = res.data.sessionToken;
    participantId = res.data.participantId;
    playersJoined.add(1);
    noUnexpected();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('recon:join:exception');
    c1.close();
    return;
  }

  // Wait for the question, then answer (correct).
  const deadline = Date.now() + HOLD * 1000;
  while (!recvAt && Date.now() < deadline && !c1.closed) await delay(50);
  if (recvAt) {
    try {
      const ack = await c1.invoke('SubmitAnswer', qid, question.correctChoiceId);
      answersSubmitted.add(1);
      if (ack && ack.success === true && ack.data && ack.data.accepted === true) {
        answersAccepted.add(1);
        noUnexpected();
      } else {
        bumpUnexpected('recon:submit');
      }
    } catch (e) {
      bumpUnexpected('recon:submit:exception');
    }
  }

  if (!isReconnector) {
    while (Date.now() < deadline && !c1.closed) await delay(1000);
    c1.close();
    return;
  }

  // --- Reconnection under test ---
  await delay(RECONNECT_AFTER_MS);
  c1.close();
  await delay(300 + Math.random() * 700); // brief outage

  let firstScore = null;
  for (let attempt = 1; attempt <= 2; attempt += 1) {
    const c2 = new SignalRClient(env);
    let rc;
    const t0 = Date.now();
    try {
      await c2.start();
      rc = await c2.invoke('Reconnect', sessionToken);
    } catch (e) {
      reconnectionFailures.add(1, { attempt: String(attempt), reason: 'exception' });
      bumpUnexpected('recon:reconnect:exception');
      try {
        c2.close();
      } catch (_) {
        /* ignore */
      }
      continue;
    }
    reconnectionDuration.add(Date.now() - t0);

    if (!rc || rc.success !== true || !rc.data) {
      reconnectionFailures.add(1, { attempt: String(attempt), reason: rc && rc.error ? rc.error.code : 'no-data' });
      bumpUnexpected('recon:reconnect:failed');
      c2.close();
      continue;
    }

    const d = rc.data;
    if (d.participantId !== participantId) {
      duplicateParticipants.add(1, { where: 'reconnect' });
      reconnectionFailures.add(1, { attempt: String(attempt), reason: 'identity-changed' });
    }
    if (attempt === 1) firstScore = d.totalScore;
    else if (d.totalScore !== firstScore) {
      inconsistentGameState.add(1, { where: 'reconnect:score-drift' });
    }

    check(d, {
      'reconnect restored same participant id': (x) => x.participantId === participantId,
      'reconnect restored game status': (x) =>
        (typeof x.status === 'string' && x.status.length > 0) ||
        (typeof x.status === 'number' && x.status >= 0 && x.status <= 5),
      'reconnect restored current question': (x) => !!x.currentQuestion && !!x.currentQuestion.endsAt,
      'reconnect preserved answered flag': (x) => x.alreadyAnsweredCurrentQuestion === true,
      'reconnect preserved a non-negative score': (x) => x.totalScore >= 0 && x.totalScore <= data.question.points,
      'reconnect current question has no correct answer': (x) =>
        !x.currentQuestion ||
        (x.currentQuestion.correctChoiceId === undefined &&
          !(x.currentQuestion.choices || []).some((c) => 'isCorrect' in c)),
    });

    const lingerUntil = Math.min(deadline, Date.now() + 2000);
    while (Date.now() < lingerUntil && !c2.closed) await delay(500);
    c2.close();
    await delay(400);
  }

  while (Date.now() < deadline) await delay(1000);
}

export async function director(data) {
  const { env, hostToken, gameId, question } = data;
  const tags = { scope: 'director' };

  const pre = await waitForParticipantCount(env, hostToken, gameId, PLAYERS, {
    timeoutMs: (durationSeconds(JOIN_RAMP) + 90) * 1000,
    minFraction: 0.95,
    intervalMs: 2000,
  });
  const present = pre.participants.length;
  console.log(`[reconnection] starting question with ${present}/${PLAYERS} players`);

  startGame(env, hostToken, gameId);

  // Give players time to answer AND to churn their connections.
  await delay((question.timeLimitSeconds + 20) * 1000);
  endQuestion(env, hostToken, gameId);

  const { results, state } = verifyClosedQuestion(env, hostToken, gameId, question, present, 'reconnection');
  console.log(
    `[reconnection] participants after churn: ${state.participants.length} (must be ${present}); ` +
      `answerCount=${results.answerCount}`,
  );
  check(
    state,
    {
      [`participant count unchanged (${present}, not ${present + RECONNECT_PLAYERS})`]: (s) =>
        s.participants.length === present,
    },
    tags,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

export function teardown(data) {
  try {
    const s = getHostState(data.env, data.hostToken, data.gameId);
    console.log(`[reconnection] final participants=${s.participants.length} status=${s.status}`);
  } catch (_) {
    /* ignore */
  }
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 90;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('reconnection');
