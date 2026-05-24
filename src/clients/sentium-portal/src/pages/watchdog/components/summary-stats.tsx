import { Activity, CheckCircle, XCircle } from "lucide-react";
import styles from "../watchdog.module.scss";
import StatCard from "../../../components/ui/stat-card";
import type { ServiceHealthStatus } from "../../../types/serviceHealth";

interface SummaryStatsProps {
  services: ServiceHealthStatus[];
  healthyCount: number;
  unhealthyCount: number;
  allHealthy: boolean;
}

const SummaryStats = ({ services, healthyCount, unhealthyCount, allHealthy }: SummaryStatsProps) => (
  <div className={styles.summaryRow}>
    <StatCard icon={<Activity size={16} />} value={services.length} label="Monitored" iconColor="blue" />
    <StatCard icon={<CheckCircle size={16} />} value={healthyCount} label="Healthy" iconColor="green" />
    <StatCard
      icon={<XCircle size={16} />}
      value={unhealthyCount}
      label="Unhealthy"
      iconColor={unhealthyCount > 0 ? "red" : "green"}
    />
    <div className={`${styles.overallBadge} ${allHealthy ? styles.overallBadgeGreen : styles.overallBadgeRed}`}>
      {allHealthy ? <CheckCircle size={13} /> : <XCircle size={13} />}
      <span>{allHealthy ? "All Systems Operational" : "Degraded Services Detected"}</span>
    </div>
  </div>
);

export default SummaryStats;
