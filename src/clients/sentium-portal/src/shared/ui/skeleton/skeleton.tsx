import styles from "./skeleton.module.css";

interface SkeletonProps {
  width?: number | string;
  height?: number | string;
  /** Pill/circle shapes for avatars and badges. */
  radius?: "sm" | "md" | "full";
  className?: string;
}

/** Shimmering placeholder block. Decorative: mark the loading region itself with aria-busy. */
export function Skeleton({ width, height = 12, radius = "sm", className }: SkeletonProps) {
  return (
    <span
      className={[styles.skeleton, styles[radius], className ?? ""].filter(Boolean).join(" ")}
      style={{ width, height }}
      aria-hidden="true"
    />
  );
}

interface SkeletonTextProps {
  lines?: number;
  className?: string;
}

/** A few text-like lines, the last one shorter. */
export function SkeletonText({ lines = 3, className }: SkeletonTextProps) {
  return (
    <span className={[styles.text, className ?? ""].filter(Boolean).join(" ")} aria-hidden="true">
      {Array.from({ length: lines }, (_, i) => (
        <Skeleton key={i} width={i === lines - 1 && lines > 1 ? "60%" : "100%"} />
      ))}
    </span>
  );
}
