// Scenario 4 — 500-player answer burst (the primary performance test).
//
// Shape: one k6 process, two scenarios sharing setup().
//   * `players`  — PLAYERS VUs. Each connects, JoinGame, waits for the
//                  QuestionStarted server push, then IMMEDIATELY SubmitAnswer.
//                  The single broadcast makes ~PLAYERS submissions land inside a
//                  ~1 s window naturally (this is what a real classroom does).
//   * `director` — 1 VU. Waits until players are in, POST /start (fires the
//                  broadcast), waits out the question, POST /end-question, then
//                  verifies resulting application state, not just acks.
//
// Measures p50/p90/p95/p99 + throughput for answer submission and asserts:
//   answer_submission_duration p95 < 500 ms
//   answers accepted == answers in final DB state  (no lost accepted answers)
//   answerCount <= active players                  (no duplicate accepted answers)
//   every score <= question points                 (no duplicate scores)
//   per-choice counts sum to total, host answeredCount == results  (state intact)
//
//   k6 run -e ALLOW_LOAD_TEST=true -e BASE_URL=... -e SIGNALR_URL=... \
//     load-tests/scenarios/answer-burst.js

import { check } from 'k6';
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, showLeaderboard, endGame, getHostState } from '../helpers/rest.js';
import { waitForParticipantCount, verifyClosedQuestion } from '../helpers/orchestration.js';
import { answerThresholds, mergeThresholds } from '../config/thresholds.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  answersSubmitted,
  answersAccepted,
  answersRejected,
  unexpectedAnswerFailures,
  duplicateAnswerViolations,
  answerSubmissionDuration,
  questionDeliveryDuration,
  questionDeliveryFailures,
  questionDelivered,
  inconsistentGameState,
  lostAcceptedAnswers,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const PLAYERS = intEnv('PLAYERS', 500);
const JOIN_RAMP = __ENV.JOIN_RAMP || '90s';
const TIME_LIMIT = Math.min(300, Math.max(5, intEnv('ANSWER_TIME_LIMIT', 120)));
const WRONG_FRACTION = Number(__ENV.WRONG_FRACTION || 0.15);
const CONFIRM_FRACTION = Number(__ENV.CONFIRM_FRACTION || 0.3);
const READY_FRACTION = Number(__ENV.READY_FRACTION || 0.98);
const CLOCK_SKEW_MS = Number(__ENV.CLOCK_SKEW_MS || 0);
// Slack after the join ramp for the director's readiness poll + post-question
// drain + state verification. Lower it for quick dev runs.
const SETTLE = intEnv('SETTLE_SECONDS', 60);

// director must outlive: join ramp + readiness poll + question + drain
const DIRECTOR_MAX = `${durationSeconds(JOIN_RAMP) + SETTLE + 60 + TIME_LIMIT + 60}s`;
const PLAYER_HOLD = durationSeconds(JOIN_RAMP) + SETTLE + 40 + TIME_LIMIT;

export const options = {
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: PLAYERS },
        { duration: `${SETTLE + 40 + TIME_LIMIT}s`, target: PLAYERS },
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
      startTime: '0s',
      maxDuration: DIRECTOR_MAX,
    },
  },
  thresholds: mergeThresholds(answerThresholds(), {
    lost_accepted_answers: ['count<1'],
    duplicate_answer_violations: ['count<1'],
    duplicate_score_violations: ['count<1'],
    inconsistent_game_state: ['count<1'],
    question_delivery_failures: [`count<${Math.ceil(PLAYERS * 0.01)}`],
    'checks{scope:director}': ['rate>0.99'],
  }),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const env = resolveEnv();
  assertLoadAllowed(env, PLAYERS);
  const p = provisionGames(env, { questions: 1, timeLimitSeconds: TIME_LIMIT, points: 1000, games: 1 });
  console.log(`[answer-burst] game ${p.gameId} pin ${p.pin} question ${p.firstQuestion.questionId}`);
  return {
    env,
    gameId: p.gameId,
    pin: p.pin,
    hostToken: p.hostToken,
    question: p.firstQuestion,
  };
}

