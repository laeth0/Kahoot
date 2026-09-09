import { useCallback, useEffect, useRef, useState } from 'react';

import { getFriendlyErrorMessage } from '../constants/errorCodes.ts';
import { type NormalizedGameStatus, normalizeGameStatus } from '../constants/gameStatus.ts';
import type {
  LeaderboardResponse,
  PlayerGameStateResponse,
  PlayerQuestionResponse,
  QuestionResultsResponse,
} from '../realtime/events.ts';
import { invokeReconnect, invokeSubmitAnswer } from '../realtime/gameHub.ts';
import { useGameHubConnection } from './useGameHubConnection.ts';
import { useSessionToken } from './useSessionToken.ts';

const REMOVED_CODE = 'Game.ParticipantRemoved';
const INVALID_SESSION_CODE = 'Game.InvalidSessionToken';
const TOO_LATE_CODES = new Set(['Game.QuestionClosed', 'Game.QuestionNotActive']);
const MISSING_GAME_MESSAGE = 'This game link is missing its session id.';
const MISSING_SESSION_MESSAGE = "We couldn't find your player session for this game.";

export type AnswerState =
  'idle' | 'submitting' | 'accepted' | 'alreadyAnswered' | 'tooLate' | 'rejected' | 'slowDown';

export interface PlayerState {
  nickname: string;
  status: NormalizedGameStatus;
  participantId: string;
  totalScore: number;
  rank: number | null;
  participantCount: number;
  currentQuestion: PlayerQuestionResponse | null;
  alreadyAnswered: boolean;
  lastResults: QuestionResultsResponse | null;
  leaderboard: LeaderboardResponse | null;
}

interface LiveRefs {
  participantId: string | null;
  totalScore: number;
}

function myLeaderboardEntry(board: LeaderboardResponse, participantId: string) {
  return board.entries.find((entry) => entry.participantId === participantId) ?? null;
}

