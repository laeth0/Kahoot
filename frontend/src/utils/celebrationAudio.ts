export type CelebrationVariant = 'leaderboard' | 'final-results';

const MUTE_STORAGE_KEY = 'kahoot_sound_muted';

let audioCtx: AudioContext | null = null;
let activeGainNodes: GainNode[] = [];
const muteListeners = new Set<(muted: boolean) => void>();

function getStoredMute(): boolean {
  try {
    return localStorage.getItem(MUTE_STORAGE_KEY) === 'true';
  } catch {
    return false;
  }
}

let mutedState = getStoredMute();

export function isMuted(): boolean {
  return mutedState;
}

export function setMuted(muted: boolean): void {
  mutedState = muted;
  try {
    localStorage.setItem(MUTE_STORAGE_KEY, String(muted));
  } catch {
    void 0;
  }
  if (muted) {
    stopCelebrationSound();
  }
  muteListeners.forEach((listener) => listener(muted));
}

export function toggleMuted(): boolean {
  const next = !mutedState;
  setMuted(next);
  return next;
}

export function subscribeMuteChange(listener: (muted: boolean) => void): () => void {
  muteListeners.add(listener);
  return () => {
    muteListeners.delete(listener);
  };
}

function getAudioContext(): AudioContext | null {
  if (typeof window === 'undefined') {
    return null;
  }
  const AudioCtxClass =
    window.AudioContext ||
    (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
  if (!AudioCtxClass) {
    return null;
  }
  if (!audioCtx) {
    try {
      audioCtx = new AudioCtxClass();
    } catch {
      return null;
    }
  }
  if (audioCtx.state === 'suspended') {
    audioCtx.resume().catch(() => {});
  }
  return audioCtx;
}

if (typeof window !== 'undefined') {
  const unlock = () => {
    if (audioCtx && audioCtx.state === 'suspended') {
      audioCtx.resume().catch(() => {});
    }
  };
  window.addEventListener('pointerdown', unlock, { passive: true, once: true });
  window.addEventListener('keydown', unlock, { passive: true, once: true });
}

function createNoiseBuffer(ctx: AudioContext, duration: number): AudioBuffer {
  const sampleRate = ctx.sampleRate;
  const bufferSize = sampleRate * duration;
  const buffer = ctx.createBuffer(1, bufferSize, sampleRate);
  const data = buffer.getChannelData(0);
  for (let i = 0; i < bufferSize; i++) {
    data[i] = Math.random() * 2 - 1;
  }
  return buffer;
}

function playApplause(
  ctx: AudioContext,
  startTime: number,
  duration: number,
  masterGain: GainNode,
): void {
  const noise = createNoiseBuffer(ctx, duration);
  const noiseSource = ctx.createBufferSource();
  noiseSource.buffer = noise;

  const bandpass = ctx.createBiquadFilter();
  bandpass.type = 'bandpass';
  bandpass.frequency.setValueAtTime(1400, startTime);
  bandpass.Q.setValueAtTime(1.2, startTime);

  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0.001, startTime);
  gain.gain.linearRampToValueAtTime(0.35, startTime + 0.3);
  gain.gain.setValueAtTime(0.35, startTime + duration * 0.7);
  gain.gain.exponentialRampToValueAtTime(0.001, startTime + duration);

  const tremolo = ctx.createOscillator();
  tremolo.frequency.setValueAtTime(14, startTime);
  const tremoloGain = ctx.createGain();
  tremoloGain.gain.setValueAtTime(0.12, startTime);
  tremolo.connect(tremoloGain.gain);

  noiseSource.connect(bandpass);
  bandpass.connect(gain);
  gain.connect(masterGain);

  noiseSource.start(startTime);
  noiseSource.stop(startTime + duration);
  tremolo.start(startTime);
  tremolo.stop(startTime + duration);
}

function playChime(
  ctx: AudioContext,
  freq: number,
  startTime: number,
  duration: number,
  volume: number,
  masterGain: GainNode,
): void {
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();

  osc.type = 'triangle';
  osc.frequency.setValueAtTime(freq, startTime);

  gain.gain.setValueAtTime(0.001, startTime);
  gain.gain.linearRampToValueAtTime(volume, startTime + 0.04);
  gain.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);

  osc.connect(gain);
  gain.connect(masterGain);

  osc.start(startTime);
  osc.stop(startTime + duration);
}

export function stopCelebrationSound(): void {
  activeGainNodes.forEach((node) => {
    try {
      node.gain.setValueAtTime(0, audioCtx?.currentTime ?? 0);
      node.disconnect();
    } catch {
      void 0;
    }
  });
  activeGainNodes = [];
}

export function playCelebrationSound(
  variant: 'leaderboard' | 'final-results' = 'leaderboard',
): void {
  if (mutedState) {
    return;
  }
  const ctx = getAudioContext();
  if (!ctx) {
    return;
  }

  stopCelebrationSound();

  const now = ctx.currentTime + 0.05;
  const master = ctx.createGain();
  master.gain.setValueAtTime(0.5, now);
  master.connect(ctx.destination);
  activeGainNodes.push(master);

  if (variant === 'leaderboard') {
    playApplause(ctx, now, 2.6, master);
    playChime(ctx, 523.25, now, 0.45, 0.22, master);
    playChime(ctx, 659.25, now + 0.12, 0.45, 0.25, master);
    playChime(ctx, 783.99, now + 0.24, 0.55, 0.28, master);
    playChime(ctx, 1046.5, now + 0.38, 0.9, 0.32, master);
  } else {
    playApplause(ctx, now, 4.8, master);
    playChime(ctx, 392.0, now, 0.35, 0.2, master);
    playChime(ctx, 523.25, now + 0.12, 0.35, 0.24, master);
    playChime(ctx, 659.25, now + 0.24, 0.4, 0.28, master);
    playChime(ctx, 783.99, now + 0.36, 0.5, 0.32, master);
    playChime(ctx, 1046.5, now + 0.52, 1.4, 0.38, master);
    playChime(ctx, 1318.51, now + 0.7, 1.2, 0.28, master);
  }
}
