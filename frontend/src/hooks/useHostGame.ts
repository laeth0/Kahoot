import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { ApiError, isApiRequestCanceled } from '../api/axiosClient.ts';
import { hostGameService, type HostGameStateResponse } from '../api/hostGameService.ts';
import { getFriendlyErrorMessage } from '../constants/errorCodes.ts';
import { normalizeGameStatus } from '../constants/gameStatus.ts';
import type {
  GameDataSyncState,
  GameParticipantResponse,
  HostQuestionResponse,
  LeaderboardResponse,
  ParticipantPresenceResponse,
  QuestionResultsResponse,
} from '../realtime/events.ts';
import { invokeJoinAsHost } from '../realtime/gameHub.ts';
import { useGameHubConnection } from './useGameHubConnection.ts';
import { getHostQuestion, saveHostQuestion } from './useSessionToken.ts';

const ANSWERED_POLL_INTERVAL_MS = 2000;
const MAX_SYNC_RETRY_DELAY_MS = 15000;
const STALE_FAILURE_THRESHOLD = 2;

function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return getFriendlyErrorMessage(error.code ?? error.message, fallback);
  }
  if (error instanceof Error) {
    return error.message;
  }
  return fallback;
}

function getSyncRetryDelay(failureCount: number): number {
  return Math.min(
    ANSWERED_POLL_INTERVAL_MS * 2 ** Math.max(0, failureCount - 1),
    MAX_SYNC_RETRY_DELAY_MS,
  );
}

