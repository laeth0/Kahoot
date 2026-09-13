import { useCallback, useEffect, useRef, useState } from 'react';

import {
  type CelebrationVariant,
  isMuted,
  playCelebrationSound,
  stopCelebrationSound,
  subscribeMuteChange,
  toggleMuted,
} from '../utils/celebrationAudio';

export interface UseCelebrationOptions {
  variant?: CelebrationVariant;
  active?: boolean;
  triggerKey?: string | number;
  autoPlaySound?: boolean;
}

export interface UseCelebrationResult {
  isMuted: boolean;
  toggleMute: () => boolean;
  triggerCelebration: () => void;
}

export function useCelebration({
  variant = 'leaderboard',
  active = true,
  triggerKey,
  autoPlaySound = true,
}: UseCelebrationOptions = {}): UseCelebrationResult {
  const [muted, setMutedState] = useState<boolean>(() => isMuted());
  const lastFiredKeyRef = useRef<string | number | undefined>(undefined);

  useEffect(() => {
    return subscribeMuteChange((next) => {
      setMutedState(next);
    });
  }, []);

  const triggerCelebration = useCallback(() => {
    if (autoPlaySound) {
      playCelebrationSound(variant);
    }
  }, [autoPlaySound, variant]);

  useEffect(() => {
    if (!active) {
      stopCelebrationSound();
      return;
    }

    if (triggerKey !== undefined && lastFiredKeyRef.current === triggerKey) {
      return;
    }

    lastFiredKeyRef.current = triggerKey;
    triggerCelebration();

    return () => {
      stopCelebrationSound();
    };
  }, [active, triggerKey, triggerCelebration]);

  const toggleMute = useCallback(() => {
    return toggleMuted();
  }, []);

  return {
    isMuted: muted,
    toggleMute,
    triggerCelebration,
  };
}
