import { useCallback, useEffect, useRef, useState } from 'react';

import { getFriendlyErrorMessage } from '../constants/errorCodes.ts';
import { type NormalizedGameStatus, normalizeGameStatus } from '../constants/gameStatus.ts';
import type {
  GameDataSyncState,
  LeaderboardResponse,
  ParticipantPresenceResponse,
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
const MAX_SYNC_RETRY_DELAY_MS = 15000;
const STALE_FAILURE_THRESHOLD = 2;

export type AnswerState =
  'idle' | 'submitting' | 'accepted' | 'alreadyAnswered' | 'tooLate' | 'rejected' | 'slowDown';

export interface PlayerState {
  nickname: string;
  status: NormalizedGameStatus;
  participantId: string;
  totalScore: number;
  rank: number | null;
  participantCount: number;
  presenceVersion: number;
  currentQuestion: PlayerQuestionResponse | null;
  alreadyAnswered: boolean;
  lastResults: QuestionResultsResponse | null;
  leaderboard: LeaderboardResponse | null;
}

interface LiveRefs {
  participantId: string | null;
  totalScore: number;
  rank: number | null;
  presenceVersion: number;
}

function getSyncRetryDelay(failureCount: number): number {
  return Math.min(2000 * 2 ** Math.max(0, failureCount - 1), MAX_SYNC_RETRY_DELAY_MS);
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
  const [selectedChoiceIds, setSelectedChoiceIds] = useState<string[]>([]);
  const [scoreBeforeQuestion, setScoreBeforeQuestion] = useState(0);
  const [rankDelta, setRankDelta] = useState<number | null>(null);
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

  const liveRef = useRef<LiveRefs>({
    participantId: null,
    totalScore: 0,
    rank: null,
    presenceVersion: 0,
  });
  const rankBeforeQuestionRef = useRef<number | null>(null);
  const submitInFlightRef = useRef(false);
  const prevHubStatusRef = useRef(hubStatus);

  useEffect(() => {
    if (prevHubStatusRef.current === 'connected' && hubStatus === 'reconnecting') {
      setLiveAnnouncement({
        message: 'Connection interrupted. Reconnecting to live game...',
        politeness: 'polite',
      });
    } else if (prevHubStatusRef.current === 'reconnecting' && hubStatus === 'connected') {
      setLiveAnnouncement({
        message: 'Reconnected to live game.',
        politeness: 'polite',
      });
    }
    prevHubStatusRef.current = hubStatus;
  }, [hubStatus]);

  useEffect(() => {
    liveRef.current = {
      participantId: playerState?.participantId ?? null,
      totalScore: playerState?.totalScore ?? 0,
      rank: playerState?.rank ?? null,
      presenceVersion: playerState?.presenceVersion ?? 0,
    };
  }, [
    playerState?.participantId,
    playerState?.totalScore,
    playerState?.rank,
    playerState?.presenceVersion,
  ]);

  const applyState = useCallback((data: PlayerGameStateResponse) => {
    if (data.presenceVersion < liveRef.current.presenceVersion) {
      return false;
    }
    const status = normalizeGameStatus(data.status);
    liveRef.current = {
      participantId: data.participantId,
      totalScore: data.totalScore,
      rank: data.rank ?? null,
      presenceVersion: data.presenceVersion,
    };
    setScoreBeforeQuestion(data.totalScore);
    setRankDelta(null);
    rankBeforeQuestionRef.current = data.rank ?? null;
    setPlayerState({
      nickname: data.nickname,
      status,
      participantId: data.participantId,
      totalScore: data.totalScore,
      rank: data.rank ?? null,
      participantCount: data.participantCount,
      presenceVersion: data.presenceVersion,
      currentQuestion: data.currentQuestion ?? null,
      alreadyAnswered: data.alreadyAnsweredCurrentQuestion,
      lastResults: data.lastQuestionResults ?? null,
      leaderboard: data.leaderboard ?? null,
    });
    setHydrateError(null);
    setDataSyncState({ status: 'current', message: null, lastSuccessfulAt: Date.now() });
    setSelectedChoiceIds([]);
    setAnswerState(
      status === 'QuestionActive' && data.alreadyAnsweredCurrentQuestion
        ? 'alreadyAnswered'
        : 'idle',
    );
    return true;
  }, []);

  useEffect(() => {
    if (!connection || hubStatus !== 'connected' || !sessionToken || isKicked) {
      return;
    }

    let active = true;
    let retryTimeout: ReturnType<typeof setTimeout> | null = null;
    let failureCount = 0;
    let hydrateInFlight = false;

    const handleKicked = () => {
      clear();
      setIsKicked(true);
      setLiveAnnouncement({ message: 'You were removed from the game.', politeness: 'assertive' });
      connection.stop().catch(() => {});
    };

    const scheduleHydrateRetry = (message: string) => {
      failureCount += 1;
      if (liveRef.current.participantId) {
        if (failureCount >= STALE_FAILURE_THRESHOLD) {
          setDataSyncState((previous) => ({
            ...previous,
            status: 'stale',
            message,
          }));
        }
      } else {
        setHydrateError('Lost connection while joining the game lobby. Retrying automatically.');
      }
      retryTimeout = setTimeout(() => {
        retryTimeout = null;
        void hydrate();
      }, getSyncRetryDelay(failureCount));
    };

    const hydrate = async () => {
      if (hydrateInFlight) {
        return;
      }
      hydrateInFlight = true;
      try {
        const jitter = Math.floor(Math.random() * 300);
        if (jitter > 0) {
          await new Promise((resolve) => setTimeout(resolve, jitter));
        }
        if (!active) {
          return;
        }
        const response = await invokeReconnect(connection, sessionToken);
        if (!active) {
          return;
        }
        if (response.success && response.data) {
          if (!applyState(response.data)) {
            scheduleHydrateRetry(
              'Live state has not caught up yet. Your last known game view is still shown.',
            );
            return;
          }
          failureCount = 0;
          if (retryTimeout) {
            clearTimeout(retryTimeout);
            retryTimeout = null;
          }
          setLiveAnnouncement({ message: 'Connected to game session.', politeness: 'polite' });
          return;
        }
        const code = response.error?.code;
        if (code === REMOVED_CODE) {
          handleKicked();
          return;
        }
        if (
          code === INVALID_SESSION_CODE ||
          code === 'Game.NotFound' ||
          code === 'Game.InvalidPin'
        ) {
          clear();
          setPlayerState((previous) => (previous ? { ...previous, status: 'Finished' } : null));
          setHydrateError('This game session has ended or is no longer available.');
          return;
        }
        scheduleHydrateRetry(
          getFriendlyErrorMessage(
            code,
            'Live state may be out of date. Your last known game view is still shown.',
          ),
        );
      } catch {
        if (active) {
          scheduleHydrateRetry(
            'Live state may be out of date. Your last known game view is still shown.',
          );
        }
      } finally {
        hydrateInFlight = false;
      }
    };

    const handleParticipantPresenceChanged = (payload: ParticipantPresenceResponse) => {
      const currentVersion = liveRef.current.presenceVersion;
      if (payload.presenceVersion <= currentVersion) {
        return;
      }
      if (payload.presenceVersion !== currentVersion + 1) {
        setDataSyncState((previous) => ({
          ...previous,
          status: 'stale',
          message: 'A live update was missed. Synchronizing authoritative game state.',
        }));
        void hydrate();
        return;
      }
      liveRef.current.presenceVersion = payload.presenceVersion;
      setPlayerState((previous) =>
        previous
          ? {
              ...previous,
              participantCount: payload.participantCount,
              presenceVersion: payload.presenceVersion,
            }
          : previous,
      );
    };

    const handleParticipantRemoved = (participantId: string) => {
      if (participantId === liveRef.current.participantId) {
        handleKicked();
        return;
      }
      setLiveAnnouncement({ message: 'A player was removed.', politeness: 'polite' });
    };

    const handleQuestionStarted = (question: PlayerQuestionResponse) => {
      submitInFlightRef.current = false;
      setScoreBeforeQuestion(liveRef.current.totalScore);
      rankBeforeQuestionRef.current = liveRef.current.rank;
      setRankDelta(null);
      setSelectedChoiceIds([]);
      setAnswerState('idle');
      setLiveAnnouncement({ message: 'Question started.', politeness: 'polite' });
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
      setLiveAnnouncement({ message: 'Results are in.', politeness: 'polite' });
      setPlayerState((previous) =>
        previous ? { ...previous, status: 'QuestionResults', lastResults: results } : previous,
      );
    };

    const foldLeaderboard = (board: LeaderboardResponse, status: 'Leaderboard' | 'Finished') => {
      const pid = liveRef.current.participantId;
      const mine = pid ? myLeaderboardEntry(board, pid) : null;
      const newRank = mine?.rank ?? null;
      const before = rankBeforeQuestionRef.current;
      setRankDelta(newRank !== null && before !== null ? before - newRank : null);
      setPlayerState((previous) =>
        previous
          ? {
              ...previous,
              status,
              leaderboard: board,
              totalScore: mine?.totalScore ?? previous.totalScore,
              rank: mine?.rank ?? previous.rank,
            }
          : previous,
      );
    };

    const handleLeaderboardUpdated = (board: LeaderboardResponse) => {
      setLiveAnnouncement({ message: 'Leaderboard updated.', politeness: 'polite' });
      foldLeaderboard(board, 'Leaderboard');
    };

    const handleGameEnded = (board: LeaderboardResponse) => {
      setLiveAnnouncement({ message: 'Game finished. Final results ready.', politeness: 'polite' });
      foldLeaderboard(board, 'Finished');
    };

    const handleBeforeUnload = () => {
      connection.stop().catch(() => {});
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    connection.on('ParticipantPresenceChanged', handleParticipantPresenceChanged);
    connection.on('ParticipantRemoved', handleParticipantRemoved);
    connection.on('QuestionStarted', handleQuestionStarted);
    connection.on('QuestionEnded', handleQuestionEnded);
    connection.on('LeaderboardUpdated', handleLeaderboardUpdated);
    connection.on('GameEnded', handleGameEnded);

    void hydrate();

    return () => {
      active = false;
      if (retryTimeout) {
        clearTimeout(retryTimeout);
      }
      window.removeEventListener('beforeunload', handleBeforeUnload);
      connection.off('ParticipantPresenceChanged', handleParticipantPresenceChanged);
      connection.off('ParticipantRemoved', handleParticipantRemoved);
      connection.off('QuestionStarted', handleQuestionStarted);
      connection.off('QuestionEnded', handleQuestionEnded);
      connection.off('LeaderboardUpdated', handleLeaderboardUpdated);
      connection.off('GameEnded', handleGameEnded);
    };
  }, [connection, hubStatus, sessionToken, isKicked, applyState, clear, syncRetryTrigger]);

  const activeQuestionId = playerState?.currentQuestion?.questionId ?? null;

  const submitAnswer = useCallback(
    async (choiceIds: string | string[]) => {
      if (!connection || !activeQuestionId || submitInFlightRef.current) {
        return;
      }
      const ids = Array.isArray(choiceIds) ? choiceIds : [choiceIds];
      if (ids.length === 0) {
        return;
      }
      submitInFlightRef.current = true;
      setSelectedChoiceIds(ids);
      setAnswerState('submitting');
      try {
        const response = await invokeSubmitAnswer(connection, activeQuestionId, ids);
        if (response.success && response.data?.accepted) {
          setLiveAnnouncement({ message: 'Answer accepted.', politeness: 'polite' });
          setAnswerState(response.data.alreadyAnswered ? 'alreadyAnswered' : 'accepted');
          setPlayerState((previous) =>
            previous ? { ...previous, alreadyAnswered: true } : previous,
          );
          return;
        }
        const code = response.error?.code;
        if (code === REMOVED_CODE) {
          clear();
          setIsKicked(true);
          setLiveAnnouncement({
            message: 'You were removed from the game.',
            politeness: 'assertive',
          });
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
    [connection, activeQuestionId, clear],
  );

  const leaveGame = useCallback(async () => {
    clear();
    if (connection) {
      await connection.stop().catch(() => {});
    }
  }, [clear, connection]);

  const retrySync = useCallback(() => {
    if (hubStatus !== 'connected') {
      retryHub();
      return;
    }
    setSyncRetryTrigger((value) => value + 1);
  }, [hubStatus, retryHub]);

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
    dataSyncState,
    retryHub,
    retrySync,
    leaveGame,
    submitAnswer,
    answerState,
    selectedChoiceId: selectedChoiceIds[0] ?? null,
    selectedChoiceIds,
    pointsThisQuestion,
    rankDelta,
    liveAnnouncement,
  };
}

export default usePlayerGame;
