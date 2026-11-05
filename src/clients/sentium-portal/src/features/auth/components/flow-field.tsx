import { useEffect, useRef } from "react";

interface Particle {
  x: number;
  y: number;
  speed: number;
  depth: number;
  life: number;
  maxLife: number;
  color: string;
  trailX: Float32Array;
  trailY: Float32Array;
  head: number;
  count: number;
}

interface Tracer {
  x: number;
  y: number;
  speed: number;
  life: number;
  maxLife: number;
  color: string;
}

const PARTICLE_COUNT = 700;
const TRAIL_LENGTH = 18;
const RECENT_LENGTH = 6;
const FIELD_SCALE = 0.0016;
const TURN = Math.PI * 4;
const Z_DRIFT = 0.0012;
const EDGE = 24;
const POINTER_RADIUS = 220;
const TAU = Math.PI * 2;

const MESH_TRACERS = 450;
const MESH_PASSES = 320;
const MESH_PASSES_PER_FRAME = 7;
const MESH_ALPHA = 0.04;
const MESH_OPACITY = 0.7;

const PALETTE: ReadonlyArray<readonly [string, number]> = [
  ["148, 163, 184", 0.5],
  ["134, 239, 172", 0.28],
  ["74, 222, 128", 0.22],
];

const pickColor = () => {
  let r = Math.random();
  for (const [color, weight] of PALETTE) {
    r -= weight;
    if (r <= 0) {
      return color;
    }
  }
  return PALETTE[0][0];
};

const buildNoise = () => {
  const p = new Uint8Array(512);
  const perm = Array.from({ length: 256 }, (_, i) => i);
  for (let i = 255; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [perm[i], perm[j]] = [perm[j], perm[i]];
  }
  for (let i = 0; i < 512; i++) {
    p[i] = perm[i & 255];
  }
  const fade = (t: number) => t * t * t * (t * (t * 6 - 15) + 10);
  const lerp = (a: number, b: number, t: number) => a + t * (b - a);
  const grad = (hash: number, x: number, y: number) => {
    switch (hash & 3) {
      case 0:
        return x + y;
      case 1:
        return -x + y;
      case 2:
        return x - y;
      default:
        return -x - y;
    }
  };
  return (x: number, y: number, z: number) => {
    const xi = Math.floor(x) & 255;
    const yi = Math.floor(y) & 255;
    const zi = Math.floor(z) & 255;
    const xf = x - Math.floor(x);
    const yf = y - Math.floor(y);
    const u = fade(xf);
    const v = fade(yf);
    const aa = p[p[xi] + yi] + zi;
    const ab = p[p[xi] + yi + 1] + zi;
    const ba = p[p[xi + 1] + yi] + zi;
    const bb = p[p[xi + 1] + yi + 1] + zi;
    const x1 = lerp(grad(p[aa], xf, yf), grad(p[ba], xf - 1, yf), u);
    const x2 = lerp(grad(p[ab], xf, yf - 1), grad(p[bb], xf - 1, yf - 1), u);
    return lerp(x1, x2, v); // roughly [-1, 1]
  };
};

