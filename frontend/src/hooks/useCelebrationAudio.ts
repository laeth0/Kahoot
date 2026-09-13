import { useCallback, useEffect, useState } from 'react';

import { celebrationAudioService } from '../services/celebrationAudioService.ts';

export interface UseCelebrationAudioResult {
  isMuted: boolean;
  toggleMute: () => boolean;
  playApplause: () => void;
  stopAudio: () => void;
}

export function useCelebrationAudio(): UseCelebrationAudioResult {
  const [muted, setMutedState] = useState<boolean>(() => celebrationAudioService.isMuted());

  useEffect(() => {
    return celebrationAudioService.subscribe((next) => {
      setMutedState(next);
    });
  }, []);

  const toggleMute = useCallback(() => {
    return celebrationAudioService.toggleMuted();
  }, []);

  const playApplause = useCallback(() => {
    celebrationAudioService.playApplause();
  }, []);

  const stopAudio = useCallback(() => {
    celebrationAudioService.stop();
  }, []);

  return {
    isMuted: muted,
    toggleMute,
    playApplause,
    stopAudio,
  };
}
