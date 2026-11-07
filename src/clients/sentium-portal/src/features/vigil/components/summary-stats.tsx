import { Activity, AlertTriangle, CheckCircle, ShieldAlert, XCircle } from "lucide-react";
import styles from "../vigil.module.css";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";
import type { ServiceStatus } from "@/features/vigil/types/serviceHealth";

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

export function SummaryStats({ total, healthy, degraded, unhealthy, openIncidents, overallStatus }: SummaryStatsProps) {
  const Icon = overallStatus === "Healthy" ? CheckCircle : overallStatus === "Degraded" ? AlertTriangle : XCircle;

  return (
    <StatGrid minCellWidth={140}>
      <div className={styles.overall} data-status={overallStatus}>
        <span className={styles.overallIcon} aria-hidden="true">
          <Icon size={20} />
        </span>
        <span className={styles.overallText}>
          <span className={styles.overallLabel}>Overall status</span>
          <span className={styles.overallValue}>{overallText(overallStatus)}</span>
        </span>
      </div>
      <StatCard icon={<Activity />} value={total} label="Monitored" />
      <StatCard icon={<CheckCircle />} value={healthy} label="Healthy" iconColor="green" />
      <StatCard
        icon={<AlertTriangle />}
        value={degraded}
        label="Degraded"
        iconColor={degraded > 0 ? "amber" : "neutral"}
      />
      <StatCard icon={<XCircle />} value={unhealthy} label="Unhealthy" iconColor={unhealthy > 0 ? "red" : "neutral"} />
      <StatCard
        icon={<ShieldAlert />}
        value={openIncidents}
        label="Open Incidents"
        iconColor={openIncidents > 0 ? "red" : "neutral"}
      />
    </StatGrid>
  );
}
