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

const RAMP_LOAD_LEVELS = (__ENV.RAMP_LEVELS || '100,250,500').split(',').map((levelString) => parseInt(levelString.trim(), 10));
const PEAK_LOAD_PLAYERS = Math.max(...RAMP_LOAD_LEVELS);
const JOIN_RAMP = __ENV.RAMP_JOIN_RAMP || __ENV.JOIN_RAMP || '60s';
const QUESTION_TIME_LIMIT_SECONDS = Math.min(300, Math.max(10, intEnv('RAMP_QUESTION_SECONDS', 20)));
const TOTAL_QUESTIONS_COUNT = Math.max(RAMP_LOAD_LEVELS.length, intEnv('RAMP_QUESTIONS', RAMP_LOAD_LEVELS.length));

const executionStages = [
  { duration: JOIN_RAMP, target: PEAK_LOAD_PLAYERS },
  { duration: `${TOTAL_QUESTIONS_COUNT * (QUESTION_TIME_LIMIT_SECONDS + 6) + 10}s`, target: PEAK_LOAD_PLAYERS },
  { duration: '10s', target: 0 },
];

const perLevelThresholds = {};
for (const loadLevel of RAMP_LOAD_LEVELS) {
  perLevelThresholds[`answer_submission_duration{load:${loadLevel}}`] =
    loadLevel <= 500 ? ['p(95)<1000'] : [{ threshold: 'p(95)<100000', abortOnFail: false }];
  perLevelThresholds[`unexpected_error_rate{load:${loadLevel}}`] =
    loadLevel <= 500 ? ['rate<0.05'] : [{ threshold: 'rate<1', abortOnFail: false }];
  perLevelThresholds[`answers_submitted{load:${loadLevel}}`] = ['count>=0'];
  perLevelThresholds[`answers_accepted{load:${loadLevel}}`] = ['count>=0'];
}

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: executionStages,
      gracefulRampDown: '15s',
      gracefulStop: '30s',
    },
    director: {
      executor: 'per-vu-iterations',
      exec: 'director',
      vus: 1,
      iterations: 1,
      maxDuration: `${executionStages.reduce((totalDuration, stage) => totalDuration + parseDurationSeconds(stage.duration), 0) + 120}s`,
    },
  },
  thresholds: Object.assign(
    {
      checks: ['rate>0.90'],
    },
    perLevelThresholds,
  ),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, PEAK_LOAD_PLAYERS);
  const provisionedGame = provisionGames(environmentConfig, {
    questions: TOTAL_QUESTIONS_COUNT,
    timeLimitSeconds: QUESTION_TIME_LIMIT_SECONDS,
    points: 1000,
    games: 1,
  });
  console.log(`[ramp] game ${provisionedGame.gameId} pin ${provisionedGame.pin} with ${provisionedGame.questions.length} questions for levels: ${RAMP_LOAD_LEVELS.join(', ')}`);
  return {
    env: environmentConfig,
    gameId: provisionedGame.gameId,
    pin: provisionedGame.pin,
    hostToken: provisionedGame.hostToken,
    questions: provisionedGame.questions,
  };
}

