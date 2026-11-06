import styles from "../system.module.css";
import type { UsageTone } from "../system-utils";

interface ProgressGroupProps {
  name: string;
  value: string;
  percent: number;
  tone?: UsageTone;
}

export function ProgressGroup({ name, value, percent, tone = "neutral" }: ProgressGroupProps) {
  return (
    <div className={styles.progress}>
      <div className={styles.progressLabel}>
        <span className={styles.progressName}>{name}</span>
        <span className={styles.progressValue}>{value}</span>
      </div>
      <div className={styles.progressTrack}>
        <div className={styles.progressFill} data-tone={tone} style={{ width: `${Math.min(percent, 100)}%` }} />
      </div>
    </div>
  );
}
