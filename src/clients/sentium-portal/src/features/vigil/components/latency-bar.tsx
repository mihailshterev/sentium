import styles from "../vigil.module.css";

export function LatencyBar({ latencyMs }: { latencyMs: number }) {
  const pct = Math.min((latencyMs / 1000) * 100, 100);
  const tone = latencyMs < 100 ? "ok" : latencyMs < 400 ? "warn" : "critical";
  return (
    <div className={styles.latencyTrack} aria-hidden="true">
      <div className={styles.latencyFill} data-tone={tone} style={{ width: `${Math.max(pct, 3)}%` }} />
    </div>
  );
}
