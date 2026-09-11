// Scenario 3 — Question broadcast with 500 connected players + group isolation.
//
// PLAYERS players join game A. A smaller ISOLATION_PLAYERS group joins game B
// (same quiz, never started). The director starts A's first question and, after
// the window, ends it. We assert:
//   * every game-A player received QuestionStarted   (question_delivery_failures)
//   * end-to-end delivery latency p95 (approx, server->client clock)
//   * NO game-B player ever received any game-A event (session_isolation_violations)
//   * the QuestionStarted payload players get contains no correct answer
//
//   k6 run -e ALLOW_LOAD_TEST=true load-tests/scenarios/question-broadcast.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionGames, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, endGame } from '../helpers/rest.js';
import { waitForParticipantCount } from '../helpers/orchestration.js';
import { questionDeliveryThresholds, mergeThresholds } from '../config/thresholds.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  questionDeliveryDuration,
  questionDeliveryFailures,
  questionDelivered,
  sessionIsolationViolations,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const PLAYERS = intEnv('PLAYERS', 500);
const ISOLATION_PLAYERS = intEnv('ISOLATION_PLAYERS', 25);
const JOIN_RAMP = __ENV.JOIN_RAMP || '90s';
const TIME_LIMIT = Math.min(300, Math.max(5, intEnv('ANSWER_TIME_LIMIT', 60)));
const CLOCK_SKEW_MS = Number(__ENV.CLOCK_SKEW_MS || 0);
const READY_FRACTION = Number(__ENV.READY_FRACTION || 0.98);

const TOTAL_HOLD_DURATION_SECONDS = parseDurationSeconds(JOIN_RAMP) + 90 + TIME_LIMIT;

export const options = {
  hosts: hostsOverride(),
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: PLAYERS + ISOLATION_PLAYERS },
        { duration: `${90 + TIME_LIMIT}s`, target: PLAYERS + ISOLATION_PLAYERS },
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
  thresholds: mergeThresholds(questionDeliveryThresholds(), {
    session_isolation_violations: ['count<1'],
    question_delivery_failures: [`count<${Math.ceil(PLAYERS * 0.01)}`],
    'checks{scope:director}': ['rate>0.99'],
  }),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, PLAYERS + ISOLATION_PLAYERS);
  const provisionedGames = provisionGames(environmentConfig, {
    questions: 1,
    timeLimitSeconds: TIME_LIMIT,
    points: 1000,
    games: 2,
  });
  return {
    env: environmentConfig,
    hostToken: provisionedGames.hostToken,
    gameA: provisionedGames.games[0],
    gameB: provisionedGames.games[1],
    question: provisionedGames.questions[0],
  };
}