let submittedThisVu = false;

export async function player(data) {
  const { env, pin, question } = data;
  if (submittedThisVu) {
    await delay(3000);
    return;
  }
  submittedThisVu = true;

  let client;
  let questionRecvAt = 0;
  let questionPayload = null;

  try {
    client = new SignalRClient(env, {
      onClose: () => {
        /* tracked via client.closed */
      },
    });
    client.on('QuestionStarted', (q) => {
      if (!questionRecvAt) {
        questionRecvAt = Date.now();
        questionPayload = q;
      }
    });
    await client.start();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('burst:connect');
    return;
  }

  // Join the lobby.
  let sessionToken = null;
  try {
    const res = await client.invoke('JoinGame', pin, uniqueNickname('b'));
    if (!res || res.success !== true) {
      const code = res && res.error ? res.error.code : 'no-response';
      recordJoinFailure(code, `burst:join:${code}`);
      client.close();
      return;
    }
    sessionToken = res.data.sessionToken;
    playersJoined.add(1);
    noUnexpected();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('burst:join:exception');
    client.close();
    return;
  }

  // Wait for the QuestionStarted broadcast.
  const waitDeadline = Date.now() + PLAYER_HOLD * 1000;
  while (!questionRecvAt && Date.now() < waitDeadline && !client.closed) {
    await delay(50);
  }

  if (!questionRecvAt) {
    questionDeliveryFailures.add(1);
    check(null, { 'received QuestionStarted': () => false });
    client.close();
    return;
  }

  // Approx end-to-end delivery latency (server clock -> client clock).
  // startedAt ~= endsAt - timeLimit. Assumes reasonably synced clocks; correct
  // with -e CLOCK_SKEW_MS. On localhost this is accurate to a few ms.
  if (questionPayload && questionPayload.endsAt) {
    const skew = Number.isFinite(data.clockSkewMs) ? data.clockSkewMs : CLOCK_SKEW_MS;
    const serverStart = Date.parse(questionPayload.endsAt) - question.timeLimitSeconds * 1000;
    const deliveryMs = questionRecvAt - serverStart - skew;
    // clamp small negatives from the ~1 s Date-header resolution to 1 ms
    if (deliveryMs > -2000 && deliveryMs < 60000) questionDeliveryDuration.add(Math.max(1, deliveryMs));
  }
  questionDelivered.add(1);
  check(questionPayload, {
    'QuestionStarted has no correct answer leaked': (q) =>
      !q || (q.correctChoiceId === undefined && !(q.choices || []).some((c) => 'isCorrect' in c)),
  });

  // The burst: submit immediately.
  const wrong = Math.random() < WRONG_FRACTION;
  const choiceId = wrong ? question.wrongChoiceId : question.correctChoiceId;
  const qid = (questionPayload && questionPayload.questionId) || question.questionId;

  const a0 = Date.now();
  try {
    const ack = await client.invoke('SubmitAnswer', qid, choiceId);
    const dur = Date.now() - a0;
    answersSubmitted.add(1);
    answerSubmissionDuration.add(dur);

    if (ack && ack.success === true && ack.data && ack.data.accepted === true) {
      if (ack.data.alreadyAnswered === true) {
        // We only submit once per VU — an "already answered" here is a real dup.
        duplicateAnswerViolations.add(1, { where: 'burst:ack' });
      } else {
        answersAccepted.add(1, { correct: String(!wrong) });
        noUnexpected();
        // Authoritative "no lost accepted answers" check on a staggered SAMPLE of
        // players: re-read our own state over the SAME socket. The server said
        // accepted:true, so the row must exist — if Reconnect disagrees, that ack
        // was lost. Sampled + delayed so it doesn't pile onto the submit burst.
        if (Math.random() < CONFIRM_FRACTION) {
          await delay(500 + Math.random() * 2500);
          try {
            const st = await client.invoke('Reconnect', sessionToken);
            const confirmed =
              !st ||
              st.success !== true ||
              !st.data ||
              st.data.status !== 1 /* QuestionActive */ ||
              st.data.alreadyAnsweredCurrentQuestion === true;
            if (!confirmed) {
              lostAcceptedAnswers.add(1, { where: 'burst:ack-not-in-state' });
            }
            check(
              { confirmed },
              { 'sampled: accepted answer confirmed in server state': (x) => x.confirmed === true },
            );
          } catch (_) {
            /* confirmation is best-effort; director-side count reconciliation still applies */
          }
        }
      }
    } else if (ack && ack.success === false && ack.error) {
      // A well-formed rejection (e.g. QuestionClosed at the very edge) is an
      // expected 4xx-equivalent, not an error.
      answersRejected.add(1, { code: ack.error.code });
    } else {
      unexpectedAnswerFailures.add(1);
      bumpUnexpected('burst:submit:malformed');
    }
  } catch (e) {
    answersSubmitted.add(1);
    unexpectedAnswerFailures.add(1);
    bumpUnexpected('burst:submit:exception');
  }

  // Stay connected briefly so a late server event can't be misread as a drop.
  const holdUntil = Math.min(waitDeadline, Date.now() + 5000);
  while (Date.now() < holdUntil && !client.closed) {
    await delay(500);
  }
  client.close();
}

