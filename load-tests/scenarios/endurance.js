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
  signalrUnexpectedDisconnects,
  reconnectionFailures,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const DURATION_MINUTES = Math.max(1, intEnv('ENDURANCE_MINUTES', 10));
const PLAYERS = intEnv('ENDURANCE_PLAYERS', 200);
const QUESTION_DURATION_SECONDS = Math.min(300, Math.max(8, intEnv('ENDURANCE_QUESTION_SECONDS', 15)));
const QUESTION_GAP_SECONDS = intEnv('ENDURANCE_GAP_SECONDS', 8);
const QUESTION_CYCLE_PERIOD_SECONDS = QUESTION_DURATION_SECONDS + QUESTION_GAP_SECONDS;
const TOTAL_RUN_SECONDS = DURATION_MINUTES * 60;
const TOTAL_SCENARIO_DURATION_MS = (60 + TOTAL_RUN_SECONDS + 15) * 1000;
const REQUIRED_QUESTIONS_COUNT = Math.min(120, Math.ceil(TOTAL_RUN_SECONDS / QUESTION_CYCLE_PERIOD_SECONDS) + 3);
const HALF_RUN_DURATION_MS = (TOTAL_RUN_SECONDS / 2) * 1000;

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: '60s', target: PLAYERS },
        { duration: `${TOTAL_RUN_SECONDS}s`, target: PLAYERS },
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
      maxDuration: `${TOTAL_RUN_SECONDS + 180}s`,
    },
  },
  thresholds: {
    answer_submission_duration: ['p(95)<500'],
    'answer_submission_duration{half:first}': ['p(95)<500'],
    'answer_submission_duration{half:second}': ['p(95)<500'],
    'http_req_duration{half:second}': ['p(95)<500'],
    unexpected_answer_failures: ['count<1'],
    signalr_unexpected_disconnects: [`count<${Math.max(1, Math.ceil(PLAYERS * 0.02))}`],
    reconnection_failures: [`count<${Math.max(1, Math.ceil(PLAYERS * 0.02))}`],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, PLAYERS);
  const provisionedGame = provisionGames(environmentConfig, {
    questions: REQUIRED_QUESTIONS_COUNT,
    timeLimitSeconds: QUESTION_DURATION_SECONDS,
    points: 1000,
    games: 1,
  });
  console.log(
    `[endurance] ${DURATION_MINUTES} min, ${PLAYERS} players, ${provisionedGame.questions.length} questions, period ${QUESTION_CYCLE_PERIOD_SECONDS}s`,
  );
  return {
    env: environmentConfig,
    gameId: provisionedGame.gameId,
    pin: provisionedGame.pin,
    hostToken: provisionedGame.hostToken,
    questions: provisionedGame.questions,
  };
}

