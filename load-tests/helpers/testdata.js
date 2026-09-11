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

  for (let questionIndex = 0; questionIndex < questionCount; questionIndex += 1) {
    addQuestion(env, token, quizId, {
      text: `Load-test question ${questionIndex + 1} (${stamp})`,
      imageUrl: null,
      timeLimitSeconds,
      points,
      choices: CHOICE_LABELS.map((label, choiceIndex) => ({
        text: `Choice ${label}`,
        imageUrl: null,
        isCorrect: choiceIndex === 0, // "Choice A" is always the correct one
      })),
    });
  }

  publishQuiz(env, token, quizId);

  const quiz = getQuiz(env, token, quizId);
  const questions = quiz.questions
    .slice()
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .map((questionItem) => {
      const correctChoice = questionItem.choices.find((choice) => choice.isCorrect);
      const wrongChoice = questionItem.choices.find((choice) => !choice.isCorrect);
      return {
        questionId: questionItem.id,
        orderIndex: questionItem.orderIndex,
        timeLimitSeconds: questionItem.timeLimitSeconds,
        points: questionItem.points,
        correctChoiceId: correctChoice.id,
        wrongChoiceId: wrongChoice ? wrongChoice.id : correctChoice.id,
        choiceIds: questionItem.choices.map((choice) => choice.id),
      };
    });

  const games = [];
  for (let gameIndex = 0; gameIndex < gameCount; gameIndex += 1) {
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

  for (let gameIndex = 0; gameIndex < count; gameIndex += 1) {
    const quizId = createQuiz(env, token, `Isolation ${gameIndex + 1} ${stamp}`, `k6 isolation quiz ${gameIndex + 1}`);
    for (let questionIndex = 0; questionIndex < questionsPerQuiz; questionIndex += 1) {
      addQuestion(env, token, quizId, {
        text: `Game ${gameIndex + 1} question ${questionIndex + 1} (${stamp})`,
        imageUrl: null,
        timeLimitSeconds,
        points,
        choices: CHOICE_LABELS.map((label, choiceIndex) => ({
          text: `G${gameIndex + 1} Choice ${label}`,
          imageUrl: null,
          isCorrect: choiceIndex === 0,
        })),
      });
    }
    publishQuiz(env, token, quizId);
    const quiz = getQuiz(env, token, quizId);
    const questions = quiz.questions
      .slice()
      .sort((a, b) => a.orderIndex - b.orderIndex)
      .map((questionItem) => {
        const correctChoice = questionItem.choices.find((choice) => choice.isCorrect);
        const wrongChoice = questionItem.choices.find((choice) => !choice.isCorrect);
        return {
          questionId: questionItem.id,
          orderIndex: questionItem.orderIndex,
          points: questionItem.points,
          timeLimitSeconds: questionItem.timeLimitSeconds,
          correctChoiceId: correctChoice.id,
          wrongChoiceId: wrongChoice ? wrongChoice.id : correctChoice.id,
        };
      });
    const game = createGame(env, token, quizId);
    games.push({ index: gameIndex, quizId, gameId: game.gameId, pin: game.pin, questions, firstQuestion: questions[0] });
  }

  return { hostToken: token, hostId, stamp, games, clockSkewMs: serverClockSkewMs(env) };
}

// Globally-unique, deterministic, validator-safe nickname
// (`^[\p{L}\p{N} _.\-]+$`, length 2..30). idInTest is unique across all VUs.
export function uniqueNickname(prefix) {
  const sanitizedPrefix = (prefix || __ENV.RUN_ID || 'p').replace(/[^\p{L}\p{N}_.\-]/gu, '').slice(0, 10) || 'p';
  return `${sanitizedPrefix}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}`.slice(0, 30);
}

function clamp(value, minimum, maximum) {
  return Math.max(minimum, Math.min(maximum, value));
}
