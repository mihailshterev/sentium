import { Link } from "react-router";
import styles from "../dashboard.module.css";
import { RESOURCE_WARNING_PERCENT } from "../hooks/useAttention";
import type { AuditStats } from "@/features/sentinel/types/sentinel";
import type { SystemMetrics } from "@/features/system/types/system";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";
import type { WorkflowRun } from "@/features/workflows/types/workflows";
import { Skeleton } from "@/shared/ui/skeleton";

interface KpiProps {
  label: string;
  value: string | number;
  context: string;
  to: string;
  warn?: boolean;
  loading: boolean;
}

function Kpi({ label, value, context, to, warn = false, loading }: KpiProps) {
  return (
    <Link
      to={to}
      className={`frame ${styles.kpi}`}
      data-warn={warn || undefined}
      aria-label={loading ? label : `${label}: ${value}, ${context}`}
    >
      <span className={styles.kpiLabel}>{label}</span>
      {loading ? <Skeleton width={56} height={28} /> : <span className={styles.kpiValue}>{value}</span>}
      <span className={styles.kpiContext}>{!loading && context}</span>
    </Link>
  );
}

const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? "" : "s"}`;

interface KpiGridProps {
  services: ServiceHealthStatus[];
  metrics?: SystemMetrics | null;
  stats?: AuditStats | null;
  runs: WorkflowRun[];
  agentCount: number;
  workflowCount: number;
  modelCount: number;
  collectionCount: number;
  jobCount: number;
  loading: boolean;
}

/** Headline numbers, each with the context that makes it actionable and a link to the page behind it. */
export function KpiGrid({
  services,
  metrics,
  stats,
  runs,
  agentCount,
  workflowCount,
  modelCount,
  collectionCount,
  jobCount,
  loading,
}: KpiGridProps) {
  const healthy = services.filter((s) => s.status === "Healthy").length;
  const memory = metrics?.memory.memoryLoadPercent;
  const riskyRuns = runs.filter((r) => r.risk === "High" || r.risk === "Critical").length;
  const highRiskCalls = (stats?.highRisk ?? 0) + (stats?.criticalRisk ?? 0);

  return (
    <section className={styles.kpiGrid} aria-label="Key figures">
      <Kpi
        label="Services"
        value={`${healthy}/${services.length}`}
        context={healthy === services.length ? "all healthy" : `${services.length - healthy} not healthy`}
        to="/vigil"
        warn={healthy < services.length}
        loading={loading}
      />
      <Kpi
        label="Host memory"
        value={memory === undefined ? "—" : `${Math.round(memory)}%`}
        context={metrics ? `CPU ${metrics.cpu.processCpuPercent.toFixed(1)}%` : "no data"}
        to="/system"
        warn={memory !== undefined && memory >= RESOURCE_WARNING_PERCENT}
        loading={loading}
      />
      <Kpi
        label="Tool calls"
        value={stats?.total ?? 0}
        context={`${stats?.denied ?? 0} denied · ${highRiskCalls} high risk`}
        to="/sentinel"
        warn={highRiskCalls > 0}
        loading={loading}
      />
      <Kpi
        label="Recent runs"
        value={runs.length}
        context={riskyRuns > 0 ? `${riskyRuns} rated high risk` : "none rated high risk"}
        to="/orchestration"
        warn={riskyRuns > 0}
        loading={loading}
      />
      <Kpi
        label="Agents"
        value={agentCount}
        context={`${plural(workflowCount, "workflow")} · ${plural(modelCount, "model")}`}
        to="/agents"
        loading={loading}
      />
      <Kpi
        label="Knowledge"
        value={collectionCount}
        context={`collections · ${plural(jobCount, "scheduled job")}`}
        to="/knowledge-base"
        loading={loading}
      />
    </section>
  );
}
