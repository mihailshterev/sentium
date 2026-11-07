import { AlertTriangle, Shield, ShieldCheck, ShieldOff, Siren } from "lucide-react";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";
import type { AuditStats } from "@/features/sentinel/types/sentinel";

interface SentinelStatsProps {
  stats: AuditStats | undefined;
  denialRate: number;
}

export function SentinelStats({ stats, denialRate }: SentinelStatsProps) {
  return (
    <StatGrid>
      <StatCard icon={<Shield />} value={stats?.total ?? "—"} label="Total Decisions" />
      <StatCard icon={<ShieldCheck />} value={stats?.allowed ?? "—"} label="Allowed" iconColor="green" />
      <StatCard icon={<ShieldOff />} value={stats?.denied ?? "—"} label="Denied" iconColor="red" />
      <StatCard icon={<Siren />} value={stats?.alerts ?? "—"} label="Alerts" iconColor="amber" />
      <StatCard
        icon={<AlertTriangle />}
        value={stats ? `${denialRate}%` : "—"}
        label="Denial Rate"
        iconColor="neutral"
      />
    </StatGrid>
  );
}
