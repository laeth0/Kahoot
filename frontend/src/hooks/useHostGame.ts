import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { ApiError } from '../api/axiosClient.ts';
import { hostGameService, type HostGameStateResponse } from '../api/hostGameService.ts';
import { getFriendlyErrorMessage } from '../constants/errorCodes.ts';
import { normalizeGameStatus } from '../constants/gameStatus.ts';
import type {
  GameParticipantResponse,
  HostQuestionResponse,
  LeaderboardResponse,
  QuestionResultsResponse,
} from '../realtime/events.ts';
import { invokeJoinAsHost } from '../realtime/gameHub.ts';
import { useGameHubConnection } from './useGameHubConnection.ts';
import { getHostQuestion, saveHostQuestion } from './useSessionToken.ts';

const ANSWERED_POLL_INTERVAL_MS = 2000;

function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return getFriendlyErrorMessage(error.code ?? error.message, fallback);
  }
  if (error instanceof Error) {
    return error.message;
  }
  return fallback;
}

export function useHostGame(gameId: string | undefined) {
  const [gameState, setGameState] = useState<HostGameStateResponse | null>(null);
  const [participantsMap, setParticipantsMap] = useState<Map<string, GameParticipantResponse>>(
    new Map(),
  );
  const [currentQuestion, setCurrentQuestion] = useState<HostQuestionResponse | null>(() =>
    gameId ? getHostQuestion(gameId) : null,
  );
  const [questionResults, setQuestionResults] = useState<QuestionResultsResponse | null>(null);
  const [leaderboard, setLeaderboard] = useState<LeaderboardResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(() => Boolean(gameId));
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [isActionPending, setIsActionPending] = useState<boolean>(false);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  const pendingJoinQueueRef = useRef<GameParticipantResponse[]>([]);
  const flushTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hasSubscribedRef = useRef(false);
  const actionInFlightRef = useRef(false);

  const { connection, status: hubConnectionStatus, retry: retryHub } = useGameHubConnection(true);

  useEffect(() => {
    if (!gameId) {
      return;
    }

    let isMounted = true;
    hostGameService
      .getState(gameId)
      .then((data) => {
        if (!isMounted) {
          return;
        }
        setGameState({ ...data, status: normalizeGameStatus(data.status) });
        const nextMap = new Map<string, GameParticipantResponse>();
        data.participants.forEach((participant) => nextMap.set(participant.id, participant));
        setParticipantsMap(nextMap);
        setError(null);
        setIsLoading(false);
      })
      .catch((err) => {
        if (isMounted) {
          setError(describeError(err, 'Failed to load game'));
          setIsLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [gameId, refreshTrigger]);

  const flushJoinedParticipants = useCallback(() => {
    if (pendingJoinQueueRef.current.length === 0) {
      return;
    }
    const queued = [...pendingJoinQueueRef.current];
    pendingJoinQueueRef.current = [];
    setParticipantsMap((previous) => {
      const next = new Map(previous);
      queued.forEach((participant) => next.set(participant.id, participant));
      return next;
    });
  }, []);

  useEffect(() => {
    if (!connection || hubConnectionStatus !== 'connected' || !gameId) {
      return;
    }

    let isSubscribed = true;
    const wasSubscribedBefore = hasSubscribedRef.current;
    hasSubscribedRef.current = true;

    invokeJoinAsHost(connection, gameId)
      .then((response) => {
        if (!isSubscribed) {
          return;
        }
        if (!response.success) {
          setError(response.error?.description || 'Failed to authorize as game host.');
          return;
        }
        if (wasSubscribedBefore) {
          setRefreshTrigger((value) => value + 1);
        }
      })
      .catch((err) => {
        if (isSubscribed) {
          setError(describeError(err, 'Failed to join game hub as host.'));
        }
      });

    const handleParticipantJoined = (participant: GameParticipantResponse) => {
      pendingJoinQueueRef.current.push(participant);
      if (!flushTimeoutRef.current) {
        flushTimeoutRef.current = setTimeout(() => {
          flushTimeoutRef.current = null;
          flushJoinedParticipants();
        }, 150);
      }
    };

    const handleParticipantLeft = (participantId: string) => {
      setParticipantsMap((previous) => {
        const existing = previous.get(participantId);
        if (!existing) {
          return previous;
        }
        const next = new Map(previous);
        next.set(participantId, { ...existing, isConnected: false });
        return next;
      });
    };

    const handleParticipantRemoved = (participantId: string) => {
      setParticipantsMap((previous) => {
        if (!previous.has(participantId)) {
          return previous;
        }
        const next = new Map(previous);
        next.delete(participantId);
        return next;
      });
    };

    const handleQuestionStarted = (payload: HostQuestionResponse) => {
      setActionError(null);
      setCurrentQuestion(payload);
      setQuestionResults(null);
      saveHostQuestion(gameId, payload);
      setGameState((previous) =>
        previous
          ? {
              ...previous,
              status: 'QuestionActive',
              currentQuestionIndex: payload.questionIndex,
              currentQuestionStartedAt: payload.startedAt,
              currentQuestionEndsAt: payload.endsAt,
              answeredCount: 0,
            }
          : previous,
      );
    };

    const handleQuestionEnded = (payload: QuestionResultsResponse) => {
      setQuestionResults(payload);
      setGameState((previous) =>
        previous ? { ...previous, status: 'QuestionResults' } : previous,
      );
    };

    const handleLeaderboardUpdated = (payload: LeaderboardResponse) => {
      setLeaderboard(payload);
      setGameState((previous) => (previous ? { ...previous, status: 'Leaderboard' } : previous));
    };

    const handleGameEnded = (payload: LeaderboardResponse) => {
      setLeaderboard(payload);
      setGameState((previous) => (previous ? { ...previous, status: 'Finished' } : previous));
    };

    connection.on('ParticipantJoined', handleParticipantJoined);
    connection.on('ParticipantLeft', handleParticipantLeft);
    connection.on('ParticipantRemoved', handleParticipantRemoved);
    connection.on('QuestionStartedForHost', handleQuestionStarted);
    connection.on('QuestionEnded', handleQuestionEnded);
    connection.on('LeaderboardUpdated', handleLeaderboardUpdated);
    connection.on('GameEnded', handleGameEnded);

    return () => {
      isSubscribed = false;
      connection.off('ParticipantJoined', handleParticipantJoined);
      connection.off('ParticipantLeft', handleParticipantLeft);
      connection.off('ParticipantRemoved', handleParticipantRemoved);
      connection.off('QuestionStartedForHost', handleQuestionStarted);
      connection.off('QuestionEnded', handleQuestionEnded);
      connection.off('LeaderboardUpdated', handleLeaderboardUpdated);
      connection.off('GameEnded', handleGameEnded);
      if (flushTimeoutRef.current) {
        clearTimeout(flushTimeoutRef.current);
        flushTimeoutRef.current = null;
      }
    };
  }, [connection, hubConnectionStatus, gameId, flushJoinedParticipants]);

  useEffect(() => {
    if (!gameId || normalizeGameStatus(gameState?.status ?? 'Lobby') !== 'QuestionActive') {
      return;
    }

    let active = true;
    const interval = setInterval(() => {
      hostGameService
        .getState(gameId)
        .then((data) => {
          if (active) {
            setGameState((previous) =>
              previous ? { ...previous, answeredCount: data.answeredCount } : previous,
            );
          }
        })
        .catch(() => {});
    }, ANSWERED_POLL_INTERVAL_MS);

    return () => {
      active = false;
      clearInterval(interval);
    };
  }, [gameId, gameState?.status]);

  useEffect(() => {
    const phase = normalizeGameStatus(gameState?.status ?? 'Lobby');
    if (!gameId || phase !== 'QuestionResults' || questionResults || !currentQuestion) {
      return;
    }
    let active = true;
    hostGameService
      .getQuestionResults(gameId, currentQuestion.questionId)
      .then((data) => {
        if (active) {
          setQuestionResults(data);
        }
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [gameId, gameState?.status, questionResults, currentQuestion]);

  const participants = useMemo(() => Array.from(participantsMap.values()), [participantsMap]);

  const runAction = useCallback(async (action: () => Promise<unknown>, fallbackMessage: string) => {
    if (actionInFlightRef.current) {
      return undefined;
    }
    actionInFlightRef.current = true;
    setIsActionPending(true);
    setActionError(null);
    try {
      return await action();
    } catch (err) {
      setActionError(describeError(err, fallbackMessage));
      return undefined;
    } finally {
      actionInFlightRef.current = false;
      setIsActionPending(false);
    }
  }, []);

  const startGame = useCallback(async () => {
    if (!gameId) {
      return;
    }
    const result = await runAction(
      () => hostGameService.startGame(gameId),
      'Failed to start the game.',
    );
    if (result && typeof result === 'object' && 'host' in result) {
      const host = (result as { host: HostQuestionResponse }).host;
      setCurrentQuestion(host);
      saveHostQuestion(gameId, host);
    }
  }, [gameId, runAction]);

  const advanceQuestion = useCallback(async () => {
    if (!gameId) {
      return;
    }
    const result = await runAction(
      () => hostGameService.advance(gameId),
      'Failed to load the next question.',
    );
    if (result && typeof result === 'object' && 'host' in result) {
      const host = (result as { host: HostQuestionResponse }).host;
      setCurrentQuestion(host);
      saveHostQuestion(gameId, host);
    }
  }, [gameId, runAction]);

  const endQuestion = useCallback(async () => {
    if (!gameId) {
      return;
    }
    const result = await runAction(
      () => hostGameService.endQuestion(gameId),
      'Failed to end the question.',
    );
    if (result) {
      setQuestionResults(result as QuestionResultsResponse);
    }
  }, [gameId, runAction]);

  const showLeaderboard = useCallback(async () => {
    if (!gameId) {
      return;
    }
    await runAction(
      () => hostGameService.showLeaderboard(gameId),
      'Failed to show the leaderboard.',
    );
  }, [gameId, runAction]);

  const endGame = useCallback(async () => {
    if (!gameId) {
      return;
    }
    await runAction(() => hostGameService.endGame(gameId), 'Failed to end the game.');
  }, [gameId, runAction]);

  const removeParticipant = useCallback(
    async (participantId: string) => {
      if (!gameId) {
        return;
      }
      await hostGameService.removeParticipant(gameId, participantId);
      setParticipantsMap((previous) => {
        if (!previous.has(participantId)) {
          return previous;
        }
        const next = new Map(previous);
        next.delete(participantId);
        return next;
      });
    },
    [gameId],
  );

  const refetch = useCallback(() => {
    setIsLoading(true);
    setRefreshTrigger((value) => value + 1);
  }, []);

  const dismissActionError = useCallback(() => setActionError(null), []);

  return {
    gameState,
    participants,
    participantCount: participants.length,
    currentQuestion,
    questionResults,
    leaderboard,
    isLoading,
    error,
    actionError,
    isActionPending,
    hubConnectionStatus,
    retryHub,
    refetch,
    dismissActionError,
    startGame,
    advanceQuestion,
    endQuestion,
    showLeaderboard,
    endGame,
    removeParticipant,
  };
}

export default useHostGame;
