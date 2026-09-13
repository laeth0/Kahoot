import React, { useEffect, useRef } from 'react';

import { ConfettiEngine, type ConfettiVariant } from '../../utils/confettiEngine';

export interface CelebrationOverlayProps {
  active?: boolean;
  variant?: ConfettiVariant;
  triggerKey?: string | number;
}

export const CelebrationOverlay: React.FC<CelebrationOverlayProps> = ({
  active = true,
  variant = 'leaderboard',
  triggerKey,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const engineRef = useRef<ConfettiEngine | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const engine = new ConfettiEngine(canvas);
    engineRef.current = engine;

    const handleResize = () => {
      engine.resize(window.innerWidth, window.innerHeight);
    };

    handleResize();
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      engine.stop();
      engineRef.current = null;
    };
  }, []);

  useEffect(() => {
    if (!active || !engineRef.current) return;
    engineRef.current.start(variant);

    return () => {
      engineRef.current?.stop();
    };
  }, [active, variant, triggerKey]);

  return (
    <canvas
      ref={canvasRef}
      aria-hidden="true"
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        width: '100vw',
        height: '100vh',
        pointerEvents: 'none',
        zIndex: 1400,
      }}
    />
  );
};
