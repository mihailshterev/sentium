import { useState } from "react";
import { ChevronRight, Clock } from "lucide-react";
import styles from "../vigil.module.css";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";
import { formatTimeHms } from "../../../shared/utils/formatters";
import { LatencyBar } from "./latency-bar";
import { StatusIcon } from "./status-icon";
import { SubcheckList } from "./subcheck-list";

export function ServiceRow({ service }: { service: ServiceHealthStatus }) {
  const [expanded, setExpanded] = useState(false);
  const hasChecks = service.checks && service.checks.length > 0;

  return (
    <div className={styles.serviceWrap} data-status={service.status}>
      <div
        className={styles.serviceRow}
        data-clickable={hasChecks || undefined}
        onClick={hasChecks ? () => setExpanded((e) => !e) : undefined}
        aria-expanded={hasChecks ? expanded : undefined}
      >
        <div className={styles.statusCell}>
          <ChevronRight
            size={14}
            className={styles.expandChevron}
            data-open={expanded || undefined}
            data-hidden={!hasChecks || undefined}
            aria-hidden="true"
          />
          <StatusIcon status={service.status} />
          <span className={styles.statusLabel}>{service.status}</span>
        </div>

        <div className={styles.nameCell}>
          <span className={styles.serviceName}>{service.serviceName}</span>
          <span className={styles.uptime}>{service.uptimePercent.toFixed(1)}% uptime</span>
        </div>

        <div className={styles.latencyCell}>
          <span className={styles.latencyValue}>{service.latencyMs.toFixed(0)}ms</span>
          <LatencyBar latencyMs={service.latencyMs} />
        </div>

        <div className={styles.checkedCell}>
          <Clock size={13} aria-hidden="true" />
          <span>{service.checkedAt ? formatTimeHms(service.checkedAt) : "-"}</span>
        </div>

        {service.details && <span className={styles.details}>{service.details}</span>}
      </div>

      {expanded && hasChecks && <SubcheckList checks={service.checks} />}
    </div>
  );
}
