import { useCallback, useEffect, useRef, useState } from 'react';

import { celebrationAudioService } from '../services/celebrationAudioService.ts';

export type CelebrationVariant = 'leaderboard' | 'final-results';

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
  active = true,
  triggerKey,
  autoPlaySound = true,
}: UseCelebrationOptions = {}): UseCelebrationResult {
  const [muted, setMutedState] = useState<boolean>(() => celebrationAudioService.isMuted());
  const lastFiredKeyRef = useRef<string | number | undefined>(undefined);

  useEffect(() => {
    return celebrationAudioService.subscribe((next) => {
      setMutedState(next);
    });
  }, []);

  const triggerCelebration = useCallback(() => {
    if (autoPlaySound) {
      celebrationAudioService.playApplause();
    }
  }, [autoPlaySound]);

  useEffect(() => {
    if (!active) {
      celebrationAudioService.stop();
      return;
    }

    if (triggerKey !== undefined && lastFiredKeyRef.current === triggerKey) {
      return;
    }

    lastFiredKeyRef.current = triggerKey;
    triggerCelebration();

    return () => {
      celebrationAudioService.stop();
    };
  }, [active, triggerKey, triggerCelebration]);

  const toggleMute = useCallback(() => {
    return celebrationAudioService.toggleMuted();
  }, []);

  return {
    isMuted: muted,
    toggleMute,
    triggerCelebration,
  };
}
