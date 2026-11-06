import { Activity, Database, RefreshCw, Server, ShieldAlert } from "lucide-react";
import styles from "../vigil.module.css";
import { useServiceHealth } from "@/features/vigil/hooks/useServiceHealth";
import { useSystemMetrics } from "@/features/vigil/hooks/useSystemMetrics";
import { useSystemOverview } from "@/features/vigil/hooks/useSystemOverview";
import { useIncidents } from "@/features/vigil/hooks/useIncidents";
import { useVigilStream } from "@/features/vigil/hooks/useVigilStream";
import { useRole } from "@/shared/auth/hooks/useRole";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { ServiceRow } from "../components/service-row";
import { SummaryStats } from "../components/summary-stats";
import { IncidentTimeline } from "../components/incident-timeline";
import { VigilConfigPanel } from "../components/vigil-config-panel";
import type { ServiceHealthStatus, ServiceStatus } from "@/features/vigil/types/serviceHealth";

const countBy = (services: ServiceHealthStatus[], status: ServiceStatus) =>
  services.filter((s) => s.status === status).length;

const HealthTable = ({
  title,
  icon,
  services,
  loading,
  isLive,
}: {
  title: string;
  icon: React.ReactNode;
  services: ServiceHealthStatus[];
  loading: boolean;
  isLive: boolean;
}) => (
  <Card padding="none" as="section">
    <CardHeader
      icon={icon}
      title={title}
      actions={
        isLive ? (
          <Badge tone="success" dot>
            Live
          </Badge>
        ) : (
          <Badge tone="neutral">Polling</Badge>
        )
      }
    />

    <div className={styles.tableHead} aria-hidden="true">
      <span>Status</span>
      <span>Target</span>
      <span>Latency</span>
      <span>Last checked</span>
    </div>

    <div className={styles.tableBody}>
      {loading ? (
        [0, 1, 2].map((i) => (
          <div key={i} className={styles.skeletonRow}>
            <Skeleton width={70} />
            <Skeleton width={120} />
            <Skeleton width={80} />
            <Skeleton width={60} />
          </div>
        ))
      ) : services.length === 0 ? (
        <div className={styles.emptyRow}>
          <Activity size={18} aria-hidden="true" />
          <span>No data yet - monitoring will begin shortly</span>
        </div>
      ) : (
        services.map((s) => <ServiceRow key={s.serviceName} service={s} />)
      )}
    </div>
  </Card>
);

export function VigilView() {
  const { services, isLoading: healthLoading, refetch } = useServiceHealth();
  const { metrics, isLoading: metricsLoading, isRefetching } = useSystemMetrics();
  const { overview } = useSystemOverview();
  const { incidents } = useIncidents();
  const { isLive } = useVigilStream();
  const { isSovereign } = useRole();

  const serviceTargets = services.filter((s) => s.kind === "Service");
  const infraTargets = services.filter((s) => s.kind === "Infrastructure");

  const summary = overview ?? {
    total: services.length,
    healthy: countBy(services, "Healthy"),
    degraded: countBy(services, "Degraded"),
    unhealthy: countBy(services, "Unhealthy"),
    unknown: countBy(services, "Unknown"),
    openIncidents: incidents.filter((i) => i.status === "Open").length,
    overallStatus: (countBy(services, "Unhealthy") > 0
      ? "Unhealthy"
      : countBy(services, "Degraded") > 0
        ? "Degraded"
        : services.length > 0
          ? "Healthy"
          : "Unknown") as ServiceStatus,
    generatedAt: new Date().toISOString(),
  };

  const hostMetrics = metrics
    ? [
        { label: "Hostname", value: metrics.host.machineName },
        { label: "CPU Cores", value: metrics.host.processorCount },
        { label: "CPU Usage", value: `${metrics.cpu.processCpuPercent.toFixed(1)}%` },
        { label: "Memory Load", value: `${metrics.memory.memoryLoadPercent.toFixed(1)}%` },
        { label: "Uptime", value: metrics.host.uptime.split(".")[0] },
        { label: "Runtime", value: metrics.host.runtimeVersion },
      ]
    : [];

  return (
    <Page
      title="Vigil"
      subtitle="Service & infrastructure health, incidents, and diagnostics"
      width="wide"
      actions={
        <Button
          variant="secondary"
          icon={<RefreshCw size={15} className={healthLoading ? "animate-spin" : undefined} />}
          onClick={() => refetch()}
          disabled={healthLoading}
        >
          Refresh
        </Button>
      }
    >
      <SummaryStats
        total={summary.total}
        healthy={summary.healthy}
        degraded={summary.degraded}
        unhealthy={summary.unhealthy}
        openIncidents={summary.openIncidents}
        overallStatus={summary.overallStatus}
      />

      <div className={styles.mainGrid}>
        <div className={styles.column}>
          <HealthTable
            title="Services"
            icon={<Server />}
            services={serviceTargets}
            loading={healthLoading}
            isLive={isLive}
          />
          <HealthTable
            title="Infrastructure"
            icon={<Database />}
            services={infraTargets}
            loading={healthLoading}
            isLive={isLive}
          />
          <Card padding="none" as="section">
            <CardHeader icon={<ShieldAlert />} title="Incidents" />
            <IncidentTimeline incidents={incidents} />
          </Card>
        </div>

        <div className={styles.column}>
          <Card padding="md" as="section">
            <CardHeader
              title="Host Metrics"
              actions={isRefetching && <RefreshCw size={14} className="animate-spin" aria-label="Refreshing" />}
            />
            {metricsLoading || !metrics ? (
              <div className={styles.metricsLoading} aria-busy="true">
                {[0, 1, 2, 3].map((i) => (
                  <div key={i} className={styles.metricRow}>
                    <Skeleton width={90} />
                    <Skeleton width={60} />
                  </div>
                ))}
              </div>
            ) : (
              <dl className={styles.metrics}>
                {hostMetrics.map((m) => (
                  <div key={m.label} className={styles.metricRow}>
                    <dt>{m.label}</dt>
                    <dd>{m.value}</dd>
                  </div>
                ))}
              </dl>
            )}
          </Card>

          {isSovereign && <VigilConfigPanel />}
        </div>
      </div>
    </Page>
  );
}
