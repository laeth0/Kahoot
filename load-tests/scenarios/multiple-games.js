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
import { resolveEnv, assertLoadAllowed, intEnv } from '../config/environments.js';
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
const TOTAL = GAMES * PLAYERS_PER_GAME;
const TIME_LIMIT = Math.min(300, Math.max(20, intEnv('ANSWER_TIME_LIMIT', 60)));
const JOIN_RAMP = __ENV.JOIN_RAMP || '90s';
const HOLD = durationSeconds(JOIN_RAMP) + 120 + TIME_LIMIT;

export const options = {
  scenarios: {
    players: {
      executor: 'ramping-vus',
      exec: 'player',
      startVUs: 0,
      stages: [
        { duration: JOIN_RAMP, target: TOTAL },
        { duration: `${120 + TIME_LIMIT}s`, target: TOTAL },
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
      maxDuration: `${HOLD + 90}s`,
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
  const env = resolveEnv();
  assertLoadAllowed(env, TOTAL);
  const p = provisionPerGameQuizzes(env, { count: GAMES, timeLimitSeconds: TIME_LIMIT, points: 1000 });
  return { env, hostToken: p.hostToken, games: p.games };
}

let done = false;

export async function player(data) {
  const { env, games } = data;
  if (done) {
    await delay(3000);
    return;
  }
  done = true;

  const g = (exec.vu.idInTest - 1) % GAMES;
  const game = games[g];
  const myQuestionId = game.firstQuestion.questionId;

  let client;
  const received = []; // questionIds seen
  let recvOwnAt = 0;

  try {
    client = new SignalRClient(env);
    const record = (label) => (payload) => {
      const qid = payload && (payload.questionId || (payload && payload.QuestionId));
      received.push({ label, qid });
      if (label === 'QuestionStarted' && qid === myQuestionId && !recvOwnAt) recvOwnAt = Date.now();
      if (qid && qid !== myQuestionId) {
        sessionIsolationViolations.add(1, { event: label, game: String(g) });
      }
    };
    client.on('QuestionStarted', record('QuestionStarted'));
    client.on('QuestionEnded', record('QuestionEnded'));
    client.on('LeaderboardUpdated', () => {
      /* leaderboard payload carries no questionId; isolation checked director-side */
    });
    client.on('GameEnded', () => {});
    await client.start();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'connect' });
    bumpUnexpected('multi:connect');
    return;
  }

  let qidToAnswer = myQuestionId;
  try {
    const res = await client.invoke('JoinGame', game.pin, uniqueNickname(`g${g}`));
    if (!res || res.success !== true) {
      recordJoinFailure(res && res.error ? res.error.code : 'no-response', 'multi:join');
      client.close();
      return;
    }
    playersJoined.add(1, { game: String(g) });
    noUnexpected();
  } catch (e) {
    playerJoinFailures.add(1, { reason: 'exception' });
    bumpUnexpected('multi:join:exception');
    client.close();
    return;
  }

  const deadline = Date.now() + HOLD * 1000;
  while (!recvOwnAt && Date.now() < deadline && !client.closed) await delay(50);

  if (recvOwnAt) {
    try {
      const ack = await client.invoke('SubmitAnswer', qidToAnswer, game.firstQuestion.correctChoiceId);
      answersSubmitted.add(1, { game: String(g) });
      if (ack && ack.success === true && ack.data && ack.data.accepted === true) {
        answersAccepted.add(1, { game: String(g) });
        noUnexpected();
      } else {
        bumpUnexpected('multi:submit');
      }
    } catch (e) {
      bumpUnexpected('multi:submit:exception');
    }
  }

  // Linger to catch any cross-game event that might arrive late.
  const lingerUntil = Math.min(deadline, Date.now() + 10000);
  while (Date.now() < lingerUntil && !client.closed) await delay(500);

  check(
    { received, g },
    {
      'player received its own QuestionStarted': (x) =>
        x.received.some((r) => r.label === 'QuestionStarted' && r.qid === myQuestionId),
      'player received NO foreign questionId': (x) => x.received.every((r) => !r.qid || r.qid === myQuestionId),
    },
  );

  client.close();
}

export async function director(data) {
  const { env, hostToken, games } = data;
  const tags = { scope: 'director' };

  // Start every game once ~all its players are in (staggered).
  const started = [];
  for (const game of games) {
    const pre = await waitForParticipantCount(env, hostToken, game.gameId, PLAYERS_PER_GAME, {
      timeoutMs: (durationSeconds(JOIN_RAMP) + 90) * 1000,
      minFraction: 0.95,
      intervalMs: 1500,
    });
    startGame(env, hostToken, game.gameId);
    started.push({ game, present: pre.participants.length });
    console.log(`[multiple-games] game ${game.index} started with ${pre.participants.length}/${PLAYERS_PER_GAME}`);
    await delay(750);
  }

  await delay((data.games[0].firstQuestion.timeLimitSeconds + 15) * 1000);

  // Close + verify each game independently, then cross-check disjointness.
  const idSets = [];
  for (const { game, present } of started) {
    endQuestion(env, hostToken, game.gameId);
    const { results, state } = verifyClosedQuestion(env, hostToken, game.gameId, game.firstQuestion, present, `multi:g${game.index}`);
    const lb = showLeaderboard(env, hostToken, game.gameId);
    const ids = new Set(state.participants.map((p) => p.id));
    idSets.push({ index: game.index, ids, lbCount: lb.entries.length, present, answerCount: results.answerCount });

    check(
      { results, lb, present },
      {
        [`g${game.index}: answerCount == players (${present})`]: (x) => x.results.answerCount === x.present,
        [`g${game.index}: leaderboard size == players`]: (x) => x.lb.entries.length === x.present,
        [`g${game.index}: leaderboard ids subset of game roster`]: (x) =>
          x.lb.entries.every((e) => ids.has(e.participantId)),
      },
      tags,
    );
  }

  // Pairwise-disjoint participant id sets => fully isolated rosters/scores.
  let overlaps = 0;
  for (let i = 0; i < idSets.length; i += 1) {
    for (let j = i + 1; j < idSets.length; j += 1) {
      for (const id of idSets[i].ids) if (idSets[j].ids.has(id)) overlaps += 1;
    }
  }
  if (overlaps > 0) sessionIsolationViolations.add(overlaps, { where: 'roster-overlap' });
  check({ overlaps }, { 'no participant appears in two games': (x) => x.overlaps === 0 }, tags);

  for (const game of games) {
    try {
      endGame(env, hostToken, game.gameId);
    } catch (_) {
      /* best-effort */
    }
  }
}

function durationSeconds(s) {
  const m = /^(\d+)(s|m)?$/.exec(String(s).trim());
  if (!m) return 90;
  return m[2] === 'm' ? Number(m[1]) * 60 : Number(m[1]);
}

export const handleSummary = makeHandleSummary('multiple-games');
