import { useCallback, useState } from 'react';

import type { HostQuestionResponse } from '../realtime/events.ts';

export interface PlayerSession {
  sessionToken: string;
  participantId: string;
  nickname: string;
  gameId: string;
}

const storageKey = (gameId: string): string => `kahoot_player_session_${gameId}`;
const hostQuestionKey = (gameId: string): string => `kahoot_host_question_${gameId}`;

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

export function getHostQuestion(gameId: string): HostQuestionResponse | null {
  try {
    const raw = sessionStorage.getItem(hostQuestionKey(gameId));
    if (!raw) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    if (
      parsed &&
      typeof parsed === 'object' &&
      typeof (parsed as { questionId?: unknown }).questionId === 'string'
    ) {
      return parsed as HostQuestionResponse;
    }
    return null;
  } catch {
    return null;
  }
}

export function saveHostQuestion(gameId: string, question: HostQuestionResponse): boolean {
  try {
    sessionStorage.setItem(hostQuestionKey(gameId), JSON.stringify(question));
    return true;
  } catch {
    return false;
  }
}

export function clearHostQuestion(gameId: string): boolean {
  try {
    sessionStorage.removeItem(hostQuestionKey(gameId));
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
