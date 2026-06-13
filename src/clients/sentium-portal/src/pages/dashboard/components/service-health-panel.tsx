import { Activity } from "lucide-react";
import styles from "../dashboard.module.scss";
import EmptyState from "../../../components/ui/empty-state";
import type { ServiceHealthStatus } from "../../../types/serviceHealth";

interface ServiceHealthPanelProps {
  services: ServiceHealthStatus[];
  loading: boolean;
}

function dotClass(status: string): string {
  if (status === "Healthy") {
    return styles.healthDotHealthy;
  }
  if (status === "Degraded") {
    return styles.healthDotDegraded;
  }
  if (status === "Unhealthy") {
    return styles.healthDotUnhealthy;
  }
  return styles.healthDotUnknown;
}

function statusClass(status: string): string {
  if (status === "Healthy") {
    return styles.healthStatusHealthy;
  }
  if (status === "Degraded") {
    return styles.healthStatusDegraded;
  }
  if (status === "Unhealthy") {
    return styles.healthStatusUnhealthy;
  }
  return styles.healthStatusUnknown;
}

const SkeletonRow = () => (
  <div className={styles.healthRow}>
    <span className={`${styles.healthDot} ${styles.skeletonDot}`} style={{ width: 7, height: 7 }} />
    <span className={`${styles.skeletonLine} ${styles.skeletonLineLong}`} style={{ flex: 1 }} />
    <span className={`${styles.skeletonLine} ${styles.skeletonLineShort}`} />
  </div>
);

const ServiceHealthPanel = ({ services, loading }: ServiceHealthPanelProps) => {
  const healthyCount = services.filter((s) => s.status === "Healthy").length;

  return (
    <section className={styles.section}>
      <div className={styles.sectionHeader}>
        <span className={styles.sectionTitle}>Service Health</span>
        <span className={styles.healthCountChip}>{loading ? "…" : `${healthyCount}/${services.length}`}</span>
      </div>

      <div className={styles.healthList}>
        {loading ? (
          [0, 1, 2, 3].map((i) => <SkeletonRow key={i} />)
        ) : services.length === 0 ? (
          <EmptyState icon={<Activity size={18} />} title="No services monitored" />
        ) : (
          services.slice(0, 8).map((s) => (
            <div key={s.serviceName} className={styles.healthRow}>
              <span className={`${styles.healthDot} ${dotClass(s.status)}`} />
              <span className={styles.healthName}>{s.serviceName}</span>
              <span className={`${styles.healthStatus} ${statusClass(s.status)}`}>{s.status}</span>
              <span className={styles.latencyBadge}>{s.latencyMs.toFixed(0)}ms</span>
            </div>
          ))
        )}
      </div>
    </section>
  );
};

export default ServiceHealthPanel;