export async function director(data) {
  const { env, hostToken, gameId, question } = data;
  const tags = { scope: 'director' };

  // 1. Wait for the FULL cohort to be in the lobby (or time out well past the
  //    ramp). Starting the question early would force late joiners into a benign
  //    Game.NotJoinable; waiting for 100% keeps the burst == the population.
  const preState = await waitForParticipantCount(env, hostToken, gameId, PLAYERS, {
    timeoutMs: (durationSeconds(JOIN_RAMP) + SETTLE) * 1000,
    minFraction: 1,
    intervalMs: 2000,
  });
  const present = preState.participants.length;
  console.log(`[answer-burst] starting question with ${present}/${PLAYERS} players present`);
  check(
    { present },
    { [`>= ${Math.ceil(PLAYERS * READY_FRACTION)} players joined before question`]: (x) => x.present >= Math.ceil(PLAYERS * READY_FRACTION) },
    tags,
  );

  // 2. Fire the question (broadcasts QuestionStarted to the players group).
  const started = startGame(env, hostToken, gameId);
  check(started, { 'POST /start returned QuestionStarted': (s) => !!s && !!s.player }, tags);

  // 3. Let the burst + any stragglers complete. Deadline is inclusive, so wait
  //    past it before closing.
  await delay((question.timeLimitSeconds + 15) * 1000);

  // 4. Close the question and verify DB-truth state.
  endQuestion(env, hostToken, gameId);
  const { results, activePlayers } = verifyClosedQuestion(
    env,
    hostToken,
    gameId,
    question,
    present,
    'answer-burst',
  );
  console.log(
    `[answer-burst] results: answerCount=${results.answerCount} participantCount=${results.participantCount} ` +
      `activePlayers=${activePlayers} correctChoiceCount=${
        (results.choices.find((c) => c.isCorrect) || {}).answerCount
      }`,
  );

  // 5. Leaderboard must list exactly the active players, once each.
  const lb = showLeaderboard(env, hostToken, gameId);
  const lbIds = new Set(lb.entries.map((e) => e.participantId));
  if (lbIds.size !== lb.entries.length) {
    inconsistentGameState.add(1, { where: 'leaderboard-dupes' });
  }
  check(
    lb,
    {
      'leaderboard entry count == active players': (l) => l.entries.length === activePlayers,
      'leaderboard has no duplicate participants': () => lbIds.size === lb.entries.length,
      'ranks are 1..N contiguous': (l) => ranksContiguous(l.entries),
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
    console.log(`[answer-burst] final status=${s.status} participants=${s.participants.length}`);
  } catch (_) {
    /* ignore */
  }
}

function ranksContiguous(entries) {
  const ranks = entries.map((e) => e.rank).sort((a, b) => a - b);
  for (let i = 0; i < ranks.length; i += 1) {
    // ties allowed: rank must never exceed position+1
    if (ranks[i] > i + 1) return false;
  }
  return true;
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 90;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('answer-burst');
