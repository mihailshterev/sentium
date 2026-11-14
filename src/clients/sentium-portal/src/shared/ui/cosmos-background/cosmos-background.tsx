import { useEffect, useRef } from "react";
import { readCssColor, readCssNumber, startShader, type ShaderRunner } from "@/shared/ui/shader";
import { FRAGMENT_SHADER } from "./cosmos-shader";
import styles from "./cosmos-background.module.css";

/** Fraction of CSS pixels rendered by the shader; haze is soft and stars tolerate the upscale. */
const RENDER_SCALE = 0.5;

/** Celestial chart drawn above the field: rings, crosshair and a dashed transfer orbit. */
function Chart() {
  const rings = [140, 260, 420, 600, 820, 1080, 1380];
  return (
    <svg className={styles.chart} viewBox="-2000 -2000 4000 4000" aria-hidden="true">
      <g className={styles.lines}>
        <line x1="-2000" y1="0" x2="2000" y2="0" />
        <line x1="0" y1="-2000" x2="0" y2="2000" />
        {rings.map((r, i) => (
          <circle key={r} r={r} className={i % 3 === 2 ? styles.dashed : undefined} />
        ))}
        <path className={styles.dashed} d="M -1500 900 A 1700 1700 0 0 1 900 -1500" />
        <rect x="-14" y="-14" width="28" height="28" />
      </g>
      <g className={styles.labels}>
        <text x="16" y="-12">
          RA 14h 29m 43s
        </text>
        <text x="16" y="22">
          DEC −62° 40′ 46″
        </text>
        <text x="-1070" y="-8">
          r 1.08e3
        </text>
      </g>
    </svg>
  );
}

/** Slowly rotating meridians and a tick ring (rotated as a compositor layer, never repainted). */
function Orrery() {
  const ticks = Array.from({ length: 120 }, (_, i) => i * 3);
  return (
    <div className={styles.orrery} aria-hidden="true">
      <svg viewBox="-1100 -1100 2200 2200">
        <g className={styles.lines}>
          {[230, 480, 760].map((rx) => (
            <ellipse key={rx} rx={rx} ry="1080" />
          ))}
          <ellipse rx="1080" ry="300" />
          {ticks.map((deg) => (
            <line
              key={deg}
              x1="0"
              y1={-600}
              x2="0"
              y2={deg % 30 === 0 ? -628 : -612}
              transform={`rotate(${deg})`}
              className={deg % 30 === 0 ? styles.tickMajor : undefined}
            />
          ))}
        </g>
      </svg>
    </div>
  );
}

interface CosmosBackgroundProps {
  /** Stops animating (keeps the last frame), e.g. while a page covers the background entirely. */
  paused?: boolean;
}

/**
 * Deep-space backdrop behind the whole app: shader haze + stars, an isometric point grid and a celestial
 * coordinate chart. A static CSS composition from the same tokens covers WebGL-less environments.
 */
export function CosmosBackground({ paused = false }: CosmosBackgroundProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const runnerRef = useRef<ShaderRunner | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }
    const dark: [number, number, number] = [0.02, 0.03, 0.05];
    const runner = startShader(canvas, {
      fragment: FRAGMENT_SHADER,
      scale: RENDER_SCALE,
      fps: 24,
      startTime: 40,
      setup: (gl, uniform) => {
        gl.uniform3fv(uniform("uBase"), readCssColor("--field-base", dark));
        gl.uniform3fv(uniform("uHazeA"), readCssColor("--field-1", dark));
        gl.uniform3fv(uniform("uHazeB"), readCssColor("--field-2", dark));
        gl.uniform3fv(uniform("uHazeC"), readCssColor("--field-3", dark));
        gl.uniform3fv(uniform("uStar"), readCssColor("--field-star", [0.9, 0.92, 1]));
        gl.uniform1f(uniform("uStars"), readCssNumber("--field-stars", 1));
      },
    });
    runnerRef.current = runner;
    return () => {
      runner?.dispose();
      runnerRef.current = null;
    };
  }, []);

  // Declared after the runner effect so it also applies on mount.
  useEffect(() => {
    runnerRef.current?.setPaused(paused);
  }, [paused]);

  return (
    <div className={styles.root} data-paused={paused || undefined} aria-hidden="true">
      <div className={styles.fallback} />
      <canvas ref={canvasRef} className={styles.canvas} />
      <div className={styles.grid} />
      <div className={styles.center}>
        <Chart />
      </div>
      <div className={styles.orreryCenter}>
        <Orrery />
      </div>
      <div className={styles.vignette} />
    </div>
  );
}
