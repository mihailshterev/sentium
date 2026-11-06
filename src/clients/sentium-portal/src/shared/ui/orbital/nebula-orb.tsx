import { useEffect, useRef } from "react";
import { readCssColor, startShader } from "@/shared/ui/shader";
import { NEBULA_SHADER } from "./nebula-shader";
import styles from "./nebula-orb.module.css";

interface NebulaOrbProps {
  /** Sizes and positions the orb; it is a square that fills its box. */
  className?: string;
}

/** Procedural nebula rendered into a circle. A static gradient stands in without WebGL. */
export function NebulaOrb({ className }: NebulaOrbProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }
    const runner = startShader(canvas, {
      fragment: NEBULA_SHADER,
      scale: Math.min(window.devicePixelRatio || 1, 1.5),
      fps: 30,
      alpha: true,
      startTime: 12,
      pauseOffscreen: true,
      setup: (gl, uniform) => {
        // The orb is a window into space, so its ground stays dark in both themes.
        gl.uniform3fv(uniform("uBase"), [0.012, 0.016, 0.03]);
        gl.uniform3fv(uniform("uTeal"), readCssColor("--orb-teal", [0.27, 0.78, 0.85]));
        gl.uniform3fv(uniform("uViolet"), readCssColor("--orb-violet", [0.55, 0.42, 0.95]));
        gl.uniform3fv(uniform("uGold"), readCssColor("--orb-gold", [0.85, 0.68, 0.37]));
      },
    });
    return () => runner?.dispose();
  }, []);

  return (
    <div className={[styles.orb, className ?? ""].filter(Boolean).join(" ")} aria-hidden="true">
      <div className={styles.fallback} />
      <canvas ref={canvasRef} className={styles.canvas} />
    </div>
  );
}
