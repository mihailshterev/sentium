import styles from "../vigil.module.css";
import type { HealthCheckEntry } from "@/features/vigil/types/serviceHealth";

export function SubcheckList({ checks }: { checks: HealthCheckEntry[] }) {
  return (
    <ul className={styles.subchecks}>
      {checks.map((c) => (
        <li key={c.name} className={styles.subcheck} data-status={c.status}>
          <span className={styles.subDot} aria-hidden="true" />
          <span className={styles.subName}>{c.name}</span>
          <span className={styles.subStatus}>{c.status}</span>
          {c.description && <span className={styles.subDesc}>{c.description}</span>}
          <span className={styles.subDuration}>{c.durationMs.toFixed(0)}ms</span>
        </li>
      ))}
    </ul>
  );
}
