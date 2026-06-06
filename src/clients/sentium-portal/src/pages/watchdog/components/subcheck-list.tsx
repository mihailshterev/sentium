import styles from "../watchdog.module.scss";
import type { HealthCheckEntry } from "../../../types/serviceHealth";

const dotClass = (status: string) =>
  status === "Healthy" ? styles.subDotHealthy : status === "Degraded" ? styles.subDotDegraded : styles.subDotUnhealthy;

const SubcheckList = ({ checks }: { checks: HealthCheckEntry[] }) => (
  <div className={styles.subcheckList}>
    {checks.map((c) => (
      <div key={c.name} className={styles.subcheckRow}>
        <span className={`${styles.subDot} ${dotClass(c.status)}`} />
        <span className={styles.subcheckName}>{c.name}</span>
        <span className={styles.subcheckStatus}>{c.status}</span>
        {c.description && <span className={styles.subcheckDesc}>{c.description}</span>}
        <span className={styles.subcheckDuration}>{c.durationMs.toFixed(0)}ms</span>
      </div>
    ))}
  </div>
);

export default SubcheckList;
