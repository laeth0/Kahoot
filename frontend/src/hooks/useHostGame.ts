import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { type HostGameStateResponse, hostGameService } from '../api/hostGameService.ts';
import type {
  GameParticipantResponse,
  HostQuestionResponse,
  LeaderboardResponse,
  QuestionResultsResponse,
} from '../realtime/events.ts';
import { invokeJoinAsHost } from '../realtime/gameHub.ts';
import { useGameHubConnection } from './useGameHubConnection.ts';

export function useHostGame(gameId: string | undefined) {
  const [gameState, setGameState] = useState<HostGameStateResponse | null>(null);
  const [participantsMap, setParticipantsMap] = useState<Map<string, GameParticipantResponse>>(
    new Map(),
  );
  const [isLoading, setIsLoading] = useState<boolean>(() => Boolean(gameId));
  const [error, setError] = useState<string | null>(null);
  const [isActionPending, setIsActionPending] = useState<boolean>(false);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  const pendingJoinQueueRef = useRef<GameParticipantResponse[]>([]);
  const flushTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const { connection, status: hubConnectionStatus, retry: retryHub } = useGameHubConnection(true);

  useEffect(() => {
    if (!gameId) {
      return;
    }

    let isMounted = true;
    hostGameService
      .getState(gameId)
      .then((data) => {
        if (isMounted) {
          setGameState(data);
          const pMap = new Map<string, GameParticipantResponse>();
          data.participants.forEach((p) => pMap.set(p.id, p));
          setParticipantsMap(pMap);
          setError(null);
          setIsLoading(false);
        }
      })
      .catch((err) => {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Failed to load game');
          setIsLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [gameId, refreshTrigger]);

  const flushJoinedParticipants = useCallback(() => {
    if (pendingJoinQueueRef.current.length === 0) return;

    const queued = [...pendingJoinQueueRef.current];
    pendingJoinQueueRef.current = [];

    setParticipantsMap((prev) => {
      const next = new Map(prev);
      queued.forEach((p) => {
        next.set(p.id, p);
      });
      return next;
    });
  }, []);

  useEffect(() => {
    if (!connection || hubConnectionStatus !== 'connected' || !gameId) {
      return;
    }

    let isSubscribed = true;

    invokeJoinAsHost(connection, gameId)
      .then((res) => {
        if (!res.success && isSubscribed) {
          setError(res.error?.description || 'Failed to authorize as game host.');
        }
      })
      .catch((err) => {
        if (isSubscribed) {
          setError(err instanceof Error ? err.message : 'Failed to join game hub as host.');
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
      setParticipantsMap((prev) => {
        const existing = prev.get(participantId);
        if (!existing) return prev;
        const next = new Map(prev);
        next.set(participantId, { ...existing, isConnected: false });
        return next;
      });
    };

    const handleParticipantRemoved = (participantId: string) => {
      setParticipantsMap((prev) => {
        if (!prev.has(participantId)) return prev;
        const next = new Map(prev);
        next.delete(participantId);
        return next;
      });
    };

    const handleQuestionStarted = (payload: HostQuestionResponse) => {
      setGameState((prev) =>
        prev
          ? {
              ...prev,
              status: 'QuestionActive',
              currentQuestionIndex: payload.questionIndex,
              currentQuestionStartedAt: payload.startedAt,
              currentQuestionEndsAt: payload.endsAt,
              answeredCount: 0,
            }
          : null,
      );
    };

    const handleQuestionEnded = (_payload: QuestionResultsResponse) => {
      setGameState((prev) =>
        prev
          ? {
              ...prev,
              status: 'QuestionResults',
            }
          : null,
      );
    };

    const handleLeaderboardUpdated = (_payload: LeaderboardResponse) => {
      setGameState((prev) =>
        prev
          ? {
              ...prev,
              status: 'Leaderboard',
            }
          : null,
      );
    };

    const handleGameEnded = (_payload: LeaderboardResponse) => {
      setGameState((prev) =>
        prev
          ? {
              ...prev,
              status: 'Finished',
            }
          : null,
      );
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

  const participants = useMemo(() => {
    return Array.from(participantsMap.values());
  }, [participantsMap]);

  const startGame = useCallback(async () => {
    if (!gameId || isActionPending) return;
    setIsActionPending(true);
    try {
      await hostGameService.startGame(gameId);
    } finally {
      setIsActionPending(false);
    }
  }, [gameId, isActionPending]);

  const endGame = useCallback(async () => {
    if (!gameId || isActionPending) return;
    setIsActionPending(true);
    try {
      await hostGameService.endGame(gameId);
    } finally {
      setIsActionPending(false);
    }
  }, [gameId, isActionPending]);

  const removeParticipant = useCallback(
    async (participantId: string) => {
      if (!gameId) return;
      await hostGameService.removeParticipant(gameId, participantId);
      setParticipantsMap((prev) => {
        if (!prev.has(participantId)) return prev;
        const next = new Map(prev);
        next.delete(participantId);
        return next;
      });
    },
    [gameId],
  );

  const refetch = useCallback(() => {
    setIsLoading(true);
    setRefreshTrigger((prev) => prev + 1);
  }, []);

  return {
    gameState,
    participants,
    participantCount: participants.length,
    isLoading,
    error,
    isActionPending,
    hubConnectionStatus,
    retryHub,
    refetch,
    startGame,
    endGame,
    removeParticipant,
  };
}

export default useHostGame;
