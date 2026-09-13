import { Howl, Howler } from 'howler';

const MUTE_STORAGE_KEY = 'kahoot_sound_muted';

function getStoredMute(): boolean {
  try {
    return localStorage.getItem(MUTE_STORAGE_KEY) === 'true';
  } catch {
    return false;
  }
}

class CelebrationAudioService {
  private muted = getStoredMute();
  private howl: Howl | null = null;
  private muteListeners = new Set<(muted: boolean) => void>();
  private audioCtx: AudioContext | null = null;

  constructor() {
    this.initUnlockListener();
  }

  private initUnlockListener(): void {
    if (typeof window === 'undefined') return;
    const unlock = () => {
      if (this.howl && (Howler as unknown as { ctx?: AudioContext }).ctx?.state === 'suspended') {
        (Howler as unknown as { ctx?: AudioContext }).ctx?.resume().catch(() => {});
      }
      if (this.audioCtx && this.audioCtx.state === 'suspended') {
        this.audioCtx.resume().catch(() => {});
      }
    };
    window.addEventListener('pointerdown', unlock, { passive: true, once: true });
    window.addEventListener('keydown', unlock, { passive: true, once: true });
  }

  private getHowl(): Howl {
    if (!this.howl) {
      this.howl = new Howl({
        src: ['/sounds/applause.wav'],
        format: ['wav'],
        volume: 0.75,
        html5: false,
        preload: true,
        onloaderror: () => {
          this.howl = null;
        },
      });
    }
    return this.howl;
  }

  public isMuted(): boolean {
    return this.muted;
  }

  public setMuted(muted: boolean): void {
    this.muted = muted;
    try {
      localStorage.setItem(MUTE_STORAGE_KEY, String(muted));
    } catch {
      void 0;
    }
    if (muted) {
      this.stop();
    }
    this.muteListeners.forEach((listener) => listener(muted));
  }

  public toggleMuted(): boolean {
    const next = !this.muted;
    this.setMuted(next);
    return next;
  }

  public subscribe(listener: (muted: boolean) => void): () => void {
    this.muteListeners.add(listener);
    return () => {
      this.muteListeners.delete(listener);
    };
  }

  public playApplause(): void {
    if (this.muted) {
      return;
    }

    this.stop();

    try {
      const sound = this.getHowl();
      sound.play();
    } catch {
      this.playFallbackApplause();
    }
  }

  public stop(): void {
    if (this.howl) {
      try {
        this.howl.stop();
      } catch {
        void 0;
      }
    }
    this.stopFallback();
  }

  private stopFallback(): void {
    if (this.audioCtx) {
      try {
        this.audioCtx.close().catch(() => {});
      } catch {
        void 0;
      }
      this.audioCtx = null;
    }
  }

  private playFallbackApplause(): void {
    if (typeof window === 'undefined' || this.muted) return;
    try {
      const AudioContextClass =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (!AudioContextClass) return;

      this.audioCtx = new AudioContextClass();
      const ctx = this.audioCtx;
      if (ctx.state === 'suspended') {
        ctx.resume().catch(() => {});
      }

      const duration = 3.0;
      const bufferSize = ctx.sampleRate * duration;
      const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
      const data = buffer.getChannelData(0);
      for (let i = 0; i < bufferSize; i++) {
        data[i] = Math.random() * 2 - 1;
      }

      const noiseSource = ctx.createBufferSource();
      noiseSource.buffer = buffer;

      const bandpass = ctx.createBiquadFilter();
      bandpass.type = 'bandpass';
      bandpass.frequency.setValueAtTime(1200, ctx.currentTime);
      bandpass.Q.setValueAtTime(1.0, ctx.currentTime);

      const gain = ctx.createGain();
      gain.gain.setValueAtTime(0.001, ctx.currentTime);
      gain.gain.linearRampToValueAtTime(0.35, ctx.currentTime + 0.25);
      gain.gain.setValueAtTime(0.35, ctx.currentTime + duration * 0.7);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duration);

      noiseSource.connect(bandpass);
      bandpass.connect(gain);
      gain.connect(ctx.destination);

      noiseSource.start();
      noiseSource.stop(ctx.currentTime + duration);
    } catch {
      void 0;
    }
  }
}

export const celebrationAudioService = new CelebrationAudioService();
