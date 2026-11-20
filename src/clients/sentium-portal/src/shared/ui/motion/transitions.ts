export interface Timing {
  /** Milliseconds. */
  duration: number;
  easing?: string;
}

/** A visual state an element animates from (on enter) or to (on exit); missing values mean "as laid out". */
export interface Pose {
  opacity?: number;
  x?: number;
  y?: number;
  scaleY?: number;
}

export const EASE_DEFAULT = "cubic-bezier(0.25, 0.1, 0.35, 1)";
export const EASE_OUT_EXPO = "cubic-bezier(0.16, 1, 0.3, 1)";
export const EASE_OUT_QUINT = "cubic-bezier(0.22, 1, 0.36, 1)";
export const EASE_IN = "cubic-bezier(0.42, 0, 1, 1)";

export const DEFAULT_TIMING: Timing = { duration: 300, easing: EASE_DEFAULT };
