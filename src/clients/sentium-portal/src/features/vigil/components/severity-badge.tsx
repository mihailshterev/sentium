import styles from "../vigil.module.scss";
import type { IncidentSeverity, IncidentStatus } from "@/features/vigil/types/serviceHealth";

interface Props {
  severity: IncidentSeverity;
  status: IncidentStatus;
}

export function SeverityBadge({ severity, status }: Props) {
  if (status === "Resolved") {
    return <span className={`${styles.sevBadge} ${styles.sevResolved}`}>Resolved</span>;
  }

  const cls = severity === "Critical" ? styles.sevCritical : styles.sevWarning;
  return <span className={`${styles.sevBadge} ${cls}`}>{severity}</span>;
}


