import { useEffect, useRef } from "react";
import { latticeFrame, type LatticeFrame } from "./lattice-geometry";
import styles from "./lattice-core.module.css";

interface LatticeCoreProps {
  cx: number;
  cy: number;
  radius: number;
}

/** Lattice time at mount; also the static frame shown under reduced motion and without animation frames. */
const START_TIME = 9;
const FRAME_INTERVAL_MS = 1000 / 30;

/** Writes a frame onto the elements rendered below, which keep the frame's order. */
function paint(group: SVGGElement, frame: LatticeFrame) {
  const lines = group.querySelectorAll("line");
  [...frame.outer, ...frame.inner, ...frame.pulses].forEach((segment, i) => {
    const line = lines[i];
    line.setAttribute("x1", String(segment.x1));
    line.setAttribute("y1", String(segment.y1));
    line.setAttribute("x2", String(segment.x2));
    line.setAttribute("y2", String(segment.y2));
    line.setAttribute("opacity", String(segment.opacity));
  });
  const circles = group.querySelectorAll("circle");
  const heads = frame.pulses.map((pulse) => ({ x: pulse.x2, y: pulse.y2, r: 2.2, opacity: pulse.opacity }));
  [...frame.vertices, ...heads].forEach((dot, i) => {
    const circle = circles[i];
    circle.setAttribute("cx", String(dot.x));
    circle.setAttribute("cy", String(dot.y));
    circle.setAttribute("r", String(dot.r));
    circle.setAttribute("opacity", String(dot.opacity));
  });
}

/**
 * Centrepiece of the orbital map: a wireframe lattice turning around a bright core while signal pulses
 * run along its edges. Drawn as SVG inside the map's view box, so it takes its colours from the theme.
 */
export function LatticeCore({ cx, cy, radius }: LatticeCoreProps) {
  const groupRef = useRef<SVGGElement>(null);
  const initial = latticeFrame(START_TIME, cx, cy, radius);

  useEffect(() => {
    const group = groupRef.current;
    if (!group) {
      return;
    }
    const reducedMotion =
      typeof window.matchMedia === "function" ? window.matchMedia("(prefers-reduced-motion: reduce)") : null;
    let time = START_TIME;
    let raf = 0;
    let last = 0;
    let visible = true;

    const tick = (now: number) => {
      raf = requestAnimationFrame(tick);
      const elapsed = now - last;
      if (elapsed < FRAME_INTERVAL_MS) {
        return;
      }
      last = now;
      time += Math.min(elapsed, 100) / 1000;
      paint(group, latticeFrame(time, cx, cy, radius));
    };

    const sync = () => {
      const run = visible && !document.hidden && !reducedMotion?.matches;
      if (run && !raf) {
        last = performance.now();
        raf = requestAnimationFrame(tick);
      } else if (!run && raf) {
        cancelAnimationFrame(raf);
        raf = 0;
      }
    };

    const intersection =
      typeof IntersectionObserver !== "undefined"
        ? new IntersectionObserver(([entry]) => {
            visible = entry.isIntersecting;
            sync();
          })
        : null;
    intersection?.observe(group);
    document.addEventListener("visibilitychange", sync);
    reducedMotion?.addEventListener("change", sync);
    sync();

    return () => {
      cancelAnimationFrame(raf);
      intersection?.disconnect();
      document.removeEventListener("visibilitychange", sync);
      reducedMotion?.removeEventListener("change", sync);
    };
  }, [cx, cy, radius]);

  return (
    <g className={styles.root} data-lattice="">
      <circle className={styles.coreRing} cx={cx} cy={cy} r={radius * 0.16} />
      <circle className={styles.core} cx={cx} cy={cy} r={3.5} />
      <g ref={groupRef}>
        <g className={styles.outer}>
          {initial.outer.map((s, i) => (
            <line key={i} x1={s.x1} y1={s.y1} x2={s.x2} y2={s.y2} opacity={s.opacity} />
          ))}
        </g>
        <g className={styles.inner}>
          {initial.inner.map((s, i) => (
            <line key={i} x1={s.x1} y1={s.y1} x2={s.x2} y2={s.y2} opacity={s.opacity} />
          ))}
        </g>
        <g className={styles.pulses}>
          {initial.pulses.map((s, i) => (
            <line key={i} x1={s.x1} y1={s.y1} x2={s.x2} y2={s.y2} opacity={s.opacity} />
          ))}
        </g>
        <g className={styles.vertices}>
          {initial.vertices.map((d, i) => (
            <circle key={i} cx={d.x} cy={d.y} r={d.r} opacity={d.opacity} />
          ))}
        </g>
        <g className={styles.heads}>
          {initial.pulses.map((s, i) => (
            <circle key={i} cx={s.x2} cy={s.y2} r={2.2} opacity={s.opacity} />
          ))}
        </g>
      </g>
    </g>
  );
}
