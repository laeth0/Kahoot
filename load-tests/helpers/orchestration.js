// Director-side helpers: wait for players to be present, run a question cycle,
// and verify resulting application state (not just transport acks).

import { check } from 'k6';
import { getHostState, getQuestionResults } from './rest.js';
import { delay } from './signalr.js';
import {
  lostAcceptedAnswers,
  duplicateScoreViolations,
  duplicateAnswerViolations,
  inconsistentGameState,
  duplicateParticipants,
} from './metrics.js';

// Poll the host state until at least `target` participants are registered.
export async function waitForParticipantCount(env, token, gameId, target, opts = {}) {
  const timeoutMs = opts.timeoutMs || 240000;
  const intervalMs = opts.intervalMs || 1500;
  const minFraction = opts.minFraction !== undefined ? opts.minFraction : 0.95;
  const deadline = Date.now() + timeoutMs;
  let last = 0;
  let stagnantCycles = 0;
  for (;;) {
    const state = getHostState(env, token, gameId);
    const count = state && state.participants ? state.participants.length : 0;
    if (count >= Math.ceil(target * minFraction)) {
      // Settle in-flight joins for 1.5s so late arrivals are not locked out
      await delay(1500);
      return getHostState(env, token, gameId);
    }

    if (count === last && count > 0) {
      stagnantCycles += 1;
    } else {
      stagnantCycles = 0;
      last = count;
    }

    // Only exit after deadline if no new players have joined for at least 2 intervals
    if (Date.now() >= deadline && stagnantCycles >= 2) {
      return state;
    }
    await delay(intervalMs);
  }
}

// Verify one closed question. `expected` = number of players that should have
// exactly one accepted answer. Pushes the zero-tolerance correctness metrics.
export function verifyClosedQuestion(env, token, gameId, question, expected, tag) {
  const results = getQuestionResults(env, token, gameId, question.questionId);
  const state = getHostState(env, token, gameId);

  const perChoiceSum = results.choices.reduce((runningSum, choiceOption) => runningSum + choiceOption.answerCount, 0);
  const activePlayers = state.participants.filter((participant) => !participant.isRemoved).length;

  // 1. State-integrity ("no corrupted game state"): the two independent
  //    server-side counts and the per-choice breakdown must all agree. A
  //    mismatch here is real corruption, not benign player attrition — so this,
  //    not (expected - answerCount), drives the hard metrics. Per-VU
  //    confirmation that each *accepted* answer is durably in state lives in the
  //    player flow (Reconnect -> alreadyAnsweredCurrentQuestion).
  if (perChoiceSum !== results.answerCount) {
    inconsistentGameState.add(1, { where: `${tag}:choice-sum` });
  }
  if (state.answeredCount !== results.answerCount) {
    inconsistentGameState.add(1, { where: `${tag}:answeredCount-mismatch` });
    lostAcceptedAnswers.add(Math.abs(state.answeredCount - results.answerCount), { where: `${tag}:count-mismatch` });
  }

  // 2. No duplicate accepted answers: answerCount can never exceed the number of
  //    active players (DB unique index (game, question, participant)).
  if (results.answerCount > activePlayers) {
    duplicateAnswerViolations.add(results.answerCount - activePlayers, { where: tag });
  }

  // 3. No duplicate scores: for a correct answer the award is in
  //    [ceil(points/2), points]. A score above `points` after ONE question means
  //    it was applied more than once.
  let overScoredCount = 0;
  for (const participant of state.participants) {
    if (participant.totalScore > question.points) overScoredCount += 1;
    if (participant.totalScore < 0) overScoredCount += 1;
  }
  if (overScoredCount > 0) duplicateScoreViolations.add(overScoredCount, { where: tag });

  // 4. Participant roster stable (no phantom/duplicate players).
  const participantIds = new Set(state.participants.map((participant) => participant.id));
  if (participantIds.size !== state.participants.length) {
    duplicateParticipants.add(state.participants.length - participantIds.size, { where: tag });
  }

  const attritionGrace = Math.max(1, Math.ceil(expected * 0.1));
  const missing = expected - results.answerCount;

  check(
    { results, state, overScoredCount, perChoiceSum, expected, activePlayers, missing, attritionGrace },
    {
      [`[${tag}] answerCount within reasonable attrition of players present (${expected})`]: (context) =>
        context.missing <= context.attritionGrace,
      [`[${tag}] answerCount <= active players (no duplicates)`]: (context) => context.results.answerCount <= context.activePlayers,
      [`[${tag}] per-choice counts sum to total`]: (context) => context.perChoiceSum === context.results.answerCount,
      [`[${tag}] host answeredCount matches results`]: (context) => context.state.answeredCount === context.results.answerCount,
      [`[${tag}] no score above question points`]: (context) => context.overScoredCount === 0,
    },
  );

  return { results, state, activePlayers, perChoiceSum, missing };
}
