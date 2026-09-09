import { useCallback, useEffect, useRef, useState } from 'react';

import { getFriendlyErrorMessage } from '../constants/errorCodes.ts';
import { type NormalizedGameStatus, normalizeGameStatus } from '../constants/gameStatus.ts';
import type { PlayerGameStateResponse } from '../realtime/events.ts';
import { invokeReconnect } from '../realtime/gameHub.ts';
import { useGameHubConnection } from './useGameHubConnection.ts';
import { useSessionToken } from './useSessionToken.ts';

const REMOVED_CODE = 'Game.ParticipantRemoved';
const INVALID_SESSION_CODE = 'Game.InvalidSessionToken';
const MISSING_GAME_MESSAGE = 'This game link is missing its session id.';
const MISSING_SESSION_MESSAGE = "We couldn't find your player session for this game.";

export interface PlayerState {
  nickname: string;
  status: NormalizedGameStatus;
  participantId: string;
  totalScore: number;
  rank: number | null;
  participantCount: number;
}

export function usePlayerGame(gameId: string | undefined) {
  const { session, clear } = useSessionToken(gameId);
  const sessionToken = session?.sessionToken ?? null;

  const { connection, status: hubStatus, retry: retryHub } = useGameHubConnection(false);

  const [playerState, setPlayerState] = useState<PlayerState | null>(null);
  const [isKicked, setIsKicked] = useState(false);
  const [hydrateError, setHydrateError] = useState<string | null>(null);

  const participantIdRef = useRef<string | null>(null);
  useEffect(() => {
    participantIdRef.current = playerState?.participantId ?? null;
  }, [playerState?.participantId]);

  const applyState = useCallback((data: PlayerGameStateResponse) => {
    setPlayerState((previous) => ({
      nickname: data.nickname,
      status: normalizeGameStatus(data.status),
      participantId: data.participantId,
      totalScore: data.totalScore,
      rank: data.rank ?? null,
      participantCount: previous?.participantCount ?? 1,
    }));
    setHydrateError(null);
  }, []);

  const missingSession = !gameId || !sessionToken;

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
      if (participantId === participantIdRef.current) {
        handleKicked();
        return;
      }
      setPlayerState((previous) =>
        previous
          ? { ...previous, participantCount: Math.max(1, previous.participantCount - 1) }
          : previous,
      );
    };

    const handleQuestionStarted = () => {
      setPlayerState((previous) =>
        previous ? { ...previous, status: 'QuestionActive' } : previous,
      );
    };

    const handleGameEnded = () => {
      setPlayerState((previous) => (previous ? { ...previous, status: 'Finished' } : previous));
    };

    connection.on('ParticipantJoined', handleParticipantJoined);
    connection.on('ParticipantRemoved', handleParticipantRemoved);
    connection.on('QuestionStarted', handleQuestionStarted);
    connection.on('GameEnded', handleGameEnded);

    void hydrate();

    return () => {
      active = false;
      connection.off('ParticipantJoined', handleParticipantJoined);
      connection.off('ParticipantRemoved', handleParticipantRemoved);
      connection.off('QuestionStarted', handleQuestionStarted);
      connection.off('GameEnded', handleGameEnded);
    };
  }, [connection, hubStatus, sessionToken, isKicked, applyState, clear]);

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

  const isLoading = !missingSession && !isKicked && hydrateError === null && playerState === null;

  return {
    playerState,
    isKicked,
    isLoading,
    error,
    hubStatus,
    retryHub,
    leaveGame,
  };
}

export default usePlayerGame;
