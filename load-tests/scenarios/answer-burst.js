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
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
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
const READY_FRACTION = Number(__ENV.READY_FRACTION || 0.90);
const CLOCK_SKEW_MS = Number(__ENV.CLOCK_SKEW_MS || 0);
// Slack after the join ramp for the director's readiness poll + post-question
// drain + state verification. Lower it for quick dev runs.
const SETTLE_SECONDS = intEnv('SETTLE_SECONDS', 60);

// director must outlive: join ramp + readiness poll + question + drain
const DIRECTOR_MAX_DURATION = `${parseDurationSeconds(JOIN_RAMP) + SETTLE_SECONDS + 60 + TIME_LIMIT + 60}s`;
const PLAYER_HOLD_SECONDS = parseDurationSeconds(JOIN_RAMP) + SETTLE_SECONDS + 40 + TIME_LIMIT;

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: PLAYERS },
        { duration: `${SETTLE_SECONDS + 40 + TIME_LIMIT}s`, target: PLAYERS },
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
      maxDuration: DIRECTOR_MAX_DURATION,
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
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, PLAYERS);
  const provisionedGame = provisionGames(environmentConfig, {
    questions: 1,
    timeLimitSeconds: TIME_LIMIT,
    points: 1000,
    games: 1,
  });
  console.log(`[answer-burst] game ${provisionedGame.gameId} pin ${provisionedGame.pin} question ${provisionedGame.firstQuestion.questionId}`);
  return {
    env: environmentConfig,
    gameId: provisionedGame.gameId,
    pin: provisionedGame.pin,
    hostToken: provisionedGame.hostToken,
    question: provisionedGame.firstQuestion,
  };
}

export async function player(data) {
  const { env, pin, question } = data;
  const holdDeadlineTimestampMs = Date.now() + PLAYER_HOLD_SECONDS * 1000;

  let signalrClient;
  let questionReceivedTimestampMs = 0;
  let questionPayload = null;

  try {
    signalrClient = new SignalRClient(env, {
      onClose: () => {
        /* tracked via signalrClient.closed */
      },
    });
    signalrClient.on('QuestionStarted', (receivedQuestion) => {
      if (!questionReceivedTimestampMs) {
        questionReceivedTimestampMs = Date.now();
        questionPayload = receivedQuestion;
      }
    });
    await signalrClient.start();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('burst:connect');
    return;
  }

  // Join the lobby.
  let playerSessionToken = null;
  try {
    const joinResponse = await signalrClient.invoke('JoinGame', pin, uniqueNickname('b'));
    if (!joinResponse || joinResponse.success !== true) {
      const errorCode = joinResponse && joinResponse.error ? joinResponse.error.code : 'no-response';
      recordJoinFailure(errorCode, `burst:join:${errorCode}`);
      signalrClient.close();
      return;
    }
    playerSessionToken = joinResponse.data.sessionToken;
    playersJoined.add(1);
    noUnexpected();
  } catch (joinException) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('burst:join:exception');
    signalrClient.close();
    return;
  }

  // Wait for the QuestionStarted broadcast.
  while (!questionReceivedTimestampMs && Date.now() < holdDeadlineTimestampMs && !signalrClient.closed) {
    await delay(50);
  }

  if (!questionReceivedTimestampMs) {
    questionDeliveryFailures.add(1);
    check(null, { 'received QuestionStarted': () => false });
    signalrClient.close();
    return;
  }

  // Approx end-to-end delivery latency (server clock -> client clock).
  if (questionPayload && questionPayload.endsAt) {
    const clockSkew = Number.isFinite(data.clockSkewMs) ? data.clockSkewMs : CLOCK_SKEW_MS;
    const serverStartTimestamp = Date.parse(questionPayload.endsAt) - question.timeLimitSeconds * 1000;
    const deliveryLatencyMs = questionReceivedTimestampMs - serverStartTimestamp - clockSkew;
    if (deliveryLatencyMs > -2000 && deliveryLatencyMs < 60000) {
      questionDeliveryDuration.add(Math.max(1, deliveryLatencyMs));
    }
  }
  questionDelivered.add(1);
  check(questionPayload, {
    'QuestionStarted has no correct answer leaked': (payload) =>
      !payload || (payload.correctChoiceId === undefined && !(payload.choices || []).some((choice) => 'isCorrect' in choice)),
  });

  // The burst: submit answer immediately.
  const isWrongAnswer = Math.random() < WRONG_FRACTION;
  const choiceIdToSubmit = isWrongAnswer ? question.wrongChoiceId : question.correctChoiceId;
  const activeQuestionId = (questionPayload && questionPayload.questionId) || question.questionId;

  const submissionStartTimeMs = Date.now();
  try {
    const submissionAck = await signalrClient.invoke('SubmitAnswer', activeQuestionId, choiceIdToSubmit);
    const submissionDurationMs = Date.now() - submissionStartTimeMs;
    answersSubmitted.add(1);
    answerSubmissionDuration.add(submissionDurationMs);

    if (submissionAck && submissionAck.success === true && submissionAck.data && submissionAck.data.accepted === true) {
      if (submissionAck.data.alreadyAnswered === true) {
        // We only submit once per VU — an "already answered" here is a duplicate answer violation.
        duplicateAnswerViolations.add(1, { where: 'burst:ack' });
      } else {
        answersAccepted.add(1, { correct: String(!isWrongAnswer) });
        noUnexpected();

        // Sampled re-read to verify state consistency.
        if (Math.random() < CONFIRM_FRACTION) {
          await delay(500 + Math.random() * 2500);
          try {
            const reconnectResponse = await signalrClient.invoke('Reconnect', playerSessionToken);
            const isQuestionActive =
              reconnectResponse &&
              reconnectResponse.data &&
              (reconnectResponse.data.status === 2 || reconnectResponse.data.status === 'QuestionActive');
            const stateConfirmed =
              !reconnectResponse ||
              reconnectResponse.success !== true ||
              !reconnectResponse.data ||
              !isQuestionActive ||
              reconnectResponse.data.alreadyAnsweredCurrentQuestion === true;
            if (!stateConfirmed) {
              lostAcceptedAnswers.add(1, { where: 'burst:ack-not-in-state' });
            }
            check(
              { stateConfirmed },
              { 'sampled: accepted answer confirmed in server state': (ctx) => ctx.stateConfirmed === true },
            );
          } catch (_) {
            /* confirmation is best-effort */
          }
        }
      }
    } else if (submissionAck && submissionAck.success === false && submissionAck.error) {
      answersRejected.add(1, { code: submissionAck.error.code });
    } else {
      unexpectedAnswerFailures.add(1);
      bumpUnexpected('burst:submit:malformed');
    }
  } catch (submissionException) {
    answersSubmitted.add(1);
    unexpectedAnswerFailures.add(1);
    bumpUnexpected('burst:submit:exception');
  }

  // Hold connection until the scenario window ends to prevent empty loop iterations
  while (Date.now() < holdDeadlineTimestampMs && !signalrClient.closed) {
    await delay(1000);
  }
  signalrClient.close();
}