export async function player(data) {
  if (exec.vu.iterationInScenario > 0) {
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  const { env, pin, questions } = data;

  let signalrClient = null;
  let currentQuestionId = null;
  let answeredQuestionId = null;
  let sessionToken = null;

  signalrClient = new SignalRClient(env, {
    onClose: () => {
      signalrUnexpectedDisconnects.add(1);
    },
  });
  signalrClient.on('QuestionStarted', (questionEvent) => {
    if (questionEvent && questionEvent.questionId) {
      currentQuestionId = questionEvent.questionId;
    }
  });

  try {
    await signalrClient.start();
    const joinResponse = await signalrClient.invoke('JoinGame', pin, uniqueNickname('e'));
    if (!joinResponse || joinResponse.success !== true) {
      const errorCode = joinResponse && joinResponse.error ? joinResponse.error.code : 'no-response';
      recordJoinFailure(errorCode, 'endurance:join');
      signalrClient.close();
      const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
      await delay(remainingTimeMs);
      return;
    }
    sessionToken = joinResponse.data.sessionToken;
    playersJoined.add(1);
    noUnexpected();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('endurance:connect');
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  // Persistent session loop for the entire endurance duration
  while (exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS) {
    // Reconnect if the connection was unexpectedly dropped
    if (!signalrClient || signalrClient.closed) {
      const recoveredClient = new SignalRClient(env, {
        onClose: () => {
          signalrUnexpectedDisconnects.add(1);
        },
      });
      recoveredClient.on('QuestionStarted', (questionEvent) => {
        if (questionEvent && questionEvent.questionId) {
          currentQuestionId = questionEvent.questionId;
        }
      });
      try {
        await recoveredClient.start();
        const reconnectResult = await recoveredClient.invoke('Reconnect', sessionToken);
        if (!reconnectResult || reconnectResult.success !== true) {
          reconnectionFailures.add(1, { where: 'endurance' });
          bumpUnexpected('endurance:reconnect');
        }
        signalrClient = recoveredClient;
      } catch (reconnectException) {
        reconnectionFailures.add(1, { where: 'endurance:exception' });
        bumpUnexpected('endurance:reconnect:exception');
        await delay(2000);
        continue;
      }
    }

    // Submit an answer when a new question starts
    if (currentQuestionId && currentQuestionId !== answeredQuestionId) {
      answeredQuestionId = currentQuestionId;
      const targetQuestion = questions.find((q) => q.questionId === currentQuestionId);
      if (targetQuestion) {
        const testHalf = exec.instance.currentTestRunDuration < 60000 + HALF_RUN_DURATION_MS ? 'first' : 'second';
        const submissionStartTimeMs = Date.now();
        try {
          const submissionAck = await signalrClient.invoke('SubmitAnswer', currentQuestionId, targetQuestion.correctChoiceId);
          answerSubmissionDuration.add(Date.now() - submissionStartTimeMs, { half: testHalf });
          answersSubmitted.add(1, { half: testHalf });
          if (submissionAck && submissionAck.success === true && submissionAck.data && submissionAck.data.accepted === true) {
            answersAccepted.add(1, { half: testHalf });
            noUnexpected();
          } else if (submissionAck && submissionAck.success === false && submissionAck.error) {
            answersRejected.add(1, { code: submissionAck.error.code });
          } else {
            unexpectedAnswerFailures.add(1, { half: testHalf });
            bumpUnexpected('endurance:submit:malformed');
          }
        } catch (submissionException) {
          unexpectedAnswerFailures.add(1, { half: testHalf });
          bumpUnexpected('endurance:submit:exception');
        }
      }
    }

    await delay(500);
  }

  if (signalrClient) {
    signalrClient.close();
  }
}

export async function director(data) {
  const { env, hostToken, gameId, questions } = data;
  console.log(`[endurance] waiting for players to assemble in lobby (target: ${PLAYERS})...`);
  await waitForParticipantCount(env, hostToken, gameId, PLAYERS, {
    timeoutMs: 90000,
    minFraction: 0.90,
    intervalMs: 1500,
  });

  let hasStarted = false;
  let completedCycles = 0;
  const stopAtTestDurationMs = TOTAL_RUN_SECONDS * 1000 + 70000;

  for (let questionIndex = 0; questionIndex < questions.length; questionIndex += 1) {
    if (exec.instance.currentTestRunDuration > stopAtTestDurationMs) break;
    try {
      if (!hasStarted) {
        startGame(env, hostToken, gameId);
        hasStarted = true;
      } else {
        advance(env, hostToken, gameId);
      }
      await delay((questions[questionIndex].timeLimitSeconds + 3) * 1000);
      endQuestion(env, hostToken, gameId);
      showLeaderboard(env, hostToken, gameId);
      completedCycles += 1;
      await delay(Math.max(1000, (QUESTION_GAP_SECONDS - 3) * 1000));
    } catch (directorException) {
      console.log(`[endurance] director stop at cycle ${completedCycles}: ${directorException}`);
      break;
    }
  }

  console.log(`[endurance] completed ${completedCycles} question cycles`);
  check(
    { completedCycles },
    { 'ran a realistic number of question cycles': (ctx) => ctx.completedCycles >= Math.min(3, questions.length) },
  );

  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort */
  }
}

export const handleSummary = makeHandleSummary('endurance');
