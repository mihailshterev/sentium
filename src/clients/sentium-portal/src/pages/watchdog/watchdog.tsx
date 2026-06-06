import { RefreshCw, Activity, Zap, Server, Database, ShieldAlert } from "lucide-react";
import styles from "./watchdog.module.scss";
import useServiceHealth from "../../hooks/useServiceHealth";
import useSystemMetrics from "../../hooks/useSystemMetrics";
import useSystemOverview from "../../hooks/useSystemOverview";
import useIncidents from "../../hooks/useIncidents";
import useWatchdogStream from "../../hooks/useWatchdogStream";
import { useRole } from "../../hooks/useRole";
import PageHeader from "../../components/ui/page-header";
import ServiceRow from "./components/service-row";
import SummaryStats from "./components/summary-stats";
import IncidentTimeline from "./components/incident-timeline";
import WatchdogConfigPanel from "./components/watchdog-config-panel";
import type { ServiceHealthStatus, ServiceStatus } from "../../types/serviceHealth";

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
  <section className={styles.card}>
    <div className={styles.cardHeader}>
      <span className={styles.cardTitle}>
        {icon} {title}
      </span>
      {isLive ? <span className={styles.liveTag}>Live</span> : <span className={styles.offlineTag}>Polling</span>}
    </div>

    <div className={styles.tableHead}>
      <span>Status</span>
      <span>Target</span>
      <span>Latency</span>
      <span>Last Checked</span>
    </div>

    <div className={styles.tableBody}>
      {loading ? (
        [0, 1, 2].map((i) => (
          <div key={i} className={styles.skeletonRow}>
            <span className={styles.skeletonBlock} style={{ width: 60 }} />
            <span className={styles.skeletonBlock} style={{ width: 100 }} />
            <span className={styles.skeletonBlock} style={{ width: 80 }} />
            <span className={styles.skeletonBlock} style={{ width: 70 }} />
          </div>
        ))
      ) : services.length === 0 ? (
        <div className={styles.emptyState}>
          <Activity size={20} />
          <span>No data yet - monitoring will begin shortly</span>
        </div>
      ) : (
        services.map((s) => <ServiceRow key={s.serviceName} service={s} />)
      )}
    </div>
  </section>
);

const Watchdog = () => {
  const { services, isLoading: healthLoading, refetch } = useServiceHealth();
  const { metrics, isLoading: metricsLoading, isRefetching } = useSystemMetrics();
  const { overview } = useSystemOverview();
  const { incidents } = useIncidents();
  const { isLive } = useWatchdogStream();
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

  return (
    <div className={styles.root}>
      <PageHeader
        title="Watchdog"
        subtitle="Service & infrastructure health, incidents, and diagnostics"
        right={
          <button className={styles.refreshBtn} onClick={() => refetch()} disabled={healthLoading}>
            <RefreshCw size={14} className={healthLoading ? styles.spinning : undefined} />
            Refresh
          </button>
        }
      />

      <div className={styles.content}>
        <SummaryStats
          total={summary.total}
          healthy={summary.healthy}
          degraded={summary.degraded}
          unhealthy={summary.unhealthy}
          openIncidents={summary.openIncidents}
          overallStatus={summary.overallStatus}
        />

        <div className={styles.mainGrid}>
          <div className={styles.leftColumn}>
            <HealthTable
              title="Services"
              icon={<Server size={13} className={styles.headerIcon} />}
              services={serviceTargets}
              loading={healthLoading}
              isLive={isLive}
            />

            <HealthTable
              title="Infrastructure"
              icon={<Database size={13} className={styles.headerIcon} />}
              services={infraTargets}
              loading={healthLoading}
              isLive={isLive}
            />

            <section className={styles.card}>
              <div className={styles.cardHeader}>
                <span className={styles.cardTitle}>
                  <ShieldAlert size={13} className={styles.headerIcon} /> Incidents
                </span>
              </div>
              <IncidentTimeline incidents={incidents} />
            </section>
          </div>

          <div className={styles.rightColumn}>
            <section className={styles.card}>
              <div className={styles.cardHeader}>
                <span className={styles.cardTitle}>Host Metrics</span>
                {isRefetching && <RefreshCw size={12} className={styles.spinning} />}
              </div>

              {metricsLoading || !metrics ? (
                <div className={styles.metricsLoading}>
                  {[0, 1, 2, 3].map((i) => (
                    <div key={i} className={styles.metricSkeletonRow}>
                      <span className={styles.skeletonBlock} style={{ width: 90 }} />
                      <span className={styles.skeletonBlock} style={{ width: 60 }} />
                    </div>
                  ))}
                </div>
              ) : (
                <div className={styles.metricsList}>
                  <div className={styles.metricRow}>
                    <Server size={13} className={styles.metricIcon} />
                    <span className={styles.metricLabel}>Hostname</span>
                    <span className={styles.metricValue}>{metrics.host.machineName}</span>
                  </div>
                  <div className={styles.metricRow}>
                    <Zap size={13} className={styles.metricIcon} />
                    <span className={styles.metricLabel}>CPU Cores</span>
                    <span className={styles.metricValue}>{metrics.host.processorCount}</span>
                  </div>
                  <div className={styles.metricRow}>
                    <Activity size={13} className={styles.metricIcon} />
                    <span className={styles.metricLabel}>CPU Usage</span>
                    <span className={styles.metricValue}>{metrics.cpu.processCpuPercent.toFixed(1)}%</span>
                  </div>
                  <div className={styles.metricRow}>
                    <span className={styles.metricLabelIndent}>Memory Load</span>
                    <span className={styles.metricValue}>{metrics.memory.memoryLoadPercent.toFixed(1)}%</span>
                  </div>
                  <div className={styles.metricRow}>
                    <Activity size={13} className={styles.metricIcon} />
                    <span className={styles.metricLabel}>Uptime</span>
                    <span className={styles.metricValue}>{metrics.host.uptime.split(".")[0]}</span>
                  </div>
                  <div className={styles.metricRow}>
                    <span className={styles.metricLabel}>Runtime</span>
                    <span className={styles.metricValue} style={{ fontSize: "0.68rem" }}>
                      {metrics.host.runtimeVersion}
                    </span>
                  </div>
                </div>
              )}
            </section>

            {isSovereign && <WatchdogConfigPanel />}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Watchdog;
