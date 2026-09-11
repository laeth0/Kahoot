// Scenario 7 — GAMES x PLAYERS_PER_GAME concurrent games (default 10 x 50 = 500).
//
// Every game has its OWN quiz, so its questionId is unique and any foreign event
// is unambiguous. Each player joins exactly one game and records the questionId
// of every QuestionStarted it receives. The director starts all games (staggered)
// then ends them. Asserts:
//   * each player receives exactly its own game's QuestionStarted, nothing else
//     (session_isolation_violations == 0)
//   * per game: results.answerCount == that game's player count (independent state)
//   * per game: leaderboard lists only that game's participants; participant id
//     sets across games are pairwise disjoint (independent scores/leaderboards)
//
//   k6 run -e ALLOW_LOAD_TEST=true -e GAMES=10 -e PLAYERS_PER_GAME=50 \
//     load-tests/scenarios/multiple-games.js

import { check } from 'k6';
import exec from 'k6/execution';
import { resolveEnv, assertLoadAllowed, intEnv, hostsOverride } from '../config/environments.js';
import { SignalRClient, delay } from '../helpers/signalr.js';
import { provisionPerGameQuizzes, uniqueNickname } from '../helpers/testdata.js';
import { startGame, endQuestion, showLeaderboard, endGame } from '../helpers/rest.js';
import { waitForParticipantCount, verifyClosedQuestion } from '../helpers/orchestration.js';
import { correctnessThresholds, mergeThresholds } from '../config/thresholds.js';
import { makeHandleSummary } from '../helpers/summary.js';
import {
  playersJoined,
  playerJoinFailures,
  recordJoinFailure,
  answersSubmitted,
  answersAccepted,
  sessionIsolationViolations,
  inconsistentGameState,
  bumpUnexpected,
  noUnexpected,
} from '../helpers/metrics.js';

const GAMES = intEnv('GAMES', 10);
const PLAYERS_PER_GAME = intEnv('PLAYERS_PER_GAME', 50);
const TOTAL_PLAYERS = GAMES * PLAYERS_PER_GAME;
const TIME_LIMIT = Math.min(300, Math.max(20, intEnv('ANSWER_TIME_LIMIT', 60)));
const JOIN_RAMP = __ENV.JOIN_RAMP || '90s';
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
        { duration: JOIN_RAMP, target: TOTAL_PLAYERS },
        { duration: `${120 + TIME_LIMIT}s`, target: TOTAL_PLAYERS },
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
      maxDuration: `${TOTAL_HOLD_DURATION_SECONDS + 90}s`,
    },
  },
  thresholds: mergeThresholds(correctnessThresholds(), {
    session_isolation_violations: ['count<1'],
    inconsistent_game_state: ['count<1'],
    'checks{scope:director}': ['rate>0.99'],
  }),
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const environmentConfig = resolveEnv();
  assertLoadAllowed(environmentConfig, TOTAL_PLAYERS);
  const provisionedQuizzes = provisionPerGameQuizzes(environmentConfig, { count: GAMES, timeLimitSeconds: TIME_LIMIT, points: 1000 });
  return { env: environmentConfig, hostToken: provisionedQuizzes.hostToken, games: provisionedQuizzes.games };
}

export async function player(data) {
  if (exec.vu.iterationInScenario > 0) {
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  const { env, games } = data;

  const assignedGameIndex = (exec.vu.idInTest - 1) % GAMES;
  const targetGameSession = games[assignedGameIndex];
  const assignedQuestionId = targetGameSession.firstQuestion.questionId;

  let signalrClient;
  const receivedEvents = [];
  let ownQuestionReceivedTimestampMs = 0;

  try {
    signalrClient = new SignalRClient(env);
    const createEventRecorder = (eventLabel) => (eventPayload) => {
      const payloadQuestionId = eventPayload && (eventPayload.questionId || eventPayload.QuestionId);
      receivedEvents.push({ label: eventLabel, questionId: payloadQuestionId });
      if (eventLabel === 'QuestionStarted' && payloadQuestionId === assignedQuestionId && !ownQuestionReceivedTimestampMs) {
        ownQuestionReceivedTimestampMs = Date.now();
      }
      if (payloadQuestionId && payloadQuestionId !== assignedQuestionId) {
        sessionIsolationViolations.add(1, { event: eventLabel, game: String(assignedGameIndex) });
      }
    };
    signalrClient.on('QuestionStarted', createEventRecorder('QuestionStarted'));
    signalrClient.on('QuestionEnded', createEventRecorder('QuestionEnded'));
    signalrClient.on('LeaderboardUpdated', () => {});
    signalrClient.on('GameEnded', () => {});
    await signalrClient.start();
  } catch (connectionError) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('multi:connect');
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  try {
    const nickname = uniqueNickname(`g${assignedGameIndex}`);
    const joinResult = await signalrClient.invoke('JoinGame', targetGameSession.pin, nickname);
    if (!joinResult || joinResult.success !== true) {
      const errorCode = joinResult && joinResult.error ? joinResult.error.code : 'no-response';
      recordJoinFailure(errorCode, 'multi:join');
      signalrClient.close();
      const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
      await delay(remainingTimeMs);
      return;
    }
    playersJoined.add(1, { game: String(assignedGameIndex) });
    noUnexpected();
  } catch (joinException) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('multi:join:exception');
    signalrClient.close();
    const remainingTimeMs = Math.max(100, TOTAL_SCENARIO_DURATION_MS - exec.instance.currentTestRunDuration);
    await delay(remainingTimeMs);
    return;
  }

  while (!ownQuestionReceivedTimestampMs && exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS && !signalrClient.closed) {
    await delay(50);
  }

  if (ownQuestionReceivedTimestampMs) {
    try {
      const submissionAck = await signalrClient.invoke('SubmitAnswer', assignedQuestionId, targetGameSession.firstQuestion.correctChoiceId);
      answersSubmitted.add(1, { game: String(assignedGameIndex) });
      if (submissionAck && submissionAck.success === true && submissionAck.data && submissionAck.data.accepted === true) {
        answersAccepted.add(1, { game: String(assignedGameIndex) });
        noUnexpected();
      } else {
        bumpUnexpected('multi:submit');
      }
    } catch (submissionException) {
      bumpUnexpected('multi:submit:exception');
    }
  }

  while (exec.instance.currentTestRunDuration < TOTAL_SCENARIO_DURATION_MS && !signalrClient.closed) {
    await delay(500);
  }

  check(
    { receivedEvents, assignedGameIndex },
    {
      'player received its own QuestionStarted': (ctx) =>
        ctx.receivedEvents.some((eventItem) => eventItem.label === 'QuestionStarted' && eventItem.questionId === assignedQuestionId),
      'player received NO foreign questionId': (ctx) =>
        ctx.receivedEvents.every((eventItem) => !eventItem.questionId || eventItem.questionId === assignedQuestionId),
    },
  );

  signalrClient.close();
}

