// Repeatable test-data provisioning, entirely through the public host API
// (NFR: "Prefer setup through the application's public API"). One call builds:
//   host login -> quiz -> N questions (4 choices each, one correct) -> publish
//   -> one or more game sessions (fresh PIN each).
//
// Setup traffic is done in k6 setup()/teardown(), which is not counted in the
// scenario's measured VU load.

import exec from 'k6/execution';
import { hostCredentials } from '../config/environments.js';
import {
  login,
  createQuiz,
  addQuestion,
  publishQuiz,
  getQuiz,
  createGame,
  serverClockSkewMs,
} from './rest.js';

const CHOICE_LABELS = ['A', 'B', 'C', 'D'];

// options: { questions=1, timeLimitSeconds=120, points=1000, games=1, titlePrefix }
export function provisionGames(env, options = {}) {
  const questionCount = options.questions || 1;
  const timeLimitSeconds = clamp(options.timeLimitSeconds || 120, 5, 300);
  const points = options.points || 1000;
  const gameCount = options.games || 1;
  const stamp = `${__ENV.RUN_ID || 'run'}-${Date.now()}`;

  const { token, hostId } = login(env, hostCredentials());

  const quizId = createQuiz(env, token, `${options.titlePrefix || 'LoadTest'} ${stamp}`, 'k6 load-test quiz');

  for (let q = 0; q < questionCount; q += 1) {
    addQuestion(env, token, quizId, {
      text: `Load-test question ${q + 1} (${stamp})`,
      imageUrl: null,
      timeLimitSeconds,
      points,
      choices: CHOICE_LABELS.map((label, idx) => ({
        text: `Choice ${label}`,
        imageUrl: null,
        isCorrect: idx === 0, // "Choice A" is always the correct one
      })),
    });
  }

  publishQuiz(env, token, quizId);

  const quiz = getQuiz(env, token, quizId);
  const questions = quiz.questions
    .slice()
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .map((qq) => {
      const correct = qq.choices.find((c) => c.isCorrect);
      const wrong = qq.choices.find((c) => !c.isCorrect);
      return {
        questionId: qq.id,
        orderIndex: qq.orderIndex,
        timeLimitSeconds: qq.timeLimitSeconds,
        points: qq.points,
        correctChoiceId: correct.id,
        wrongChoiceId: wrong ? wrong.id : correct.id,
        choiceIds: qq.choices.map((c) => c.id),
      };
    });

  const games = [];
  for (let g = 0; g < gameCount; g += 1) {
    const game = createGame(env, token, quizId);
    games.push({ gameId: game.gameId, pin: game.pin });
  }

  return {
    hostToken: token,
    hostId,
    quizId,
    stamp,
    questions,
    games,
    clockSkewMs: serverClockSkewMs(env),
    // convenience aliases for the single-game scenarios
    gameId: games[0].gameId,
    pin: games[0].pin,
    firstQuestion: questions[0],
  };
}

// Like provisionGames, but every game gets its OWN quiz (hence its own distinct
// questionId / choiceIds). Used by the isolation scenario so a foreign event is
// unambiguously identifiable by questionId.
// options: { count, timeLimitSeconds=60, points=1000, questionsPerQuiz=1 }
export function provisionPerGameQuizzes(env, options = {}) {
  const count = options.count || 10;
  const timeLimitSeconds = clamp(options.timeLimitSeconds || 60, 5, 300);
  const points = options.points || 1000;
  const questionsPerQuiz = options.questionsPerQuiz || 1;
  const stamp = `${__ENV.RUN_ID || 'run'}-${Date.now()}`;

  const { token, hostId } = login(env, hostCredentials());
  const games = [];

  for (let i = 0; i < count; i += 1) {
    const quizId = createQuiz(env, token, `Isolation ${i + 1} ${stamp}`, `k6 isolation quiz ${i + 1}`);
    for (let q = 0; q < questionsPerQuiz; q += 1) {
      addQuestion(env, token, quizId, {
        text: `Game ${i + 1} question ${q + 1} (${stamp})`,
        imageUrl: null,
        timeLimitSeconds,
        points,
        choices: CHOICE_LABELS.map((label, idx) => ({
          text: `G${i + 1} Choice ${label}`,
          imageUrl: null,
          isCorrect: idx === 0,
        })),
      });
    }
    publishQuiz(env, token, quizId);
    const quiz = getQuiz(env, token, quizId);
    const questions = quiz.questions
      .slice()
      .sort((a, b) => a.orderIndex - b.orderIndex)
      .map((qq) => {
        const correct = qq.choices.find((c) => c.isCorrect);
        const wrong = qq.choices.find((c) => !c.isCorrect);
        return {
          questionId: qq.id,
          orderIndex: qq.orderIndex,
          points: qq.points,
          timeLimitSeconds: qq.timeLimitSeconds,
          correctChoiceId: correct.id,
          wrongChoiceId: wrong ? wrong.id : correct.id,
        };
      });
    const game = createGame(env, token, quizId);
    games.push({ index: i, quizId, gameId: game.gameId, pin: game.pin, questions, firstQuestion: questions[0] });
  }

  return { hostToken: token, hostId, stamp, games, clockSkewMs: serverClockSkewMs(env) };
}

// Globally-unique, deterministic, validator-safe nickname
// (`^[\p{L}\p{N} _.\-]+$`, length 2..30). idInTest is unique across all VUs.
export function uniqueNickname(prefix) {
  const p = (prefix || __ENV.RUN_ID || 'p').replace(/[^\p{L}\p{N}_.\-]/gu, '').slice(0, 10) || 'p';
  return `${p}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}`.slice(0, 30);
}

function clamp(v, lo, hi) {
  return Math.max(lo, Math.min(hi, v));
}
