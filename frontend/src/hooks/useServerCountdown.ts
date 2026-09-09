import { useCallback, useEffect, useRef, useState } from 'react';

export interface CountdownState {
  secondsLeft: number;
  fraction: number;
  expired: boolean;
}

export interface ServerCountdownOptions {
  startedAt?: string | null;
  totalSeconds?: number;
  paused?: boolean;
}

interface CountdownAnchor {
  skewMs: number;
  totalMs: number;
}

const EXPIRED: CountdownState = { secondsLeft: 0, fraction: 0, expired: true };

function parseMs(value: string | null | undefined): number | null {
  if (!value) {
    return null;
  }
  const ms = Date.parse(value);
  return Number.isNaN(ms) ? null : ms;
}

export function useServerCountdown(
  endsAt: string | null | undefined,
  { startedAt, totalSeconds, paused = false }: ServerCountdownOptions = {},
): CountdownState {
  const endsAtMs = parseMs(endsAt);
  const startedAtMs = parseMs(startedAt);

  const anchorRef = useRef<CountdownAnchor | null>(null);
  const lastRef = useRef<CountdownState>(EXPIRED);
  const [state, setState] = useState<CountdownState>(EXPIRED);

  const readNow = useCallback((): CountdownState => {
    const anchor = anchorRef.current;
    if (endsAtMs === null || anchor === null) {
      return EXPIRED;
    }
    const remainingMs = Math.max(0, endsAtMs - (Date.now() - anchor.skewMs));
    return {
      secondsLeft: Math.ceil(remainingMs / 1000),
      fraction: Math.min(1, Math.max(0, remainingMs / anchor.totalMs)),
      expired: remainingMs <= 0,
    };
  }, [endsAtMs]);

  useEffect(() => {
    if (endsAtMs === null) {
      anchorRef.current = null;
      return;
    }
    const receivedAtMs = Date.now();
    const serverStartMs =
      startedAtMs ?? (totalSeconds ? endsAtMs - totalSeconds * 1000 : receivedAtMs);
    anchorRef.current = {
      skewMs: receivedAtMs - serverStartMs,
      totalMs: Math.max(1, endsAtMs - serverStartMs),
    };
  }, [endsAtMs, startedAtMs, totalSeconds]);

  useEffect(() => {
    if (paused || endsAtMs === null) {
      return;
    }

    let frame = 0;

    const emit = () => {
      const next = readNow();
      const prev = lastRef.current;
      if (
        prev.secondsLeft !== next.secondsLeft ||
        prev.expired !== next.expired ||
        Math.abs(prev.fraction - next.fraction) > 0.01
      ) {
        lastRef.current = next;
        setState(next);
      }
      frame = requestAnimationFrame(emit);
    };

    const handleVisibility = () => {
      if (!document.hidden) {
        lastRef.current = readNow();
        setState(lastRef.current);
      }
    };

    frame = requestAnimationFrame(emit);
    document.addEventListener('visibilitychange', handleVisibility);

    return () => {
      cancelAnimationFrame(frame);
      document.removeEventListener('visibilitychange', handleVisibility);
    };
  }, [readNow, paused, endsAtMs]);

  return state;
}

export default useServerCountdown;
