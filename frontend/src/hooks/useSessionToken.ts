import { useCallback, useState } from 'react';

import type { HostQuestionResponse } from '../realtime/events.ts';

export interface PlayerSession {
  sessionToken: string;
  participantId: string;
  nickname: string;
  gameId: string;
  pin?: string;
}

const storageKey = (gameId: string): string => `kahoot_player_session_${gameId}`;
const hostQuestionKey = (gameId: string): string => `kahoot_host_question_${gameId}`;
const pinKey = (pin: string): string => `kahoot_pin_session_${pin.trim()}`;
const latestSessionKey = 'kahoot_latest_session_game_id';

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
    typeof candidate.gameId === 'string' &&
    (candidate.pin === undefined || typeof candidate.pin === 'string')
  );
}

export function getSession(gameId: string): PlayerSession | null {
  try {
    const key = storageKey(gameId);
    let raw = sessionStorage.getItem(key);
    if (!raw) {
      raw = localStorage.getItem(key);
      if (raw) {
        sessionStorage.setItem(key, raw);
        localStorage.removeItem(key);
      }
    }
    if (!raw) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    return isValidSession(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

export function getSessionByPin(pin: string): PlayerSession | null {
  try {
    const normalized = pin.trim();
    if (!normalized) {
      return null;
    }
    const key = pinKey(normalized);
    let gameId = sessionStorage.getItem(key);
    if (!gameId) {
      gameId = localStorage.getItem(key);
      if (gameId) {
        sessionStorage.setItem(key, gameId);
        localStorage.removeItem(key);
      }
    }
    if (!gameId) {
      return null;
    }
    const session = getSession(gameId);
    if (!session) {
      sessionStorage.removeItem(key);
      localStorage.removeItem(key);
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

export function getLatestSession(): PlayerSession | null {
  try {
    let gameId = sessionStorage.getItem(latestSessionKey);
    if (!gameId) {
      gameId = localStorage.getItem(latestSessionKey);
      if (gameId) {
        sessionStorage.setItem(latestSessionKey, gameId);
        localStorage.removeItem(latestSessionKey);
      }
    }
    if (!gameId) {
      return null;
    }
    const session = getSession(gameId);
    if (!session) {
      sessionStorage.removeItem(latestSessionKey);
      localStorage.removeItem(latestSessionKey);
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

export function saveSession(gameId: string, data: PlayerSession): boolean {
  try {
    const serialized = JSON.stringify(data);
    sessionStorage.setItem(storageKey(gameId), serialized);
    sessionStorage.setItem(latestSessionKey, gameId);
    if (data.pin) {
      sessionStorage.setItem(pinKey(data.pin), gameId);
    }
    localStorage.removeItem(storageKey(gameId));
    localStorage.removeItem(latestSessionKey);
    if (data.pin) {
      localStorage.removeItem(pinKey(data.pin));
    }
    return true;
  } catch {
    return false;
  }
}

export function clearSession(gameId: string): boolean {
  try {
    const existing = getSession(gameId);
    sessionStorage.removeItem(storageKey(gameId));
    sessionStorage.removeItem(latestSessionKey);
    localStorage.removeItem(storageKey(gameId));
    localStorage.removeItem(latestSessionKey);
    if (existing?.pin) {
      sessionStorage.removeItem(pinKey(existing.pin));
      localStorage.removeItem(pinKey(existing.pin));
    }
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
