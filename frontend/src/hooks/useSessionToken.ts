import { useCallback, useState } from 'react';

export interface PlayerSession {
  sessionToken: string;
  participantId: string;
  nickname: string;
  gameId: string;
}

const storageKey = (gameId: string): string => `kahoot_player_session_${gameId}`;

function isValidSession(value: unknown): value is PlayerSession {
  if (!value || typeof value !== 'object') {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.sessionToken === 'string' &&
    candidate.sessionToken.length > 0 &&
    typeof candidate.participantId === 'string' &&
    typeof candidate.nickname === 'string' &&
    typeof candidate.gameId === 'string'
  );
}

export function getSession(gameId: string): PlayerSession | null {
  try {
    const raw = sessionStorage.getItem(storageKey(gameId));
    if (!raw) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    return isValidSession(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

export function saveSession(gameId: string, data: PlayerSession): boolean {
  try {
    sessionStorage.setItem(storageKey(gameId), JSON.stringify(data));
    return true;
  } catch {
    return false;
  }
}

export function clearSession(gameId: string): boolean {
  try {
    sessionStorage.removeItem(storageKey(gameId));
    return true;
  } catch {
    return false;
  }
}

export function useSessionToken(gameId: string | undefined) {
  const [trackedGameId, setTrackedGameId] = useState(gameId);
  const [session, setSession] = useState<PlayerSession | null>(() =>
    gameId ? getSession(gameId) : null,
  );

  if (gameId !== trackedGameId) {
    setTrackedGameId(gameId);
    setSession(gameId ? getSession(gameId) : null);
  }

  const clear = useCallback(() => {
    if (gameId) {
      clearSession(gameId);
    }
    setSession(null);
  }, [gameId]);

  return { session, clear };
}

export default useSessionToken;
