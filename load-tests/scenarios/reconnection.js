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
const TOTAL_HOLD_DURATION_SECONDS = parseDurationSeconds(JOIN_RAMP) + 120 + TIME_LIMIT;
const TOTAL_SCENARIO_DURATION_MS = (parseDurationSeconds(JOIN_RAMP) + 120 + TIME_LIMIT + 10) * 1000;

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
      maxDuration: `${TOTAL_HOLD_DURATION_SECONDS + 60}s`,
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
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, PLAYERS);
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
  const isReconnector = exec.vu.idInTest <= RECONNECT_PLAYERS;

  let initialConnection;
  let questionReceivedTimestampMs = 0;
  let targetQuestionId = question.questionId;

  try {
    initialConnection = new SignalRClient(env);
    initialConnection.on('QuestionStarted', (questionEvent) => {
      if (!questionReceivedTimestampMs) {
        questionReceivedTimestampMs = Date.now();
        if (questionEvent && questionEvent.questionId) targetQuestionId = questionEvent.questionId;
      }
    });
    await initialConnection.start();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('recon:connect');
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  let sessionToken;
  let participantId;
  try {
    const joinResult = await initialConnection.invoke('JoinGame', pin, uniqueNickname('r'));
    if (!joinResult || joinResult.success !== true) {
      const errorCode = joinResult && joinResult.error ? joinResult.error.code : 'no-response';
      recordJoinFailure(errorCode, 'recon:join');
      initialConnection.close();
      const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
      await delay(remainingTimeMs);
      return;
    }
    sessionToken = joinResult.data.sessionToken;
    participantId = joinResult.data.participantId;
    playersJoined.add(1);
    noUnexpected();
  } catch (joinException) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('recon:join:exception');
    initialConnection.close();
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  // Wait for the question, then answer (correct).
  while (!questionReceivedTimestampMs && exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS && !initialConnection.closed) {
    await delay(50);
  }
  if (questionReceivedTimestampMs) {
    try {
      const submissionAck = await initialConnection.invoke('SubmitAnswer', targetQuestionId, question.correctChoiceId);
      answersSubmitted.add(1);
      if (submissionAck && submissionAck.success === true && submissionAck.data && submissionAck.data.accepted === true) {
        answersAccepted.add(1);
        noUnexpected();
      } else {
        bumpUnexpected('recon:submit');
      }
    } catch (submissionException) {
      bumpUnexpected('recon:submit:exception');
    }
  }

  if (!isReconnector) {
    while (exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS && !initialConnection.closed) {
      await delay(1000);
    }
    initialConnection.close();
    return;
  }

  // --- Reconnection under test ---
  await delay(RECONNECT_AFTER_MS);
  initialConnection.close();
  await delay(300 + Math.random() * 700); // brief outage simulation

  let firstScore = null;
  for (let attemptNumber = 1; attemptNumber <= 2; attemptNumber += 1) {
    const reconnectedConnection = new SignalRClient(env);
    let reconnectResponse;
    const reconnectionStartTimeMs = Date.now();
    try {
      await reconnectedConnection.start();
      reconnectResponse = await reconnectedConnection.invoke('Reconnect', sessionToken);
    } catch (reconnectionException) {
      reconnectionFailures.add(1, { attempt: String(attemptNumber), reason: 'exception' });
      bumpUnexpected('recon:reconnect:exception');
      try {
        reconnectedConnection.close();
      } catch (_) {
        /* ignore */
      }
      continue;
    }
    reconnectionDuration.add(Date.now() - reconnectionStartTimeMs);

    if (!reconnectResponse || reconnectResponse.success !== true || !reconnectResponse.data) {
      const failureReason = reconnectResponse && reconnectResponse.error ? reconnectResponse.error.code : 'no-data';
      reconnectionFailures.add(1, { attempt: String(attemptNumber), reason: failureReason });
      bumpUnexpected('recon:reconnect:failed');
      reconnectedConnection.close();
      continue;
    }

    const reconnectData = reconnectResponse.data;
    if (reconnectData.participantId !== participantId) {
      duplicateParticipants.add(1, { where: 'reconnect' });
      reconnectionFailures.add(1, { attempt: String(attemptNumber), reason: 'identity-changed' });
    }
    if (attemptNumber === 1) {
      firstScore = reconnectData.totalScore;
    } else if (reconnectData.totalScore !== firstScore) {
      inconsistentGameState.add(1, { where: 'reconnect:score-drift' });
    }

    check(reconnectData, {
      'reconnect restored same participant id': (dataContext) => dataContext.participantId === participantId,
      'reconnect restored game status': (dataContext) =>
        (typeof dataContext.status === 'string' && dataContext.status.length > 0) ||
        (typeof dataContext.status === 'number' && dataContext.status >= 0 && dataContext.status <= 5),
      'reconnect restored current question': (dataContext) =>
        !!dataContext.currentQuestion && !!dataContext.currentQuestion.endsAt,
      'reconnect preserved answered flag': (dataContext) => dataContext.alreadyAnsweredCurrentQuestion === true,
      'reconnect preserved a non-negative score': (dataContext) =>
        dataContext.totalScore >= 0 && dataContext.totalScore <= data.question.points,
      'reconnect current question has no correct answer': (dataContext) =>
        !dataContext.currentQuestion ||
        (dataContext.currentQuestion.correctChoiceId === undefined &&
          !(dataContext.currentQuestion.choices || []).some((choice) => 'isCorrect' in choice)),
    });

    const lingerUntilMs = Math.min(TOTAL_SCENARIO_DURATION_MS, exec.instance.currentTestRunDuration + 2000);
    while (exec.instance.currentTestRunDuration < lingerUntilMs && !reconnectedConnection.closed) {
      await delay(500);
    }
    reconnectedConnection.close();
    await delay(400);
  }

  while (exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS) {
    await delay(1000);
  }
}

export async function director(data) {
  const { env, hostToken, gameId, question } = data;
  const directorTags = { scope: 'director' };

  const lobbyState = await waitForParticipantCount(env, hostToken, gameId, PLAYERS, {
    timeoutMs: (parseDurationSeconds(JOIN_RAMP) + 90) * 1000,
    minFraction: 0.95,
    intervalMs: 2000,
  });
  const presentParticipantCount = lobbyState.participants.length;
  console.log(`[reconnection] starting question with ${presentParticipantCount}/${PLAYERS} players`);

  startGame(env, hostToken, gameId);

  // Give players time to answer AND to churn their connections.
  await delay((question.timeLimitSeconds + 20) * 1000);
  endQuestion(env, hostToken, gameId);

  const { results, state } = verifyClosedQuestion(env, hostToken, gameId, question, presentParticipantCount, 'reconnection');
  console.log(
    `[reconnection] participants after churn: ${state.participants.length} (must be ${presentParticipantCount}); ` +
      `answerCount=${results.answerCount}`,
  );
  check(
    state,
    {
      [`participant count unchanged (${presentParticipantCount}, not ${presentParticipantCount + RECONNECT_PLAYERS})`]: (hostState) =>
        hostState.participants.length === presentParticipantCount,
    },
    directorTags,
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

export function teardown(data) {
  try {
    const finalHostState = getHostState(data.env, data.hostToken, data.gameId);
    console.log(`[reconnection] final participants=${finalHostState.participants.length} status=${finalHostState.status}`);
  } catch (_) {
    /* ignore */
  }
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 90;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('reconnection');