export async function director(data) {
  const { env, hostToken, games } = data;
  const directorTags = { scope: 'director' };

  // Start every game once participants assemble in the lobby
  const startedGames = [];
  for (const game of games) {
    const lobbyState = await waitForParticipantCount(env, hostToken, game.gameId, PLAYERS_PER_GAME, {
      timeoutMs: (parseDurationSeconds(JOIN_RAMP) + 90) * 1000,
      minFraction: 0.95,
      intervalMs: 1500,
    });
    startGame(env, hostToken, game.gameId);
    startedGames.push({ game, present: lobbyState.participants.length });
    console.log(`[multiple-games] game ${game.index} started with ${lobbyState.participants.length}/${PLAYERS_PER_GAME}`);
    await delay(750);
  }

  await delay((data.games[0].firstQuestion.timeLimitSeconds + 15) * 1000);

  // Close + verify each game independently, then cross-check disjointness.
  const gameStateSummaries = [];
  for (const { game, present } of startedGames) {
    endQuestion(env, hostToken, game.gameId);
    const { results, state } = verifyClosedQuestion(env, hostToken, game.gameId, game.firstQuestion, present, `multi:g${game.index}`);
    const leaderboard = showLeaderboard(env, hostToken, game.gameId);
    const participantIdSet = new Set(state.participants.map((participant) => participant.id));
    gameStateSummaries.push({
      index: game.index,
      ids: participantIdSet,
      leaderboardCount: leaderboard.entries.length,
      present,
      answerCount: results.answerCount,
    });

    check(
      { results, leaderboard, present },
      {
        [`g${game.index}: answerCount == players (${present})`]: (ctx) => ctx.results.answerCount === ctx.present,
        [`g${game.index}: leaderboard size == players`]: (ctx) => ctx.leaderboard.entries.length === ctx.present,
        [`g${game.index}: leaderboard ids subset of game roster`]: (ctx) =>
          ctx.leaderboard.entries.every((entry) => participantIdSet.has(entry.participantId)),
      },
      directorTags,
    );
  }

  // Pairwise-disjoint participant id sets => fully isolated rosters/scores.
  let overlappingParticipantCount = 0;
  for (let firstGameIndex = 0; firstGameIndex < gameStateSummaries.length; firstGameIndex += 1) {
    for (let secondGameIndex = firstGameIndex + 1; secondGameIndex < gameStateSummaries.length; secondGameIndex += 1) {
      for (const participantId of gameStateSummaries[firstGameIndex].ids) {
        if (gameStateSummaries[secondGameIndex].ids.has(participantId)) {
          overlappingParticipantCount += 1;
        }
      }
    }
  }
  if (overlappingParticipantCount > 0) {
    sessionIsolationViolations.add(overlappingParticipantCount, { where: 'roster-overlap' });
  }
  check(
    { overlappingParticipantCount },
    { 'no participant appears in two games': (ctx) => ctx.overlappingParticipantCount === 0 },
    directorTags,
  );

  for (const game of games) {
    try {
      endGame(env, hostToken, game.gameId);
    } catch (_) {
      /* best-effort */
    }
  }
}

function parseDurationSeconds(durationString) {
  const match = /^(\d+)(s|m)?$/.exec(String(durationString).trim());
  if (!match) return 90;
  return match[2] === 'm' ? Number(match[1]) * 60 : Number(match[1]);
}

export const handleSummary = makeHandleSummary('multiple-games');