export function useHostGame(gameId: string | undefined) {
  const [gameState, setGameState] = useState<HostGameStateResponse | null>(null);
  const [participantsMap, setParticipantsMap] = useState<Map<string, GameParticipantResponse>>(
    new Map(),
  );
  const [participantCount, setParticipantCount] = useState(0);
  const [currentQuestion, setCurrentQuestion] = useState<HostQuestionResponse | null>(() =>
    gameId ? getHostQuestion(gameId) : null,
  );
  const [questionResults, setQuestionResults] = useState<QuestionResultsResponse | null>(null);
  const [leaderboard, setLeaderboard] = useState<LeaderboardResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(() => Boolean(gameId));
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [isActionPending, setIsActionPending] = useState(false);
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  const [syncRetryTrigger, setSyncRetryTrigger] = useState(0);
  const [dataSyncState, setDataSyncState] = useState<GameDataSyncState>({
    status: 'current',
    message: null,
    lastSuccessfulAt: null,
  });
  const [liveAnnouncement, setLiveAnnouncement] = useState<{
    message: string;
    politeness: 'polite' | 'assertive';
  } | null>(null);

  const pendingJoinQueueRef = useRef<GameParticipantResponse[]>([]);
  const flushTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hasSubscribedRef = useRef(false);
  const actionInFlightRef = useRef(false);
  const presenceVersionRef = useRef(0);
  const gameStateRef = useRef<HostGameStateResponse | null>(null);
  const stateRequestRef = useRef<Promise<HostGameStateResponse> | null>(null);
  const stateAbortControllerRef = useRef<AbortController | null>(null);
  const isMountedRef = useRef(true);

  const { connection, status: hubConnectionStatus, retry: retryHub } = useGameHubConnection(true);
  const prevHubStatusRef = useRef(hubConnectionStatus);

  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
      stateAbortControllerRef.current?.abort();
    };
  }, []);

  const markSyncFailure = useCallback((failureCount: number, message: string) => {
    if (failureCount < STALE_FAILURE_THRESHOLD) {
      return;
    }
    setDataSyncState((previous) => ({ ...previous, status: 'stale', message }));
  }, []);

  const applyAuthoritativeState = useCallback((data: HostGameStateResponse) => {
    if (data.presenceVersion < presenceVersionRef.current) {
      throw new Error('Live state is still synchronizing.');
    }
    const normalized = { ...data, status: normalizeGameStatus(data.status) };
    const nextMap = new Map<string, GameParticipantResponse>();
    data.participants.forEach((participant) => nextMap.set(participant.id, participant));

    gameStateRef.current = normalized;
    presenceVersionRef.current = data.presenceVersion;
    setGameState(normalized);
    setParticipantsMap(nextMap);
    setParticipantCount(data.participantCount);
    setError(null);
    setDataSyncState({ status: 'current', message: null, lastSuccessfulAt: Date.now() });
    return normalized;
  }, []);

  const synchronizeState = useCallback((): Promise<HostGameStateResponse> => {
    if (!gameId) {
      return Promise.reject(new Error('Game id is required.'));
    }
    if (stateRequestRef.current) {
      return stateRequestRef.current;
    }

    const controller = new AbortController();
    stateAbortControllerRef.current = controller;
    const request = hostGameService
      .getState(gameId, controller.signal)
      .then((data) => {
        if (isMountedRef.current) {
          applyAuthoritativeState(data);
        }
        return data;
      })
      .finally(() => {
        if (stateRequestRef.current === request) {
          stateRequestRef.current = null;
        }
        if (stateAbortControllerRef.current === controller) {
          stateAbortControllerRef.current = null;
        }
      });
    stateRequestRef.current = request;
    return request;
  }, [applyAuthoritativeState, gameId]);

  useEffect(() => {
    if (prevHubStatusRef.current === 'connected' && hubConnectionStatus === 'reconnecting') {
      setLiveAnnouncement({
        message: 'Connection interrupted. Reconnecting to game hub...',
        politeness: 'polite',
      });
    } else if (prevHubStatusRef.current === 'reconnecting' && hubConnectionStatus === 'connected') {
      setLiveAnnouncement({ message: 'Reconnected to game hub.', politeness: 'polite' });
    }
    prevHubStatusRef.current = hubConnectionStatus;
  }, [hubConnectionStatus]);

  useEffect(() => {
    if (!gameId) {
      return;
    }

    let active = true;
    let timeout: ReturnType<typeof setTimeout> | null = null;
    let failureCount = 0;

    const loadState = async () => {
      try {
        await synchronizeState();
        failureCount = 0;
      } catch (requestError) {
        if (!active || isApiRequestCanceled(requestError)) {
          return;
        }
        failureCount += 1;
        if (gameStateRef.current) {
          markSyncFailure(
            failureCount,
            'Game state could not be refreshed. The last known state is still shown.',
          );
        } else {
          setError(describeError(requestError, 'Failed to load game'));
        }
        timeout = setTimeout(() => void loadState(), getSyncRetryDelay(failureCount));
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    };

    void loadState();

    return () => {
      active = false;
      if (timeout) {
        clearTimeout(timeout);
      }
      const controller = stateAbortControllerRef.current;
      stateAbortControllerRef.current = null;
      stateRequestRef.current = null;
      controller?.abort();
    };
  }, [gameId, refreshTrigger, synchronizeState, markSyncFailure]);

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

  const applyQuestionStarted = useCallback(
    (host: HostQuestionResponse) => {
      setActionError(null);
      setCurrentQuestion(host);
      setQuestionResults(null);
      if (gameId) {
        saveHostQuestion(gameId, host);
      }
      setGameState((previous) => {
        if (!previous) {
          return previous;
        }
        const next = {
          ...previous,
          status: 'QuestionActive' as const,
          currentQuestionIndex: host.questionIndex,
          currentQuestionStartedAt: host.startedAt,
          currentQuestionEndsAt: host.endsAt,
          answeredCount: 0,
        };
        gameStateRef.current = next;
        return next;
      });
    },
    [gameId],
  );

  useEffect(() => {
    if (!connection || hubConnectionStatus !== 'connected' || !gameId) {
      return;
    }

    let isSubscribed = true;
    const wasSubscribedBefore = hasSubscribedRef.current;
    hasSubscribedRef.current = true;
    const jitter = wasSubscribedBefore ? Math.floor(Math.random() * 250) : 0;

    const subscribeTimer = setTimeout(() => {
      void invokeJoinAsHost(connection, gameId)
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
        .catch((requestError: unknown) => {
          if (isSubscribed) {
            setError(describeError(requestError, 'Failed to join game hub as host.'));
          }
        });
    }, jitter);

    const handleParticipantPresenceChanged = (payload: ParticipantPresenceResponse) => {
      const currentVersion = presenceVersionRef.current;
      if (payload.presenceVersion <= currentVersion) {
        return;
      }
      if (payload.presenceVersion !== currentVersion + 1) {
        setDataSyncState((previous) => ({
          ...previous,
          status: 'stale',
          message: 'A live participant update was missed. Synchronizing authoritative state.',
        }));
        setRefreshTrigger((value) => value + 1);
        return;
      }

      presenceVersionRef.current = payload.presenceVersion;
      setParticipantCount(payload.participantCount);
      setGameState((previous) => {
        if (!previous) {
          return previous;
        }
        const next = {
          ...previous,
          participantCount: payload.participantCount,
          presenceVersion: payload.presenceVersion,
        };
        gameStateRef.current = next;
        return next;
      });

      const participant = payload.participant;
      if (payload.reason === 'Joined' || payload.reason === 'Reconnected') {
        setLiveAnnouncement({
          message:
            payload.reason === 'Joined'
              ? `Player ${participant.nickname} joined.`
              : `Player ${participant.nickname} reconnected.`,
          politeness: 'polite',
        });
        pendingJoinQueueRef.current = [
          ...pendingJoinQueueRef.current.filter((queued) => queued.id !== participant.id),
          participant,
        ];
        if (!flushTimeoutRef.current) {
          flushTimeoutRef.current = setTimeout(() => {
            flushTimeoutRef.current = null;
            flushJoinedParticipants();
          }, 150);
        }
        return;
      }

      pendingJoinQueueRef.current = pendingJoinQueueRef.current.filter(
        (queued) => queued.id !== participant.id,
      );
      setLiveAnnouncement({
        message: payload.reason === 'Removed' ? 'A player was removed.' : 'A player disconnected.',
        politeness: 'polite',
      });
      setParticipantsMap((previous) => {
        if (payload.reason === 'Removed') {
          if (!previous.has(participant.id)) {
            return previous;
          }
          const next = new Map(previous);
          next.delete(participant.id);
          return next;
        }
        const existing = previous.get(participant.id);
        if (!existing) {
          return previous;
        }
        const next = new Map(previous);
        next.set(participant.id, participant);
        return next;
      });
    };

    const handleQuestionStarted = (payload: HostQuestionResponse) => {
      applyQuestionStarted(payload);
      setLiveAnnouncement({
        message: `Question ${payload.questionIndex} started.`,
        politeness: 'polite',
      });
    };

    const handleQuestionEnded = (payload: QuestionResultsResponse) => {
      setQuestionResults(payload);
      setLiveAnnouncement({ message: 'Question ended. Results are in.', politeness: 'polite' });
      setGameState((previous) => {
        if (!previous) {
          return previous;
        }
        const next = { ...previous, status: 'QuestionResults' as const };
        gameStateRef.current = next;
        return next;
      });
    };

    const handleLeaderboardUpdated = (payload: LeaderboardResponse) => {
      setLeaderboard(payload);
      setLiveAnnouncement({ message: 'Leaderboard updated.', politeness: 'polite' });
      setGameState((previous) => {
        if (!previous) {
          return previous;
        }
        const next = { ...previous, status: 'Leaderboard' as const };
        gameStateRef.current = next;
        return next;
      });
    };

    const handleGameEnded = (payload: LeaderboardResponse) => {
      setLeaderboard(payload);
      setLiveAnnouncement({
        message: 'Game session finished. Final results ready.',
        politeness: 'polite',
      });
      setGameState((previous) => {
        if (!previous) {
          return previous;
        }
        const next = { ...previous, status: 'Finished' as const };
        gameStateRef.current = next;
        return next;
      });
    };

    connection.on('ParticipantPresenceChanged', handleParticipantPresenceChanged);
    connection.on('QuestionStartedForHost', handleQuestionStarted);
    connection.on('QuestionEnded', handleQuestionEnded);
    connection.on('LeaderboardUpdated', handleLeaderboardUpdated);
    connection.on('GameEnded', handleGameEnded);

    return () => {
      isSubscribed = false;
      clearTimeout(subscribeTimer);
      connection.off('ParticipantPresenceChanged', handleParticipantPresenceChanged);
      connection.off('QuestionStartedForHost', handleQuestionStarted);
      connection.off('QuestionEnded', handleQuestionEnded);
      connection.off('LeaderboardUpdated', handleLeaderboardUpdated);
      connection.off('GameEnded', handleGameEnded);
      if (flushTimeoutRef.current) {
        clearTimeout(flushTimeoutRef.current);
        flushTimeoutRef.current = null;
      }
      pendingJoinQueueRef.current = [];
    };
  }, [connection, hubConnectionStatus, gameId, flushJoinedParticipants, applyQuestionStarted]);

  useEffect(() => {
    if (!gameId || normalizeGameStatus(gameState?.status ?? 'Lobby') !== 'QuestionActive') {
      return;
    }

    let active = true;
    let timeout: ReturnType<typeof setTimeout> | null = null;
    let failureCount = 0;

    const poll = async () => {
      try {
        await synchronizeState();
        failureCount = 0;
      } catch (requestError) {
        if (!active || isApiRequestCanceled(requestError)) {
          return;
        }
        failureCount += 1;
        markSyncFailure(
          failureCount,
          'Answer progress may be out of date. The last known count is still shown.',
        );
      }

      if (active) {
        const delay =
          failureCount === 0 ? ANSWERED_POLL_INTERVAL_MS : getSyncRetryDelay(failureCount);
        timeout = setTimeout(() => void poll(), delay);
      }
    };

    timeout = setTimeout(() => void poll(), ANSWERED_POLL_INTERVAL_MS);
    return () => {
      active = false;
      if (timeout) {
        clearTimeout(timeout);
      }
      const controller = stateAbortControllerRef.current;
      stateAbortControllerRef.current = null;
      stateRequestRef.current = null;
      controller?.abort();
    };
  }, [gameId, gameState?.status, markSyncFailure, synchronizeState]);

  useEffect(() => {
    const phase = normalizeGameStatus(gameState?.status ?? 'Lobby');
    if (!gameId || phase !== 'QuestionResults' || questionResults || !currentQuestion) {
      return;
    }

    let active = true;
    let timeout: ReturnType<typeof setTimeout> | null = null;
    let controller: AbortController | null = null;
    let failureCount = 0;

    const loadResults = async () => {
      controller = new AbortController();
      try {
        const data = await hostGameService.getQuestionResults(
          gameId,
          currentQuestion.questionId,
          controller.signal,
        );
        if (!active) {
          return;
        }
        setQuestionResults(data);
        setDataSyncState({ status: 'current', message: null, lastSuccessfulAt: Date.now() });
      } catch (requestError) {
        if (!active || isApiRequestCanceled(requestError)) {
          return;
        }
        failureCount += 1;
        markSyncFailure(
          failureCount,
          'Question results are temporarily unavailable. The last known game state is shown.',
        );
        timeout = setTimeout(() => void loadResults(), getSyncRetryDelay(failureCount));
      }
    };

    void loadResults();
    return () => {
      active = false;
      controller?.abort();
      if (timeout) {
        clearTimeout(timeout);
      }
    };
  }, [
    gameId,
    gameState?.status,
    questionResults,
    currentQuestion,
    markSyncFailure,
    syncRetryTrigger,
  ]);

  useEffect(() => {
    const phase = normalizeGameStatus(gameState?.status ?? 'Lobby');
    if (!gameId || (phase !== 'Leaderboard' && phase !== 'Finished') || leaderboard) {
      return;
    }

    let active = true;
    let timeout: ReturnType<typeof setTimeout> | null = null;
    let controller: AbortController | null = null;
    let failureCount = 0;

    const loadLeaderboard = async () => {
      controller = new AbortController();
      try {
        const data = await hostGameService.getLeaderboard(gameId, controller.signal);
        if (!active) {
          return;
        }
        setLeaderboard(data);
        setDataSyncState({ status: 'current', message: null, lastSuccessfulAt: Date.now() });
      } catch (requestError) {
        if (!active || isApiRequestCanceled(requestError)) {
          return;
        }
        failureCount += 1;
        markSyncFailure(
          failureCount,
          'Leaderboard data is temporarily unavailable. The last known game state is shown.',
        );
        timeout = setTimeout(() => void loadLeaderboard(), getSyncRetryDelay(failureCount));
      }
    };

    void loadLeaderboard();
    return () => {
      active = false;
      controller?.abort();
      if (timeout) {
        clearTimeout(timeout);
      }
    };
  }, [gameId, gameState?.status, leaderboard, markSyncFailure, syncRetryTrigger]);

  const activeParticipants = useMemo(
    () => Array.from(participantsMap.values()).filter((p) => p.isConnected && !p.isRemoved),
    [participantsMap],
  );

  const runAction = useCallback(async (action: () => Promise<unknown>, fallbackMessage: string) => {
    if (actionInFlightRef.current) {
      return undefined;
    }
    actionInFlightRef.current = true;
    setIsActionPending(true);
    setActionError(null);
    try {
      return await action();
    } catch (requestError) {
      setActionError(describeError(requestError, fallbackMessage));
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
      applyQuestionStarted((result as { host: HostQuestionResponse }).host);
    }
  }, [gameId, runAction, applyQuestionStarted]);

  const advanceQuestion = useCallback(async () => {
    if (!gameId) {
      return;
    }
    const result = await runAction(
      () => hostGameService.advance(gameId),
      'Failed to load the next question.',
    );
    if (result && typeof result === 'object' && 'host' in result) {
      applyQuestionStarted((result as { host: HostQuestionResponse }).host);
    }
  }, [gameId, runAction, applyQuestionStarted]);

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
      const removed = await runAction(async () => {
        await hostGameService.removeParticipant(gameId, participantId);
        return true;
      }, 'Failed to remove the participant.');
      if (removed) {
        setRefreshTrigger((value) => value + 1);
      }
    },
    [gameId, runAction],
  );

  const refetch = useCallback(() => {
    setIsLoading(true);
    setRefreshTrigger((value) => value + 1);
  }, []);

  const retrySync = useCallback(() => {
    if (hubConnectionStatus !== 'connected') {
      retryHub();
      return;
    }
    setSyncRetryTrigger((value) => value + 1);
    setRefreshTrigger((value) => value + 1);
  }, [hubConnectionStatus, retryHub]);

  const dismissActionError = useCallback(() => setActionError(null), []);

  return {
    gameState,
    participants: activeParticipants,
    participantCount,
    currentQuestion,
    questionResults,
    leaderboard,
    isLoading,
    error,
    actionError,
    isActionPending,
    hubConnectionStatus,
    dataSyncState,
    retryHub,
    retrySync,
    refetch,
    dismissActionError,
    startGame,
    advanceQuestion,
    endQuestion,
    showLeaderboard,
    endGame,
    removeParticipant,
    liveAnnouncement,
  };
}

export default useHostGame;
