import confetti from 'canvas-confetti';
import React, { useEffect, useRef } from 'react';

export type ConfettiVariant = 'leaderboard' | 'final-results';

export interface CelebrationOverlayProps {
  active?: boolean;
  variant?: ConfettiVariant;
  triggerKey?: string | number;
}

const CELEBRATION_COLORS = [
  '#F59E0B',
  '#00629B',
  '#10B981',
  '#F43F5E',
  '#8B5CF6',
  '#FBBF24',
  '#38BDF8',
  '#EC4899',
  '#14B8A6',
];

const GOLD_COLORS = ['#F59E0B', '#FBBF24', '#D97706', '#FEF08A', '#B45309'];

export const CelebrationOverlay: React.FC<CelebrationOverlayProps> = ({
  active = true,
  variant = 'leaderboard',
  triggerKey,
}) => {
  const timeoutsRef = useRef<number[]>([]);
  const lastKeyRef = useRef<string | number | undefined>(undefined);

  const clearAllTimeouts = () => {
    timeoutsRef.current.forEach((id) => window.clearTimeout(id));
    timeoutsRef.current = [];
  };

  useEffect(() => {
    if (!active) {
      clearAllTimeouts();
      confetti.reset();
      return;
    }

    if (triggerKey !== undefined && lastKeyRef.current === triggerKey) {
      return;
    }
    lastKeyRef.current = triggerKey;

    clearAllTimeouts();

    if (
      typeof window !== 'undefined' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches
    ) {
      return;
    }

    if (variant === 'leaderboard') {
      confetti({
        particleCount: 55,
        angle: 60,
        spread: 60,
        origin: { x: 0.08, y: 0.9 },
        colors: CELEBRATION_COLORS,
        disableForReducedMotion: true,
      });

      confetti({
        particleCount: 55,
        angle: 120,
        spread: 60,
        origin: { x: 0.92, y: 0.9 },
        colors: CELEBRATION_COLORS,
        disableForReducedMotion: true,
      });

      const t1 = window.setTimeout(() => {
        confetti({
          particleCount: 40,
          spread: 80,
          origin: { x: 0.5, y: 0.35 },
          colors: CELEBRATION_COLORS,
          disableForReducedMotion: true,
        });
      }, 350);
      timeoutsRef.current.push(t1);
    } else {
      confetti({
        particleCount: 80,
        angle: 60,
        spread: 70,
        origin: { x: 0.05, y: 0.85 },
        colors: CELEBRATION_COLORS,
        disableForReducedMotion: true,
      });

      confetti({
        particleCount: 80,
        angle: 120,
        spread: 70,
        origin: { x: 0.95, y: 0.85 },
        colors: CELEBRATION_COLORS,
        disableForReducedMotion: true,
      });

      const t1 = window.setTimeout(() => {
        confetti({
          particleCount: 60,
          spread: 100,
          origin: { x: 0.5, y: 0.4 },
          colors: CELEBRATION_COLORS,
          disableForReducedMotion: true,
        });
      }, 400);
      timeoutsRef.current.push(t1);

      const t2 = window.setTimeout(() => {
        confetti({
          particleCount: 70,
          angle: 65,
          spread: 75,
          origin: { x: 0.1, y: 0.75 },
          colors: CELEBRATION_COLORS,
          disableForReducedMotion: true,
        });
        confetti({
          particleCount: 70,
          angle: 115,
          spread: 75,
          origin: { x: 0.9, y: 0.75 },
          colors: CELEBRATION_COLORS,
          disableForReducedMotion: true,
        });
      }, 950);
      timeoutsRef.current.push(t2);

      const t3 = window.setTimeout(() => {
        confetti({
          particleCount: 65,
          spread: 90,
          origin: { x: 0.5, y: 0.2 },
          colors: GOLD_COLORS,
          shapes: ['circle'],
          scalar: 1.2,
          disableForReducedMotion: true,
        });
      }, 1600);
      timeoutsRef.current.push(t3);
    }

    return () => {
      clearAllTimeouts();
    };
  }, [active, triggerKey, variant]);

  useEffect(() => {
    return () => {
      clearAllTimeouts();
      confetti.reset();
    };
  }, []);

  return null;
};

export default CelebrationOverlay;
