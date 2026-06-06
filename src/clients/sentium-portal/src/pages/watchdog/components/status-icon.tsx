import { CheckCircle, XCircle, AlertCircle, AlertTriangle } from "lucide-react";
import styles from "../watchdog.module.scss";
import type { ServiceStatus } from "../../../types/serviceHealth";

const StatusIcon = ({ status }: { status: ServiceStatus }) => {
  if (status === "Healthy") {
    return <CheckCircle size={14} className={styles.iconHealthy} />;
  }

  if (status === "Degraded") {
    return <AlertTriangle size={14} className={styles.iconDegraded} />;
  }

  if (status === "Unhealthy") {
    return <XCircle size={14} className={styles.iconUnhealthy} />;
  }

  return <AlertCircle size={14} className={styles.iconUnknown} />;
};

export default StatusIcon;
