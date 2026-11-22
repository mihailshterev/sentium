import { useNotifications } from "@/features/notifications/hooks/useInbox";
import type { Notification } from "@/features/notifications/types/notification";
import { useSentinelStats } from "@/features/sentinel/hooks/useSentinelAudit";
import type { AuditStats } from "@/features/sentinel/types/sentinel";
import type { SystemMetrics } from "@/features/system/types/system";
import { useIncidents } from "@/features/vigil/hooks/useIncidents";
import { useServiceHealth } from "@/features/vigil/hooks/useServiceHealth";
import { useSystemMetrics } from "@/features/vigil/hooks/useSystemMetrics";
import type { Incident, ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";
import { useWorkflowRuns } from "@/features/workflows/hooks/useWorkflowRuns";
import type { WorkflowRun } from "@/features/workflows/types/workflows";
import { describeRun } from "@/features/workflows/utils/describe-run";

export type AttentionTone = "critical" | "warning";

export interface AttentionItem {
  id: string;
  tone: AttentionTone;
  source: string;
  title: string;
  detail?: string;
  to: string;
  at?: string;
}

export interface AttentionInputs {
  services: ServiceHealthStatus[];
  incidents: Incident[];
  metrics?: SystemMetrics | null;
  stats?: AuditStats | null;
  runs: WorkflowRun[];
  notifications: Notification[];
}

/** Host memory or disk use at or above this share of capacity needs attention. */
export const RESOURCE_WARNING_PERCENT = 90;
const RECENT_RUNS = 15;
const UNREAD_NOTIFICATIONS = 20;

const isRiskyRun = (run: WorkflowRun) => run.risk === "High" || run.risk === "Critical";

/**
 * Everything that wants the user's attention, critical first, then newest first. Informational notifications stay in
 * the inbox; only warnings and critical alerts are surfaced here.
 */
export function collectAttention({
  services,
  incidents,
  metrics,
  stats,
  runs,
  notifications,
}: AttentionInputs): AttentionItem[] {
  const items: AttentionItem[] = [];

  for (const service of services.filter((s) => s.status === "Degraded" || s.status === "Unhealthy")) {
    items.push({
      id: `service:${service.serviceName}`,
      tone: service.status === "Unhealthy" ? "critical" : "warning",
      source: "Vigil",
      title: `${service.serviceName} is ${service.status.toLowerCase()}`,
      detail: service.description ?? service.details ?? undefined,
      to: "/vigil",
      at: service.lastStateChange,
    });
  }

  for (const incident of incidents.filter((i) => i.status === "Open")) {
    items.push({
      id: `incident:${incident.id}`,
      tone: incident.severity === "Critical" ? "critical" : "warning",
      source: "Vigil",
      title: `Open incident on ${incident.target}`,
      detail: incident.description ?? undefined,
      to: "/vigil",
      at: incident.openedAt,
    });
  }

  if (metrics && metrics.memory.memoryLoadPercent >= RESOURCE_WARNING_PERCENT) {
    items.push({
      id: "host:memory",
      tone: "warning",
      source: "System",
      title: `Host memory at ${Math.round(metrics.memory.memoryLoadPercent)}%`,
      detail: `${(metrics.memory.availableMb / 1024).toFixed(1)} GB free of ${(metrics.memory.totalMb / 1024).toFixed(1)} GB`,
      to: "/system",
    });
  }

  for (const disk of metrics?.disks.filter((d) => d.usagePercent >= RESOURCE_WARNING_PERCENT) ?? []) {
    items.push({
      id: `host:disk:${disk.name}`,
      tone: "warning",
      source: "System",
      title: `Disk ${disk.label || disk.name} at ${Math.round(disk.usagePercent)}%`,
      detail: `${disk.availableGb.toFixed(0)} GB free`,
      to: "/system",
    });
  }

  const highRiskCalls = (stats?.highRisk ?? 0) + (stats?.criticalRisk ?? 0);
  if (highRiskCalls > 0) {
    items.push({
      id: "sentinel:high-risk",
      tone: (stats?.criticalRisk ?? 0) > 0 ? "critical" : "warning",
      source: "Sentinel",
      title: `${highRiskCalls} high-risk tool call${highRiskCalls === 1 ? "" : "s"}`,
      detail: `${stats?.denied ?? 0} denied in total`,
      to: "/sentinel",
    });
  }

  for (const run of runs.filter(isRiskyRun)) {
    items.push({
      id: `run:${run.id}`,
      tone: run.risk === "Critical" ? "critical" : "warning",
      source: "Run",
      title: `${describeRun(run).title} was rated ${run.risk.toLowerCase()} risk`,
      detail: run.recommendation || undefined,
      to: `/orchestration/runs/${run.id}`,
      at: run.startedAt,
    });
  }

  for (const notification of notifications.filter((n) => !n.readAt && n.severity !== "Info")) {
    items.push({
      id: `notification:${notification.id}`,
      tone: notification.severity === "Critical" ? "critical" : "warning",
      source: notification.category,
      title: notification.title,
      detail: notification.body ?? undefined,
      to: notification.link ?? "/inbox",
      at: notification.updatedAt,
    });
  }

  return items.sort(
    (a, b) => Number(b.tone === "critical") - Number(a.tone === "critical") || (b.at ?? "").localeCompare(a.at ?? ""),
  );
}

/** The live attention list for the home page, built from queries other pages already share. */
export function useAttention() {
  const { services, isLoading: servicesLoading } = useServiceHealth();
  const { incidents, isLoading: incidentsLoading } = useIncidents();
  const { metrics, isLoading: metricsLoading } = useSystemMetrics();
  const { stats, isLoading: statsLoading } = useSentinelStats();
  const { runs, isLoading: runsLoading } = useWorkflowRuns(RECENT_RUNS);
  const { notifications, isLoading: notificationsLoading } = useNotifications(true, UNREAD_NOTIFICATIONS);

  return {
    items: collectAttention({ services, incidents, metrics, stats, runs, notifications }),
    isLoading:
      servicesLoading || incidentsLoading || metricsLoading || statsLoading || runsLoading || notificationsLoading,
  };
}