export const FlowField = ({ className }: { className?: string }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rafRef = useRef<number>(0);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }
    const ctx = canvas.getContext("2d");
    if (!ctx) {
      return;
    }
    const mesh = document.createElement("canvas");
    const meshCtx = mesh.getContext("2d");
    if (!meshCtx) {
      return;
    }

    const noise = buildNoise();
    const dpr = Math.min(window.devicePixelRatio || 1, 2);

    let width = 0;
    let height = 0;
    let meshPass = 0;

    const resize = () => {
      width = canvas.offsetWidth;
      height = canvas.offsetHeight;
      canvas.width = Math.round(width * dpr);
      canvas.height = Math.round(height * dpr);
      mesh.width = canvas.width;
      mesh.height = canvas.height;

      for (const c of [ctx, meshCtx]) {
        c.setTransform(dpr, 0, 0, dpr, 0, 0);
        c.lineWidth = 1;
        c.lineCap = "round";
        c.lineJoin = "round";
      }
      meshPass = 0;
    };
    resize();

    const pointer = { x: 0, y: 0, active: false };

    const spawn = (pt: Particle) => {
      pt.x = Math.random() * width;
      pt.y = Math.random() * height;
      pt.depth = Math.random();
      pt.speed = 0.6 + pt.depth * 1.2;
      pt.maxLife = 140 + Math.random() * 260;
      pt.life = pt.maxLife;
      pt.color = pickColor();
      pt.head = 0;
      pt.count = 0;
    };

    const particles: Particle[] = Array.from({ length: PARTICLE_COUNT }, () => {
      const pt: Particle = {
        x: 0,
        y: 0,
        speed: 0,
        depth: 0,
        life: 0,
        maxLife: 0,
        color: PALETTE[0][0],
        trailX: new Float32Array(TRAIL_LENGTH),
        trailY: new Float32Array(TRAIL_LENGTH),
        head: 0,
        count: 0,
      };
      spawn(pt);
      pt.life = Math.random() * pt.maxLife;
      return pt;
    });

    const spawnTracer = (t: Tracer) => {
      t.x = Math.random() * width;
      t.y = Math.random() * height;
      t.speed = 0.7 + Math.random() * 1.1;
      t.maxLife = 200 + Math.random() * 400;
      t.life = t.maxLife;
      t.color = pickColor();
    };

    const tracers: Tracer[] = Array.from({ length: MESH_TRACERS }, () => {
      const t: Tracer = { x: 0, y: 0, speed: 0, life: 0, maxLife: 0, color: PALETTE[0][0] };
      spawnTracer(t);
      t.life = Math.random() * t.maxLife;
      return t;
    });

    const paintMesh = (passes: number) => {
      for (let i = 0; i < passes && meshPass < MESH_PASSES; i++, meshPass++) {
        for (const t of tracers) {
          const angle = noise(t.x * FIELD_SCALE, t.y * FIELD_SCALE, 0) * TURN;
          const nx = t.x + Math.cos(angle) * t.speed;
          const ny = t.y + Math.sin(angle) * t.speed;
          const fade = Math.min(1, t.life / 30) * Math.min(1, (t.maxLife - t.life) / 30);
          meshCtx.strokeStyle = `rgba(${t.color}, ${MESH_ALPHA * fade})`;
          meshCtx.beginPath();
          meshCtx.moveTo(t.x, t.y);
          meshCtx.lineTo(nx, ny);
          meshCtx.stroke();
          t.x = nx;
          t.y = ny;
          t.life -= 1;
          if (t.life <= 0 || t.x < -EDGE || t.x > width + EDGE || t.y < -EDGE || t.y > height + EDGE) {
            spawnTracer(t);
          }
        }
      }
    };

    const blitMesh = () => {
      ctx.globalAlpha = MESH_OPACITY;
      ctx.drawImage(mesh, 0, 0, width, height);
      ctx.globalAlpha = 1;
    };

    const drawTrail = (p: Particle, points: number, alpha: number) => {
      ctx.strokeStyle = `rgba(${p.color}, ${alpha})`;
      ctx.beginPath();
      let idx = (p.head - points + TRAIL_LENGTH * 2) % TRAIL_LENGTH;
      ctx.moveTo(p.trailX[idx], p.trailY[idx]);
      for (let k = 1; k < points; k++) {
        idx = (idx + 1) % TRAIL_LENGTH;
        ctx.lineTo(p.trailX[idx], p.trailY[idx]);
      }
      ctx.stroke();
    };

    const step = (p: Particle, z: number, dt: number) => {
      let angle = noise(p.x * FIELD_SCALE, p.y * FIELD_SCALE, z) * TURN;
      let boost = 0;

      if (pointer.active) {
        const dx = p.x - pointer.x;
        const dy = p.y - pointer.y;
        const d2 = dx * dx + dy * dy;
        if (d2 < POINTER_RADIUS * POINTER_RADIUS) {
          const influence = 1 - Math.sqrt(d2) / POINTER_RADIUS;
          angle += influence * influence * 2.2;
          boost = influence * 0.35;
        }
      }

      p.x += Math.cos(angle) * p.speed * dt;
      p.y += Math.sin(angle) * p.speed * dt;

      p.trailX[p.head] = p.x;
      p.trailY[p.head] = p.y;
      p.head = (p.head + 1) % TRAIL_LENGTH;
      if (p.count < TRAIL_LENGTH) {
        p.count += 1;
      }

      const fade = Math.min(1, p.life / 25) * Math.min(1, (p.maxLife - p.life) / 25);
      const glow = fade * (0.35 + 0.65 * p.depth);

      if (p.count >= 2) {
        drawTrail(p, p.count, (0.14 + boost * 0.2) * glow);
        drawTrail(p, Math.min(RECENT_LENGTH, p.count), (0.26 + boost * 0.3) * glow);
      }

      ctx.fillStyle = `rgba(${p.color}, ${Math.min(1, (0.7 + boost) * glow)})`;
      ctx.beginPath();
      ctx.arc(p.x, p.y, 0.7 + p.depth * 0.9, 0, TAU);
      ctx.fill();

      p.life -= dt;
      if (p.life <= 0 || p.x < -EDGE || p.x > width + EDGE || p.y < -EDGE || p.y > height + EDGE) {
        spawn(p);
      }
    };

    const reduceMotion =
      typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const ro = new ResizeObserver(() => {
      resize();
      if (reduceMotion) {
        paintMesh(MESH_PASSES);
        ctx.clearRect(0, 0, width, height);
        blitMesh();
      }
    });
    ro.observe(canvas);

    if (reduceMotion) {
      paintMesh(MESH_PASSES);
      blitMesh();
      return () => {
        ro.disconnect();
      };
    }

    const onPointerMove = (e: PointerEvent) => {
      const rect = canvas.getBoundingClientRect();
      pointer.x = e.clientX - rect.left;
      pointer.y = e.clientY - rect.top;
      pointer.active = true;
    };
    const onPointerLeave = () => {
      pointer.active = false;
    };
    window.addEventListener("pointermove", onPointerMove);
    document.addEventListener("pointerleave", onPointerLeave);
    window.addEventListener("blur", onPointerLeave);

    let z = 0;
    let last = performance.now();
    const draw = (now: number) => {
      const dt = Math.min((now - last) / 16.667, 2.5);
      last = now;

      if (meshPass < MESH_PASSES) {
        paintMesh(MESH_PASSES_PER_FRAME);
      }

      ctx.clearRect(0, 0, width, height);
      blitMesh();

      ctx.globalCompositeOperation = "lighter";
      for (const p of particles) {
        step(p, z, dt);
      }
      ctx.globalCompositeOperation = "source-over";

      z += Z_DRIFT * dt;
      rafRef.current = requestAnimationFrame(draw);
    };

    rafRef.current = requestAnimationFrame(draw);

    return () => {
      cancelAnimationFrame(rafRef.current);
      ro.disconnect();
      window.removeEventListener("pointermove", onPointerMove);
      document.removeEventListener("pointerleave", onPointerLeave);
      window.removeEventListener("blur", onPointerLeave);
    };
  }, []);

  return <canvas ref={canvasRef} className={className} aria-hidden="true" />;
};
