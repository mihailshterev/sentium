import { Activity, CheckCircle, XCircle, AlertTriangle, ShieldAlert } from "lucide-react";
import styles from "../watchdog.module.scss";
import StatCard from "../../../components/ui/stat-card";
import type { ServiceStatus } from "../../../types/serviceHealth";

interface SummaryStatsProps {
  total: number;
  healthy: number;
  degraded: number;
  unhealthy: number;
  openIncidents: number;
  overallStatus: ServiceStatus;
}

const overallText = (status: ServiceStatus) => {
  switch (status) {
    case "Healthy":
      return "All Systems Operational";
    case "Degraded":
      return "Degraded Performance";
    case "Unhealthy":
      return "Outage Detected";
    default:
      return "Awaiting Data";
  }
};

const SummaryStats = ({ total, healthy, degraded, unhealthy, openIncidents, overallStatus }: SummaryStatsProps) => {
  const badgeClass =
    overallStatus === "Healthy"
      ? styles.overallBadgeGreen
      : overallStatus === "Degraded"
        ? styles.overallBadgeAmber
        : styles.overallBadgeRed;

  return (
    <div className={styles.summaryRow}>
      <StatCard icon={<Activity size={16} />} value={total} label="Monitored" iconColor="blue" />
      <StatCard icon={<CheckCircle size={16} />} value={healthy} label="Healthy" iconColor="green" />
      <StatCard
        icon={<AlertTriangle size={16} />}
        value={degraded}
        label="Degraded"
        iconColor={degraded > 0 ? "amber" : "green"}
      />
      <StatCard
        icon={<XCircle size={16} />}
        value={unhealthy}
        label="Unhealthy"
        iconColor={unhealthy > 0 ? "red" : "green"}
      />
      <StatCard
        icon={<ShieldAlert size={16} />}
        value={openIncidents}
        label="Open Incidents"
        iconColor={openIncidents > 0 ? "red" : "green"}
      />
      <div className={`${styles.overallBadge} ${badgeClass}`}>
        {overallStatus === "Healthy" ? (
          <CheckCircle size={13} />
        ) : overallStatus === "Degraded" ? (
          <AlertTriangle size={13} />
        ) : (
          <XCircle size={13} />
        )}
        <span>{overallText(overallStatus)}</span>
      </div>
    </div>
  );
};

export default SummaryStats;
