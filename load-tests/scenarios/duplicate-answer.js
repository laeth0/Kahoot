// Scenario 5 — Duplicate-answer concurrency / idempotency.
//
// For each of DUP_PLAYERS players we open TWO hub connections bound to the SAME
// participant (JoinGame on primaryConnection, Reconnect(sessionToken) on reconnectedConnection) and fire several
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
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
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
const TOTAL_HOLD_SECONDS = parseDurationSeconds(JOIN_RAMP) + 90 + TIME_LIMIT;
const TOTAL_SCENARIO_DURATION_MS = (parseDurationSeconds(JOIN_RAMP) + 90 + TIME_LIMIT + 10) * 1000;

export const options = {
  hosts: hostsOverride(),
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
      maxDuration: `${TOTAL_HOLD_SECONDS + 60}s`,
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
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, DUP_PLAYERS);
  const provisionedGame = provisionGames(environmentConfig, {
    questions: 1,
    timeLimitSeconds: TIME_LIMIT,
    points: 1000,
    games: 1,
  });
  return {
    env: environmentConfig,
    gameId: provisionedGame.gameId,
    pin: provisionedGame.pin,
    hostToken: provisionedGame.hostToken,
    question: provisionedGame.firstQuestion,
  };
}

export async function player(data) {
  if (exec.vu.iterationInScenario > 0) {
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  const { env, pin, question } = data;

  let primaryConnection;
  let reconnectedConnection;
  let questionReceivedTimestampMs = 0;
  let targetQuestionId = question.questionId;

  try {
    primaryConnection = new SignalRClient(env);
    primaryConnection.on('QuestionStarted', (questionEvent) => {
      if (!questionReceivedTimestampMs) {
        questionReceivedTimestampMs = Date.now();
        if (questionEvent && questionEvent.questionId) targetQuestionId = questionEvent.questionId;
      }
    });
    await primaryConnection.start();
    const joinResult = await primaryConnection.invoke('JoinGame', pin, uniqueNickname('d'));
    if (!joinResult || joinResult.success !== true) {
      const errorCode = joinResult && joinResult.error ? joinResult.error.code : 'no-response';
      recordJoinFailure(errorCode, 'dup:join');
      primaryConnection.close();
      const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
      await delay(remainingTimeMs);
      return;
    }
    const sessionToken = joinResult.data.sessionToken;
    const participantId = joinResult.data.participantId;
    playersJoined.add(1);
    noUnexpected();

    // Second connection for the same participant.
    reconnectedConnection = new SignalRClient(env);
    await reconnectedConnection.start();
    const reconnectResult = await reconnectedConnection.invoke('Reconnect', sessionToken);
    check(reconnectResult, {
      'second connection reconnected same participant': (res) =>
        !!res && res.success === true && res.data && res.data.participantId === participantId,
    });
  } catch (setupException) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('dup:setup:exception');
    if (primaryConnection) primaryConnection.close();
    if (reconnectedConnection) reconnectedConnection.close();
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  // Wait for the question broadcast.
  while (!questionReceivedTimestampMs && exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS && !primaryConnection.closed) {
    await delay(50);
  }
  if (!questionReceivedTimestampMs) {
    check(null, { 'received QuestionStarted': () => false });
    primaryConnection.close();
    if (reconnectedConnection) reconnectedConnection.close();
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  // The near-simultaneous duplicate burst across BOTH sockets.
  const choiceId = question.correctChoiceId;
  const submissionPromises = [];
  const clientConnections = [primaryConnection, reconnectedConnection];
  for (const connection of clientConnections) {
    for (let attemptIndex = 0; attemptIndex < ATTEMPTS_PER_CONN; attemptIndex += 1) {
      const submissionStartTimeMs = Date.now();
      submissionPromises.push(
        connection
          .invoke('SubmitAnswer', targetQuestionId, choiceId)
          .then((acknowledgment) => {
            answerSubmissionDuration.add(Date.now() - submissionStartTimeMs);
            return acknowledgment;
          })
          .catch((err) => ({ __throw: String(err) })),
      );
    }
  }
  const submissionAcknowledgments = await Promise.all(submissionPromises);
  answersSubmitted.add(submissionAcknowledgments.length);

  let freshAcceptCount = 0;
  let idempotentAcceptCount = 0;
  let unexpectedFailureCount = 0;
  for (const acknowledgment of submissionAcknowledgments) {
    if (acknowledgment && acknowledgment.__throw) {
      unexpectedFailureCount += 1;
      continue;
    }
    if (acknowledgment && acknowledgment.success === true && acknowledgment.data && acknowledgment.data.accepted === true) {
      if (acknowledgment.data.alreadyAnswered === true) idempotentAcceptCount += 1;
      else freshAcceptCount += 1;
    } else if (acknowledgment && acknowledgment.success === false && acknowledgment.error) {
      unexpectedFailureCount += 1;
    } else {
      unexpectedFailureCount += 1;
    }
  }

  answersAccepted.add(freshAcceptCount + idempotentAcceptCount);
  if (freshAcceptCount !== 1) {
    // 0 => the one accepted answer was lost; >1 => the unique constraint failed
    duplicateAnswerViolations.add(Math.abs(freshAcceptCount - 1), {
      where: 'dup:acks',
      freshAcceptCount: String(freshAcceptCount),
    });
  }
  if (unexpectedFailureCount > 0) {
    unexpectedAnswerFailures.add(unexpectedFailureCount);
    bumpUnexpected('dup:submit:unexpected');
  }

  check(
    { freshAcceptCount, idempotentAcceptCount, unexpectedFailureCount, totalSubmissions: submissionAcknowledgments.length },
    {
      'exactly one fresh accept per player': (stats) => stats.freshAcceptCount === 1,
      'all other duplicates were idempotent': (stats) => stats.idempotentAcceptCount === stats.totalSubmissions - 1,
      'no unexpected submission failures': (stats) => stats.unexpectedFailureCount === 0,
    },
  );

  while (exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS && !primaryConnection.closed) {
    await delay(1000);
  }
  primaryConnection.close();
  if (reconnectedConnection) reconnectedConnection.close();
}

export async function director(data) {
  const { env, hostToken, gameId, question } = data;
  const directorTags = { scope: 'director' };

  const lobbyState = await waitForParticipantCount(env, hostToken, gameId, DUP_PLAYERS, {
    timeoutMs: (parseDurationSeconds(JOIN_RAMP) + 75) * 1000,
    minFraction: 0.95,
    intervalMs: 1500,
  });
  const presentParticipantCount = lobbyState.participants.length;
  console.log(`[duplicate-answer] starting with ${presentParticipantCount}/${DUP_PLAYERS} players`);

  startGame(env, hostToken, gameId);
  await delay((question.timeLimitSeconds + 12) * 1000);
  endQuestion(env, hostToken, gameId);

  const { results } = verifyClosedQuestion(env, hostToken, gameId, question, presentParticipantCount, 'duplicate-answer');
  console.log(`[duplicate-answer] answerCount=${results.answerCount} (expected ${presentParticipantCount}, one row per player)`);
  check(
    results,
    {
      'exactly one accepted answer row per player': (questionResults) => questionResults.answerCount === presentParticipantCount,
    },
    directorTags,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 45;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('duplicate-answer');