export async function player(data) {
  const { env, pin, questions } = data;
  const vuIndex = exec.vu.idInTest;

  const signalrClient = new SignalRClient(env, { onClose: () => {} });
  let activeQuestionId = null;
  let answeredQuestionId = null;

  signalrClient.on('QuestionStarted', (questionStartedPayload) => {
    const receivedId = questionStartedPayload && (questionStartedPayload.questionId || questionStartedPayload.QuestionId);
    if (receivedId) activeQuestionId = String(receivedId).toLowerCase();
  });

  try {
    await signalrClient.start();
    const joinResponse = await signalrClient.invoke('JoinGame', pin, uniqueNickname('s'));
    if (!joinResponse || joinResponse.success !== true) {
      const errorCode = joinResponse && joinResponse.error ? joinResponse.error.code : 'no-response';
      recordJoinFailure(errorCode, `ramp:join:${errorCode}`);
      signalrClient.close();
      return;
    }
    playersJoined.add(1);
    noUnexpected();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('ramp:connect');
    signalrClient.close();
    return;
  }

  // Keep client alive and respond to questions throughout the game
  const maxHoldDurationMs = (parseDurationSeconds(JOIN_RAMP) + TOTAL_QUESTIONS_COUNT * (QUESTION_TIME_LIMIT_SECONDS + 10) + 30) * 1000;
  const holdDeadlineTimestampMs = Date.now() + maxHoldDurationMs;

  while (Date.now() < holdDeadlineTimestampMs && !signalrClient.closed) {
    if (activeQuestionId && activeQuestionId !== answeredQuestionId) {
      answeredQuestionId = activeQuestionId;
      const matchingQuestion = questions.find((q) => String(q.questionId).toLowerCase() === activeQuestionId);
      const questionIndex = matchingQuestion ? matchingQuestion.orderIndex : 0;
      const activeLoadLevel = RAMP_LOAD_LEVELS[Math.min(questionIndex, RAMP_LOAD_LEVELS.length - 1)];

      // Players up to activeLoadLevel submit answers for this question tier
      if (vuIndex <= activeLoadLevel + 1) {
        const choiceIdToSubmit = matchingQuestion ? matchingQuestion.correctChoiceId : null;
        if (choiceIdToSubmit) {
          const submissionStartTimeMs = Date.now();
          const loadTags = { load: String(activeLoadLevel) };
          try {
            const submissionAck = await signalrClient.invoke('SubmitAnswer', activeQuestionId, choiceIdToSubmit);
            answerSubmissionDuration.add(Date.now() - submissionStartTimeMs, loadTags);
            answersSubmitted.add(1, loadTags);
            if (submissionAck && submissionAck.success === true && submissionAck.data && submissionAck.data.accepted === true) {
              answersAccepted.add(1, loadTags);
              noUnexpected(loadTags);
            } else if (submissionAck && submissionAck.success === false && submissionAck.error) {
              answersRejected.add(1, { code: submissionAck.error.code, ...loadTags });
            } else {
              unexpectedAnswerFailures.add(1, loadTags);
              bumpUnexpected('ramp:submit:malformed', loadTags);
            }
          } catch (submissionException) {
            unexpectedAnswerFailures.add(1, loadTags);
            bumpUnexpected('ramp:submit:exception', loadTags);
          }
        }
      }
    }
    await delay(100);
  }

  try {
    signalrClient.close();
  } catch (_) {}
}

export async function director(data) {
  const { env, hostToken, gameId, questions } = data;

  // 1. Wait for cohort to assemble in the lobby (WaitingForPlayers state)
  console.log(`[ramp] waiting for players to join lobby (target: ${PEAK_LOAD_PLAYERS})...`);
  const lobbyState = await waitForParticipantCount(env, hostToken, gameId, PEAK_LOAD_PLAYERS, {
    timeoutMs: (parseDurationSeconds(JOIN_RAMP) + 40) * 1000,
    minFraction: 0.85,
    intervalMs: 1500,
  });
  const presentParticipantCount = lobbyState && lobbyState.participants ? lobbyState.participants.length : 0;
  console.log(`[ramp] lobby ready with ${presentParticipantCount} players. Starting questions...`);

  let hasStarted = false;
  for (let questionIndex = 0; questionIndex < questions.length; questionIndex += 1) {
    try {
      if (!hasStarted) {
        startGame(env, hostToken, gameId);
        hasStarted = true;
      } else {
        advance(env, hostToken, gameId);
      }
    } catch (startError) {
      console.log(`[ramp] director stop at question ${questionIndex}: ${startError}`);
      break;
    }
    await delay((questions[questionIndex].timeLimitSeconds + 3) * 1000);
    try {
      endQuestion(env, hostToken, gameId);
      showLeaderboard(env, hostToken, gameId);
    } catch (closeError) {
      console.log(`[ramp] director close/leaderboard error at question ${questionIndex}: ${closeError}`);
      break;
    }
    await delay(2000);
  }

  check(hasStarted, { 'director drove at least one question': (status) => status === true });
  try {
    endGame(env, hostToken, gameId);
  } catch (_) {
    /* best-effort cleanup */
  }
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 30;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('ramp');
