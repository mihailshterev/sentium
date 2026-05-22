import { AlertTriangle, Shield, ShieldCheck, ShieldOff, Siren } from "lucide-react";
import styles from "../sentinel.module.scss";
import StatCard from "../../../components/ui/stat-card";
import type { AuditStats } from "../../../types/sentinel";

interface SentinelStatsProps {
  stats: AuditStats | undefined;
  denialRate: number;
}

const SentinelStats = ({ stats, denialRate }: SentinelStatsProps) => (
  <div className={styles.statsRow}>
    <StatCard icon={<Shield size={16} />} value={stats?.total ?? "—"} label="Total Decisions" iconColor="blue" />
    <StatCard icon={<ShieldCheck size={16} />} value={stats?.allowed ?? "—"} label="Allowed" iconColor="green" />
    <StatCard icon={<ShieldOff size={16} />} value={stats?.denied ?? "—"} label="Denied" iconColor="red" />
    <StatCard icon={<Siren size={16} />} value={stats?.alerts ?? "—"} label="Alerts" iconColor="amber" />
    <StatCard
      icon={<AlertTriangle size={16} />}
      value={stats ? `${denialRate}%` : "—"}
      label="Denial Rate"
      iconColor="purple"
    />
  </div>
);

export default SentinelStats;
