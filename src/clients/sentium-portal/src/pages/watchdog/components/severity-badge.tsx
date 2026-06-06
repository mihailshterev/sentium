import styles from "../watchdog.module.scss";
import type { IncidentSeverity, IncidentStatus } from "../../../types/serviceHealth";

interface Props {
  severity: IncidentSeverity;
  status: IncidentStatus;
}

const SeverityBadge = ({ severity, status }: Props) => {
  if (status === "Resolved") {
    return <span className={`${styles.sevBadge} ${styles.sevResolved}`}>Resolved</span>;
  }

  const cls = severity === "Critical" ? styles.sevCritical : styles.sevWarning;
  return <span className={`${styles.sevBadge} ${cls}`}>{severity}</span>;
};

export default SeverityBadge;
