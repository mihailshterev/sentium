import { useState } from "react";
import { Clock, ChevronRight } from "lucide-react";
import styles from "../watchdog.module.scss";
import type { ServiceHealthStatus, ServiceStatus } from "../../../types/serviceHealth";
import { formatTimeHms } from "../../../utils/formatters";
import StatusIcon from "./status-icon";
import LatencyBar from "./latency-bar";
import SubcheckList from "./subcheck-list";

const statusLabelClass = (status: ServiceStatus) =>
  status === "Healthy"
    ? styles.statusLabelHealthy
    : status === "Degraded"
      ? styles.statusLabelDegraded
      : status === "Unhealthy"
        ? styles.statusLabelUnhealthy
        : styles.statusLabelUnknown;

const ServiceRow = ({ service }: { service: ServiceHealthStatus }) => {
  const [expanded, setExpanded] = useState(false);
  const hasChecks = service.checks && service.checks.length > 0;

  const rowClass =
    service.status === "Unhealthy"
      ? styles.serviceRowUnhealthy
      : service.status === "Degraded"
        ? styles.serviceRowDegraded
        : "";

  return (
    <div className={styles.serviceRowWrap}>
      <div
        className={`${styles.serviceRow} ${rowClass} ${hasChecks ? styles.serviceRowClickable : ""}`}
        onClick={hasChecks ? () => setExpanded((e) => !e) : undefined}
      >
        <div className={styles.serviceRowStatus}>
          {hasChecks && (
            <ChevronRight size={12} className={`${styles.expandChevron} ${expanded ? styles.expandChevronOpen : ""}`} />
          )}
          <StatusIcon status={service.status} />
          <span className={`${styles.statusLabel} ${statusLabelClass(service.status)}`}>{service.status}</span>
        </div>

        <div className={styles.serviceNameCell}>
          <span className={styles.serviceName}>{service.serviceName}</span>
          <span className={styles.uptimeText}>{service.uptimePercent.toFixed(1)}% uptime</span>
        </div>

        <div className={styles.latencyCell}>
          <span className={styles.latencyValue}>{service.latencyMs.toFixed(0)}ms</span>
          <LatencyBar latencyMs={service.latencyMs} />
        </div>

        <div className={styles.checkedCell}>
          <Clock size={11} />
          <span>{service.checkedAt ? formatTimeHms(service.checkedAt) : "-"}</span>
        </div>

        {service.details && <span className={styles.detailsCell}>{service.details}</span>}
      </div>

      {expanded && hasChecks && <SubcheckList checks={service.checks} />}
    </div>
  );
};

export default ServiceRow;