export function usePlayerGame(gameId: string | undefined) {
  const { session, clear } = useSessionToken(gameId);
  const sessionToken = session?.sessionToken ?? null;

  const { connection, status: hubStatus, retry: retryHub } = useGameHubConnection(false);

  const [playerState, setPlayerState] = useState<PlayerState | null>(null);
  const [isKicked, setIsKicked] = useState(false);
  const [hydrateError, setHydrateError] = useState<string | null>(null);
  const [answerState, setAnswerState] = useState<AnswerState>('idle');
  const [selectedChoiceId, setSelectedChoiceId] = useState<string | null>(null);
  const [scoreBeforeQuestion, setScoreBeforeQuestion] = useState(0);

  const liveRef = useRef<LiveRefs>({ participantId: null, totalScore: 0 });
  const submitInFlightRef = useRef(false);

  useEffect(() => {
    liveRef.current = {
      participantId: playerState?.participantId ?? null,
      totalScore: playerState?.totalScore ?? 0,
    };
  }, [playerState?.participantId, playerState?.totalScore]);

  const applyState = useCallback((data: PlayerGameStateResponse) => {
    const status = normalizeGameStatus(data.status);
    setScoreBeforeQuestion(data.totalScore);
    setPlayerState((previous) => ({
      nickname: data.nickname,
      status,
      participantId: data.participantId,
      totalScore: data.totalScore,
      rank: data.rank ?? null,
      participantCount: previous?.participantCount ?? 1,
      currentQuestion: data.currentQuestion ?? null,
      alreadyAnswered: data.alreadyAnsweredCurrentQuestion,
      lastResults: data.lastQuestionResults ?? null,
      leaderboard: data.leaderboard ?? null,
    }));
    setHydrateError(null);
    setSelectedChoiceId(null);
    setAnswerState(
      status === 'QuestionActive' && data.alreadyAnsweredCurrentQuestion
        ? 'alreadyAnswered'
        : 'idle',
    );
  }, []);

  useEffect(() => {
    if (!connection || hubStatus !== 'connected' || !sessionToken || isKicked) {
      return;
    }

    let active = true;

    const handleKicked = () => {
      setIsKicked(true);
      connection.stop().catch(() => {});
    };

    const hydrate = async () => {
      try {
        const response = await invokeReconnect(connection, sessionToken);
        if (!active) {
          return;
        }
        if (response.success && response.data) {
          applyState(response.data);
          return;
        }
        const code = response.error?.code;
        if (code === REMOVED_CODE) {
          handleKicked();
          return;
        }
        if (code === INVALID_SESSION_CODE) {
          clear();
          return;
        }
        setHydrateError(getFriendlyErrorMessage(code, 'Unable to rejoin the game.'));
      } catch {
        if (active) {
          setHydrateError('Lost connection while joining the game lobby.');
        }
      }
    };

    const handleParticipantJoined = () => {
      setPlayerState((previous) =>
        previous ? { ...previous, participantCount: previous.participantCount + 1 } : previous,
      );
    };

    const handleParticipantRemoved = (participantId: string) => {
      if (participantId === liveRef.current.participantId) {
        handleKicked();
        return;
      }
      setPlayerState((previous) =>
        previous
          ? { ...previous, participantCount: Math.max(1, previous.participantCount - 1) }
          : previous,
      );
    };

    const handleQuestionStarted = (question: PlayerQuestionResponse) => {
      submitInFlightRef.current = false;
      setScoreBeforeQuestion(liveRef.current.totalScore);
      setSelectedChoiceId(null);
      setAnswerState('idle');
      setPlayerState((previous) =>
        previous
          ? {
              ...previous,
              status: 'QuestionActive',
              currentQuestion: question,
              alreadyAnswered: false,
              lastResults: null,
            }
          : previous,
      );
    };

    const handleQuestionEnded = (results: QuestionResultsResponse) => {
      setAnswerState('idle');
      setPlayerState((previous) =>
        previous ? { ...previous, status: 'QuestionResults', lastResults: results } : previous,
      );
    };

    const handleLeaderboardUpdated = (board: LeaderboardResponse) => {
      setPlayerState((previous) => {
        if (!previous) {
          return previous;
        }
        const mine = myLeaderboardEntry(board, previous.participantId);
        return {
          ...previous,
          status: 'Leaderboard',
          leaderboard: board,
          totalScore: mine?.totalScore ?? previous.totalScore,
          rank: mine?.rank ?? previous.rank,
        };
      });
    };

    const handleGameEnded = (board: LeaderboardResponse) => {
      setPlayerState((previous) => {
        if (!previous) {
          return previous;
        }
        const mine = myLeaderboardEntry(board, previous.participantId);
        return {
          ...previous,
          status: 'Finished',
          leaderboard: board,
          totalScore: mine?.totalScore ?? previous.totalScore,
          rank: mine?.rank ?? previous.rank,
        };
      });
    };

    connection.on('ParticipantJoined', handleParticipantJoined);
    connection.on('ParticipantRemoved', handleParticipantRemoved);
    connection.on('QuestionStarted', handleQuestionStarted);
    connection.on('QuestionEnded', handleQuestionEnded);
    connection.on('LeaderboardUpdated', handleLeaderboardUpdated);
    connection.on('GameEnded', handleGameEnded);

    void hydrate();

    return () => {
      active = false;
      connection.off('ParticipantJoined', handleParticipantJoined);
      connection.off('ParticipantRemoved', handleParticipantRemoved);
      connection.off('QuestionStarted', handleQuestionStarted);
      connection.off('QuestionEnded', handleQuestionEnded);
      connection.off('LeaderboardUpdated', handleLeaderboardUpdated);
      connection.off('GameEnded', handleGameEnded);
    };
  }, [connection, hubStatus, sessionToken, isKicked, applyState, clear]);

  const activeQuestionId = playerState?.currentQuestion?.questionId ?? null;

  const submitAnswer = useCallback(
    async (choiceId: string) => {
      if (!connection || !activeQuestionId || submitInFlightRef.current) {
        return;
      }
      submitInFlightRef.current = true;
      setSelectedChoiceId(choiceId);
      setAnswerState('submitting');
      try {
        const response = await invokeSubmitAnswer(connection, activeQuestionId, choiceId);
        if (response.success && response.data?.accepted) {
          setAnswerState(response.data.alreadyAnswered ? 'alreadyAnswered' : 'accepted');
          setPlayerState((previous) =>
            previous ? { ...previous, alreadyAnswered: true } : previous,
          );
          return;
        }
        const code = response.error?.code;
        if (code === REMOVED_CODE) {
          setIsKicked(true);
          connection.stop().catch(() => {});
          return;
        }
        if (code && TOO_LATE_CODES.has(code)) {
          setAnswerState('tooLate');
          return;
        }
        if (code === 'Game.TooManyAnswerAttempts') {
          setAnswerState('slowDown');
          return;
        }
        setAnswerState('rejected');
      } catch {
        setAnswerState('rejected');
      } finally {
        submitInFlightRef.current = false;
      }
    },
    [connection, activeQuestionId],
  );

  const leaveGame = useCallback(async () => {
    clear();
    if (connection) {
      await connection.stop().catch(() => {});
    }
  }, [clear, connection]);

  const error = !gameId
    ? MISSING_GAME_MESSAGE
    : !sessionToken
      ? MISSING_SESSION_MESSAGE
      : hydrateError;

  const missingSession = !gameId || !sessionToken;
  const isLoading = !missingSession && !isKicked && hydrateError === null && playerState === null;

  const pointsThisQuestion =
    playerState &&
    (playerState.status === 'QuestionResults' ||
      playerState.status === 'Leaderboard' ||
      playerState.status === 'Finished')
      ? Math.max(0, playerState.totalScore - scoreBeforeQuestion)
      : null;

  return {
    playerState,
    isKicked,
    isLoading,
    error,
    hubStatus,
    retryHub,
    leaveGame,
    submitAnswer,
    answerState,
    selectedChoiceId,
    pointsThisQuestion,
  };
}

export default usePlayerGame;
