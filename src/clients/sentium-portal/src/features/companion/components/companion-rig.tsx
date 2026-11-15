import { useEffect, useRef } from "react";
import { useMediaQuery } from "@/shared/hooks/useMediaQuery";
import { microphoneLevel } from "../audio/push-to-talk";
import { speechPlayer } from "../audio/speech-player";
import type { CompanionState } from "../types/companion";
import styles from "./companion-rig.module.css";

const FULL_VIEW_BOX = "0 0 120 132";
/** Head and antenna only, for the collapsed sidebar. */
const HEAD_VIEW_BOX = "20 2 80 82";
/** How far the eyes travel toward the pointer, in SVG units. */
const MAX_GLANCE = 2.6;

interface CompanionRigProps {
  state: CompanionState;
  /** Shows only the head. */
  compact?: boolean;
  className?: string;
}

/**
 * The Companion's body: an inline SVG rig whose parts are separate groups, posed per state by CSS from the
 * `data-state` attribute. Colours come from the design tokens, so it follows the theme. Decorative here; the
 * button around it carries the accessible name.
 */
export function CompanionRig({ state, compact = false, className }: CompanionRigProps) {
  const ref = useRef<SVGSVGElement>(null);
  const reducedMotion = useMediaQuery("(prefers-reduced-motion: reduce)");

  // Mouth (speaking) and antenna (listening) follow the live audio level without re-rendering React.
  const levelSource = state === "speaking" ? speechPlayer.level : state === "listening" ? microphoneLevel : null;
  useEffect(() => {
    const svg = ref.current;
    if (!svg || !levelSource || reducedMotion) {
      return;
    }

    let frame = 0;
    let smoothed = 0;
    const tick = () => {
      smoothed += (levelSource() - smoothed) * 0.35;
      svg.style.setProperty("--level", smoothed.toFixed(3));
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);

    return () => {
      cancelAnimationFrame(frame);
      svg.style.removeProperty("--level");
    };
  }, [levelSource, reducedMotion]);

  // Idle: the eyes glance toward the pointer.
  const glances = state === "idle" && !reducedMotion;
  useEffect(() => {
    const svg = ref.current;
    if (!svg || !glances) {
      return;
    }

    let frame = 0;
    const onMove = (event: PointerEvent) => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        const box = svg.getBoundingClientRect();
        const dx = event.clientX - (box.left + box.width / 2);
        const dy = event.clientY - (box.top + box.height / 2);
        const distance = Math.hypot(dx, dy) || 1;
        const reach = Math.min(1, distance / 320) * MAX_GLANCE;
        svg.style.setProperty("--look-x", `${((dx / distance) * reach).toFixed(2)}px`);
        svg.style.setProperty("--look-y", `${((dy / distance) * reach).toFixed(2)}px`);
      });
    };

    window.addEventListener("pointermove", onMove, { passive: true });
    return () => {
      window.removeEventListener("pointermove", onMove);
      cancelAnimationFrame(frame);
      svg.style.removeProperty("--look-x");
      svg.style.removeProperty("--look-y");
    };
  }, [glances]);

  return (
    <svg
      ref={ref}
      className={[styles.rig, className ?? ""].filter(Boolean).join(" ")}
      viewBox={compact ? HEAD_VIEW_BOX : FULL_VIEW_BOX}
      data-state={state}
      data-compact={compact || undefined}
      aria-hidden="true"
      focusable="false"
    >
      <ellipse className={styles.shadow} cx="60" cy="124" rx="24" ry="3.5" />

      <g className={styles.figure}>
        <g className={styles.body}>
          <path className={`${styles.arm} ${styles.armLeft}`} d="M45 90 Q35 93 34 102" />
          <path className={`${styles.arm} ${styles.armRight}`} d="M75 90 Q85 93 86 102" />
          <path className={styles.torso} d="M44 84 Q60 78 76 84 L72 105 Q60 112 48 105 Z" />
          <circle className={styles.core} cx="60" cy="95" r="3.2" />
        </g>

        <g className={styles.head}>
          <g className={styles.antenna}>
            <path className={styles.stalk} d="M60 30 V17" />
            <circle className={styles.orbHalo} cx="60" cy="12" r="5" />
            <circle className={styles.orb} cx="60" cy="12" r="4" />
            <g className={styles.orbit}>
              <circle className={styles.satellite} cx="60" cy="3.5" r="1.6" />
            </g>
          </g>

          <rect className={styles.ear} x="21.5" y="48" width="5" height="13" rx="2.5" />
          <rect className={styles.ear} x="93.5" y="48" width="5" height="13" rx="2.5" />
          <rect className={styles.shell} x="26" y="30" width="68" height="50" rx="24" />
          <rect className={styles.visor} x="33" y="38" width="54" height="33" rx="16" />

          <g className={styles.eyes}>
            <rect className={styles.eye} x="44.5" y="46" width="9" height="13" rx="4.5" />
            <rect className={styles.eye} x="66.5" y="46" width="9" height="13" rx="4.5" />
          </g>

          <path className={styles.smile} d="M55 64.5 Q60 67.5 65 64.5" />
          <ellipse className={styles.mouth} cx="60" cy="65" rx="3.6" ry="2.6" />
        </g>
      </g>
    </svg>
  );
}
