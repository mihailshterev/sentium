import { RefreshCw, Activity, CheckCircle, XCircle, AlertCircle, Clock, Zap, Server } from "lucide-react";
import styles from "./watchdog.module.scss";
import useServiceHealth from "../../hooks/useServiceHealth";
import useSystemMetrics from "../../hooks/useSystemMetrics";
import type { ServiceHealthStatus, ServiceStatus } from "../../types/serviceHealth";
import { formatTimeHms } from "../../utils/formatters";

function StatusIcon({ status }: { status: ServiceStatus }) {
  if (status === "Healthy") {
    return <CheckCircle size={14} className={styles.iconHealthy} />;
  }

  if (status === "Unhealthy") {
    return <XCircle size={14} className={styles.iconUnhealthy} />;
  }

  return <AlertCircle size={14} className={styles.iconUnknown} />;
}

function LatencyBar({ latencyMs }: { latencyMs: number }) {
  const pct = Math.min((latencyMs / 1000) * 100, 100);
  const color = latencyMs < 100 ? "green" : latencyMs < 400 ? "amber" : "red";
  return (
    <div className={styles.latencyBarWrap}>
      <div className={`${styles.latencyBar} ${styles[`latencyBar_${color}`]}`} style={{ width: `${pct}%` }} />
    </div>
  );
}

function ServiceRow({ service }: { service: ServiceHealthStatus }) {
  return (
    <div className={`${styles.serviceRow} ${service.status === "Unhealthy" ? styles.serviceRowUnhealthy : ""}`}>
      <div className={styles.serviceRowStatus}>
        <StatusIcon status={service.status} />
        <span
          className={`${styles.statusLabel} ${
            service.status === "Healthy"
              ? styles.statusLabelHealthy
              : service.status === "Unhealthy"
                ? styles.statusLabelUnhealthy
                : styles.statusLabelUnknown
          }`}
        >
          {service.status}
        </span>
      </div>

      <span className={styles.serviceName}>{service.serviceName}</span>

      <div className={styles.latencyCell}>
        <span className={styles.latencyValue}>{service.latencyMs.toFixed(0)}ms</span>
        <LatencyBar latencyMs={service.latencyMs} />
      </div>

      <div className={styles.checkedCell}>
        <Clock size={11} />
        <span>{service.checkedAt ? formatTimeHms(service.checkedAt) : "—"}</span>
      </div>

      {service.details && <span className={styles.detailsCell}>{service.details}</span>}
    </div>
  );
}

const Watchdog = () => {
  const { services, isLoading: healthLoading, refetch } = useServiceHealth();
  const { metrics, isLoading: metricsLoading, isRefetching } = useSystemMetrics();

  const healthyCount = services.filter((s) => s.status === "Healthy").length;
  const unhealthyCount = services.filter((s) => s.status === "Unhealthy").length;
  const allHealthy = services.length > 0 && unhealthyCount === 0;

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Watchdog</h1>
          <p className={styles.subtitle}>Service health monitoring and system diagnostics</p>
        </div>
        <button className={styles.refreshBtn} onClick={() => refetch()} disabled={healthLoading}>
          <RefreshCw size={14} className={healthLoading ? styles.spinning : undefined} />
          Refresh
        </button>
      </div>

      <div className={styles.summaryRow}>
        <div className={styles.summaryCard}>
          <Activity size={16} className={styles.summaryIcon} />
          <div>
            <span className={styles.summaryValue}>{services.length}</span>
            <span className={styles.summaryLabel}>Monitored</span>
          </div>
        </div>
        <div className={styles.summaryCard}>
          <CheckCircle size={16} className={`${styles.summaryIcon} ${styles.summaryIconGreen}`} />
          <div>
            <span className={`${styles.summaryValue} ${styles.summaryValueGreen}`}>{healthyCount}</span>
            <span className={styles.summaryLabel}>Healthy</span>
          </div>
        </div>
        <div className={styles.summaryCard}>
          <XCircle size={16} className={`${styles.summaryIcon} ${unhealthyCount > 0 ? styles.summaryIconRed : ""}`} />
          <div>
            <span className={`${styles.summaryValue} ${unhealthyCount > 0 ? styles.summaryValueRed : ""}`}>
              {unhealthyCount}
            </span>
            <span className={styles.summaryLabel}>Unhealthy</span>
          </div>
        </div>
        <div className={`${styles.overallBadge} ${allHealthy ? styles.overallBadgeGreen : styles.overallBadgeRed}`}>
          {allHealthy ? <CheckCircle size={13} /> : <XCircle size={13} />}
          <span>{allHealthy ? "All Systems Operational" : "Degraded Services Detected"}</span>
        </div>
      </div>

      <div className={styles.mainGrid}>
        <section className={styles.card}>
          <div className={styles.cardHeader}>
            <span className={styles.cardTitle}>Service Health</span>
            <span className={styles.liveTag}>Live</span>
          </div>

          <div className={styles.tableHead}>
            <span>Status</span>
            <span>Service</span>
            <span>Latency</span>
            <span>Last Checked</span>
          </div>

          <div className={styles.tableBody}>
            {healthLoading ? (
              [0, 1, 2, 3, 4, 5].map((i) => (
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
                <span>No health data yet — monitoring will begin shortly</span>
              </div>
            ) : (
              services.map((s) => <ServiceRow key={s.serviceName} service={s} />)
            )}
          </div>
        </section>

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
                <span className={styles.metricLabel} style={{ gridColumn: "2 / span 2", fontStyle: "italic" }}>
                  Memory
                </span>
              </div>
              <div className={styles.metricRow}>
                <span className={styles.metricLabelIndent}>Total</span>
                <span className={styles.metricValue}>{metrics.memory.totalMb.toFixed(0)} MB</span>
              </div>
              <div className={styles.metricRow}>
                <span className={styles.metricLabelIndent}>Used</span>
                <span className={styles.metricValue}>{metrics.memory.usedMb.toFixed(0)} MB</span>
              </div>
              <div className={styles.metricRow}>
                <span className={styles.metricLabelIndent}>Load</span>
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
      </div>
    </div>
  );
};

export default Watchdog;