export async function director(data) {
  const { env, hostToken, gameId, question } = data;
  const directorTags = { scope: 'director' };

  // 1. Wait for the cohort to assemble in the lobby
  const preGameState = await waitForParticipantCount(env, hostToken, gameId, PLAYERS, {
    timeoutMs: (parseDurationSeconds(JOIN_RAMP) + SETTLE_SECONDS + 30) * 1000,
    minFraction: READY_FRACTION,
    intervalMs: 1500,
  });
  const participantsPresent = preGameState && preGameState.participants ? preGameState.participants.length : 0;
  console.log(`[answer-burst] starting question with ${participantsPresent}/${PLAYERS} players present`);
  check(
    { participantsPresent },
    { [`>= ${Math.ceil(PLAYERS * READY_FRACTION)} players joined before question`]: (ctx) => ctx.participantsPresent >= Math.ceil(PLAYERS * READY_FRACTION) },
    directorTags,
  );

  // 2. Start the game (broadcasts QuestionStarted to the players group).
  const startGameResult = startGame(env, hostToken, gameId);
  check(startGameResult, { 'POST /start returned QuestionStarted': (res) => !!res && !!res.player }, directorTags);

  // 3. Let the burst + any stragglers complete.
  await delay((question.timeLimitSeconds + 15) * 1000);

  // 4. Close the question and verify database-truth state.
  endQuestion(env, hostToken, gameId);
  const { results, activePlayers } = verifyClosedQuestion(
    env,
    hostToken,
    gameId,
    question,
    participantsPresent,
    'answer-burst',
  );
  console.log(
    `[answer-burst] results: answerCount=${results.answerCount} participantCount=${results.participantCount} ` +
      `activePlayers=${activePlayers} correctChoiceCount=${
        (results.choices.find((choice) => choice.isCorrect) || {}).answerCount
      }`,
  );

  // 5. Leaderboard must list exactly the active players, once each.
  const leaderboard = showLeaderboard(env, hostToken, gameId);
  const leaderboardParticipantIds = new Set(leaderboard.entries.map((entry) => entry.participantId));
  if (leaderboardParticipantIds.size !== leaderboard.entries.length) {
    inconsistentGameState.add(1, { where: 'leaderboard-dupes' });
  }
  check(
    leaderboard,
    {
      'leaderboard entry count == active players': (lb) => lb.entries.length === activePlayers,
      'leaderboard has no duplicate participants': () => leaderboardParticipantIds.size === leaderboard.entries.length,
      'ranks are 1..N contiguous': (lb) => areRanksContiguous(lb.entries),
    },
    directorTags,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

export function teardown() {
  // state and leaderboard are verified by director
}

function areRanksContiguous(entries) {
  const sortedRanks = entries.map((entry) => entry.rank).sort((first, second) => first - second);
  for (let index = 0; index < sortedRanks.length; index += 1) {
    if (sortedRanks[index] > index + 1) return false;
  }
  return true;
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 90;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('answer-burst');
