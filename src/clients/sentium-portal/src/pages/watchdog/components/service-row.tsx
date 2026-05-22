import { Clock } from "lucide-react";
import styles from "../watchdog.module.scss";
import type { ServiceHealthStatus } from "../../../types/serviceHealth";
import { formatTimeHms } from "../../../utils/formatters";
import StatusIcon from "./status-icon";
import LatencyBar from "./latency-bar";

const ServiceRow = ({ service }: { service: ServiceHealthStatus }) => (
  <div className={`${styles.serviceRow} ${service.status === "Unhealthy" ? styles.serviceRowUnhealthy : ""}`}>
    <div className={styles.serviceRowStatus}>
      <StatusIcon status={service.status} />
      <span
        className={`${styles.statusLabel} ${
          service.status === "Healthy"
            ? styles.statusLabelHealthy
            : service.status === "Unhealthy"
              ? styles.statusLabelUnhealthy
              : styles.statusLabelUnknown
        }`}
      >
        {service.status}
      </span>
    </div>

    <span className={styles.serviceName}>{service.serviceName}</span>

    <div className={styles.latencyCell}>
      <span className={styles.latencyValue}>{service.latencyMs.toFixed(0)}ms</span>
      <LatencyBar latencyMs={service.latencyMs} />
    </div>

    <div className={styles.checkedCell}>
      <Clock size={11} />
      <span>{service.checkedAt ? formatTimeHms(service.checkedAt) : "—"}</span>
    </div>

    {service.details && <span className={styles.detailsCell}>{service.details}</span>}
  </div>
);

export default ServiceRow;