export async function player(data) {
  const { env, gameA, gameB } = data;
  const holdDeadlineTimestampMs = Date.now() + TOTAL_HOLD_DURATION_SECONDS * 1000;

  // First ISOLATION_PLAYERS VUs are the game-B control group.
  const isControlGroupParticipant = exec.vu.idInTest <= ISOLATION_PLAYERS;
  const targetGame = isControlGroupParticipant ? gameB : gameA;

  let signalrClient;
  let questionReceivedTimestampMs = 0;
  let receivedQuestionPayload = null;
  let hasIsolationLeaked = false;

  try {
    signalrClient = new SignalRClient(env);
    const registerEventListener = (eventName) => (eventArgs) => {
      if (isControlGroupParticipant) {
        if (eventName !== 'GameEnded' || (eventArgs && eventArgs.gameId === gameA.gameId)) {
          hasIsolationLeaked = true;
          sessionIsolationViolations.add(1, { event: eventName });
        }
      } else if (eventName === 'QuestionStarted' && !questionReceivedTimestampMs) {
        questionReceivedTimestampMs = Date.now();
        receivedQuestionPayload = eventArgs;
      }
    };
    signalrClient.on('QuestionStarted', registerEventListener('QuestionStarted'));
    signalrClient.on('QuestionStartedForHost', registerEventListener('QuestionStartedForHost'));
    signalrClient.on('QuestionEnded', registerEventListener('QuestionEnded'));
    signalrClient.on('LeaderboardUpdated', registerEventListener('LeaderboardUpdated'));
    signalrClient.on('GameEnded', registerEventListener('GameEnded'));
    await signalrClient.start();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('broadcast:connect');
    return;
  }

  try {
    const nickname = uniqueNickname(isControlGroupParticipant ? 'ctl' : 'a');
    const joinResult = await signalrClient.invoke('JoinGame', targetGame.pin, nickname);
    if (!joinResult || joinResult.success !== true) {
      const errorCode = joinResult && joinResult.error ? joinResult.error.code : 'no-response';
      recordJoinFailure(errorCode, `broadcast:join:${errorCode}`);
      signalrClient.close();
      return;
    }
    playersJoined.add(1, { group: isControlGroupParticipant ? 'B' : 'A' });
    noUnexpected();
  } catch (joinException) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('broadcast:join:exception');
    signalrClient.close();
    return;
  }

  while (Date.now() < holdDeadlineTimestampMs && !signalrClient.closed) {
    if (!isControlGroupParticipant && questionReceivedTimestampMs) {
      break;
    }
    await delay(50);
  }

  if (isControlGroupParticipant) {
    check(null, { 'control-group player received NO game-A event': () => !hasIsolationLeaked });
  } else if (questionReceivedTimestampMs) {
    if (receivedQuestionPayload && receivedQuestionPayload.endsAt) {
      const clockSkew = Number.isFinite(data.clockSkewMs) ? data.clockSkewMs : CLOCK_SKEW_MS;
      const serverStartTimestamp = Date.parse(receivedQuestionPayload.endsAt) - data.question.timeLimitSeconds * 1000;
      const deliveryLatencyMs = questionReceivedTimestampMs - serverStartTimestamp - clockSkew;
      if (deliveryLatencyMs > -2000 && deliveryLatencyMs < 60000) {
        questionDeliveryDuration.add(Math.max(1, deliveryLatencyMs));
      }
    }
    questionDelivered.add(1);
    check(receivedQuestionPayload, {
      'received QuestionStarted': (payload) => !!payload,
      'no correct answer in player payload': (payload) =>
        payload && payload.correctChoiceId === undefined && !(payload.choices || []).some((choice) => 'isCorrect' in choice),
    });
  } else {
    questionDeliveryFailures.add(1);
    check(null, { 'received QuestionStarted': () => false });
  }

  while (Date.now() < holdDeadlineTimestampMs && !signalrClient.closed) {
    await delay(1000);
  }
  signalrClient.close();
}

export async function director(data) {
  const { env, hostToken, gameA } = data;
  const directorTags = { scope: 'director' };

  const lobbyState = await waitForParticipantCount(env, hostToken, gameA.gameId, PLAYERS, {
    timeoutMs: (parseDurationSeconds(JOIN_RAMP) + 75) * 1000,
    minFraction: READY_FRACTION,
    intervalMs: 2000,
  });
  console.log(`[question-broadcast] starting with ${lobbyState.participants.length}/${PLAYERS} game-A players`);
  check(
    lobbyState,
    {
      [`>= ${Math.ceil(PLAYERS * READY_FRACTION)} game-A players present`]: (state) =>
        state.participants.length >= Math.ceil(PLAYERS * READY_FRACTION),
    },
    directorTags,
  );

  const startGameResult = startGame(env, hostToken, gameA.gameId);
  check(startGameResult, { 'question started': (res) => !!res && !!res.player }, directorTags);

  await delay((data.question.timeLimitSeconds + 10) * 1000);
  endQuestion(env, hostToken, gameA.gameId);

  try {
    endGame(env, hostToken, gameA.gameId);
    endGame(env, hostToken, data.gameB.gameId);
  } catch (_) {
    /* best-effort cleanup */
  }
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 90;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('question-broadcast');
