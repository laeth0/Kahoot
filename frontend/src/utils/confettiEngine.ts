export type ConfettiVariant = 'leaderboard' | 'final-results';

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  color: string;
  shape: 'rect' | 'circle' | 'star';
  rotation: number;
  rotationSpeed: number;
  wobble: number;
  wobbleSpeed: number;
  opacity: number;
  decay: number;
}

const CELEBRATION_COLORS = [
  '#F59E0B',
  '#0284C7',
  '#10B981',
  '#F43F5E',
  '#8B5CF6',
  '#FBBF24',
  '#38BDF8',
  '#EC4899',
  '#14B8A6',
];

function drawStar(
  ctx: CanvasRenderingContext2D,
  cx: number,
  cy: number,
  spikes: number,
  outerRadius: number,
  innerRadius: number,
): void {
  let rot = (Math.PI / 2) * 3;
  let x: number;
  let y: number;
  const step = Math.PI / spikes;

  ctx.beginPath();
  ctx.moveTo(cx, cy - outerRadius);
  for (let i = 0; i < spikes; i++) {
    x = cx + Math.cos(rot) * outerRadius;
    y = cy + Math.sin(rot) * outerRadius;
    ctx.lineTo(x, y);
    rot += step;

    x = cx + Math.cos(rot) * innerRadius;
    y = cy + Math.sin(rot) * innerRadius;
    ctx.lineTo(x, y);
    rot += step;
  }
  ctx.lineTo(cx, cy - outerRadius);
  ctx.closePath();
  ctx.fill();
}

export class ConfettiEngine {
  private canvas: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D | null;
  private particles: Particle[] = [];
  private animationId: number | null = null;
  private timeouts: number[] = [];
  private isRunning = false;

  constructor(canvas: HTMLCanvasElement) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
  }

  public resize(width: number, height: number): void {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.canvas.width = width * dpr;
    this.canvas.height = height * dpr;
    this.canvas.style.width = `${width}px`;
    this.canvas.style.height = `${height}px`;
    if (this.ctx) {
      this.ctx.setTransform(1, 0, 0, 1, 0, 0);
      this.ctx.scale(dpr, dpr);
    }
  }

  public start(variant: ConfettiVariant = 'leaderboard'): void {
    if (
      typeof window !== 'undefined' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches
    ) {
      return;
    }

    this.stop();
    this.isRunning = true;

    const width = this.canvas.clientWidth || window.innerWidth;
    const height = this.canvas.clientHeight || window.innerHeight;

    if (variant === 'final-results') {
      this.launchCannons(width, height, 110);
      this.launchBurst(width * 0.5, height * 0.45, 90);

      this.timeouts.push(
        window.setTimeout(() => {
          if (!this.isRunning) return;
          this.launchCannons(width, height, 90);
        }, 800),
      );

      this.timeouts.push(
        window.setTimeout(() => {
          if (!this.isRunning) return;
          this.launchBurst(width * 0.3, height * 0.4, 60);
          this.launchBurst(width * 0.7, height * 0.4, 60);
        }, 1600),
      );

      this.timeouts.push(
        window.setTimeout(() => {
          if (!this.isRunning) return;
          this.launchCannons(width, height, 80);
        }, 2500),
      );
    } else {
      this.launchCannons(width, height, 90);
      this.timeouts.push(
        window.setTimeout(() => {
          if (!this.isRunning) return;
          this.launchBurst(width * 0.5, height * 0.4, 60);
        }, 400),
      );
    }

    this.animate();
  }

  public stop(): void {
    this.isRunning = false;
    if (this.animationId !== null) {
      cancelAnimationFrame(this.animationId);
      this.animationId = null;
    }
    this.timeouts.forEach((id) => clearTimeout(id));
    this.timeouts = [];
    this.particles = [];
    if (this.ctx) {
      this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    }
  }

  private launchCannons(width: number, height: number, countPerCannon: number): void {
    const leftOriginX = width * 0.08;
    const leftOriginY = height * 0.95;
    for (let i = 0; i < countPerCannon; i++) {
      const angle = -Math.PI * (0.2 + Math.random() * 0.28);
      const speed = 15 + Math.random() * 16;
      this.particles.push(this.createParticle(leftOriginX, leftOriginY, angle, speed));
    }

    const rightOriginX = width * 0.92;
    const rightOriginY = height * 0.95;
    for (let i = 0; i < countPerCannon; i++) {
      const angle = -Math.PI * (0.52 + Math.random() * 0.28);
      const speed = 15 + Math.random() * 16;
      this.particles.push(this.createParticle(rightOriginX, rightOriginY, angle, speed));
    }
  }

  private launchBurst(x: number, y: number, count: number): void {
    for (let i = 0; i < count; i++) {
      const angle = Math.random() * Math.PI * 2;
      const speed = 4 + Math.random() * 14;
      this.particles.push(this.createParticle(x, y, angle, speed));
    }
  }

  private createParticle(x: number, y: number, angle: number, speed: number): Particle {
    const shapes: ('rect' | 'circle' | 'star')[] = ['rect', 'rect', 'circle', 'star'];
    const shape = shapes[Math.floor(Math.random() * shapes.length)];
    const color = CELEBRATION_COLORS[Math.floor(Math.random() * CELEBRATION_COLORS.length)];

    return {
      x,
      y,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed,
      size: 7 + Math.random() * 9,
      color,
      shape,
      rotation: Math.random() * Math.PI * 2,
      rotationSpeed: (Math.random() - 0.5) * 0.2,
      wobble: Math.random() * Math.PI * 2,
      wobbleSpeed: 0.08 + Math.random() * 0.1,
      opacity: 1,
      decay: 0.0035 + Math.random() * 0.004,
    };
  }

  private animate = (): void => {
    if (!this.isRunning || !this.ctx) {
      return;
    }

    const width = this.canvas.clientWidth || window.innerWidth;
    const height = this.canvas.clientHeight || window.innerHeight;
    this.ctx.clearRect(0, 0, width, height);

    const alive: Particle[] = [];

    for (let i = 0; i < this.particles.length; i++) {
      const p = this.particles[i];
      p.x += p.vx;
      p.y += p.vy;
      p.vx *= 0.985;
      p.vy = p.vy * 0.985 + 0.32;
      p.rotation += p.rotationSpeed;
      p.wobble += p.wobbleSpeed;
      p.opacity -= p.decay;

      if (p.opacity > 0 && p.y < height + 40 && p.x > -40 && p.x < width + 40) {
        alive.push(p);

        this.ctx.save();
        this.ctx.translate(p.x, p.y);
        this.ctx.rotate(p.rotation);
        this.ctx.scale(Math.cos(p.wobble), 1);
        this.ctx.globalAlpha = Math.max(0, p.opacity);
        this.ctx.fillStyle = p.color;

        if (p.shape === 'rect') {
          this.ctx.fillRect(-p.size / 2, -p.size / 4, p.size, p.size / 2);
        } else if (p.shape === 'circle') {
          this.ctx.beginPath();
          this.ctx.arc(0, 0, p.size / 2.5, 0, Math.PI * 2);
          this.ctx.fill();
        } else {
          drawStar(this.ctx, 0, 0, 5, p.size * 0.6, p.size * 0.25);
        }

        this.ctx.restore();
      }
    }

    this.particles = alive;

    if (this.particles.length > 0 || this.timeouts.length > 0) {
      this.animationId = requestAnimationFrame(this.animate);
    } else {
      this.isRunning = false;
      this.animationId = null;
    }
  };
}
