import styles from "../system.module.scss";

interface ProgressGroupProps {
  name: string;
  value: string;
  percent: number;
  fillClass: string;
}

export function ProgressGroup({ name, value, percent, fillClass }: ProgressGroupProps) {
  return (
    <div className={styles.progressGroup}>
      <div className={styles.progressLabel}>
        <span className={styles.progressName}>{name}</span>
        <span className={styles.progressValue}>{value}</span>
      </div>
      <div className={styles.progressTrack}>
        <div className={`${styles.progressFill} ${fillClass}`} style={{ width: `${Math.min(percent, 100)}%` }} />
      </div>
    </div>
  );
}
