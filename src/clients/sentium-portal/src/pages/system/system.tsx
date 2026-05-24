import { useState } from "react";
import { Cpu, HardDrive, MemoryStick, Monitor, RefreshCw, Server, Activity, Clock, AlertCircle } from "lucide-react";
import styles from "./system.module.scss";
import useSystemMetrics from "../../hooks/useSystemMetrics";
import { formatUptime, formatMb } from "./system-utils";
import PageHeader from "../../components/ui/page-header";
import StatCard from "../../components/ui/stat-card";
import ProgressGroup from "./components/progress-group";
import GcBadges from "./components/gc-badges";
import DiskCard from "./components/disk-card";

function getUsageColor(percent: number): string {
  if (percent < 50) {
    return styles.fillGreen;
  }
  if (percent < 75) {
    return styles.fillAmber;
  }
  return styles.fillRed;
}

const System = () => {
  const { metrics, isLoading, isRefetching, error, refetch } = useSystemMetrics();
  const [isManualRefetching, setIsManualRefetching] = useState(false);

  const handleManualRefresh = async () => {
    setIsManualRefetching(true);
    await refetch();
    setIsManualRefetching(false);
  };

  if (error && !metrics) {
    return (
      <div className={styles.root}>
        <div className={styles.errorState}>
          <AlertCircle size={32} className={styles.errorIcon} />
          <span className={styles.errorMessage}>
            Unable to load system metrics: {error instanceof Error ? error.message : "Unknown error"}
          </span>
          <button className={styles.retryBtn} onClick={handleManualRefresh}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.root}>
      <PageHeader
        title="System"
        subtitle="Host resource usage and runtime diagnostics"
        right={
          <div className={styles.headerRight}>
            <div className={styles.liveBadge}>
              <span className={styles.liveDot} />
              Live
            </div>
            <button
              className={`${styles.refreshBtn} ${isManualRefetching || isRefetching ? styles.spinning : ""}`}
              onClick={handleManualRefresh}
            >
              <RefreshCw size={12} />
              Refresh
            </button>
          </div>
        }
      />

      <div className={styles.statsRow}>
        {isLoading ? (
          <>
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
          </>
        ) : metrics ? (
          <>
            <StatCard
              icon={<Cpu size={18} />}
              value={`${metrics.cpu.processCpuPercent}%`}
              label="CPU (Process)"
              iconColor="blue"
            />
            <StatCard
              icon={<MemoryStick size={18} />}
              value={`${metrics.memory.memoryLoadPercent.toFixed(1)}%`}
              label="Memory Usage"
              iconColor="purple"
            />
            <StatCard
              icon={<Monitor size={18} />}
              value={metrics.cpu.processorCount}
              label="Logical Cores"
              iconColor="green"
            />
            <StatCard
              icon={<Clock size={18} />}
              value={formatUptime(metrics.host.uptime)}
              label="System Uptime"
              iconColor="amber"
            />
          </>
        ) : null}
      </div>

      {metrics && (
        <div className={styles.mainGrid}>
          <div className={styles.section}>
            <div className={styles.sectionHeader}>
              <span className={styles.sectionTitle}>
                <MemoryStick size={14} className={styles.sectionTitleIcon} />
                Memory
              </span>
              <span style={{ fontSize: "0.68rem", color: "var(--text-muted)" }}>
                {formatMb(metrics.memory.usedMb)} / {formatMb(metrics.memory.totalMb)}
              </span>
            </div>
            <div className={styles.sectionBody}>
              <ProgressGroup
                name="System Memory"
                value={`${metrics.memory.memoryLoadPercent.toFixed(1)}%`}
                percent={metrics.memory.memoryLoadPercent}
                fillClass={getUsageColor(metrics.memory.memoryLoadPercent)}
              />
              <ProgressGroup
                name="GC Heap"
                value={formatMb(metrics.memory.gcHeapSizeMb)}
                percent={metrics.memory.totalMb > 0 ? (metrics.memory.gcHeapSizeMb / metrics.memory.totalMb) * 100 : 0}
                fillClass={styles.fillPurple}
              />
              <GcBadges
                gen0={metrics.memory.gcGen0Collections}
                gen1={metrics.memory.gcGen1Collections}
                gen2={metrics.memory.gcGen2Collections}
              />
            </div>
          </div>

          <div className={styles.section}>
            <div className={styles.sectionHeader}>
              <span className={styles.sectionTitle}>
                <Activity size={14} className={styles.sectionTitleIcon} />
                Process
              </span>
              <span style={{ fontSize: "0.68rem", color: "var(--text-muted)" }}>PID {metrics.process.id}</span>
            </div>
            <div className={styles.sectionBody}>
              <ProgressGroup
                name="CPU Time"
                value={`${metrics.cpu.processCpuPercent}%`}
                percent={metrics.cpu.processCpuPercent}
                fillClass={getUsageColor(metrics.cpu.processCpuPercent)}
              />
              <ProgressGroup
                name="Working Set"
                value={formatMb(metrics.process.workingSetMb)}
                percent={metrics.memory.totalMb > 0 ? (metrics.process.workingSetMb / metrics.memory.totalMb) * 100 : 0}
                fillClass={styles.fillBlue}
              />
              <div className={styles.gcRow}>
                <div className={styles.gcBadge}>
                  <span className={styles.gcBadgeValue}>{metrics.process.threadCount}</span>
                  <span className={styles.gcBadgeLabel}>Threads</span>
                </div>
                <div className={styles.gcBadge}>
                  <span className={styles.gcBadgeValue}>{metrics.process.handleCount}</span>
                  <span className={styles.gcBadgeLabel}>Handles</span>
                </div>
              </div>
            </div>
          </div>

          <div className={`${styles.section} ${styles.sectionFull}`}>
            <div className={styles.sectionHeader}>
              <span className={styles.sectionTitle}>
                <HardDrive size={14} className={styles.sectionTitleIcon} />
                Disk Volumes
              </span>
              <span style={{ fontSize: "0.68rem", color: "var(--text-muted)" }}>
                {metrics.disks.length} volume{metrics.disks.length !== 1 ? "s" : ""}
              </span>
            </div>
            <div className={styles.sectionBody}>
              <div className={styles.diskGrid}>
                {metrics.disks.map((disk) => (
                  <DiskCard key={disk.name} disk={disk} />
                ))}
              </div>
            </div>
          </div>

          <div className={`${styles.section} ${styles.sectionFull}`}>
            <div className={styles.sectionHeader}>
              <span className={styles.sectionTitle}>
                <Server size={14} className={styles.sectionTitleIcon} />
                Host Information
              </span>
            </div>
            <div className={styles.infoGrid}>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>Machine</span>
                <span className={styles.infoValue}>{metrics.host.machineName}</span>
              </div>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>Architecture</span>
                <span className={styles.infoValue}>{metrics.host.osArchitecture}</span>
              </div>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>Operating System</span>
                <span className={styles.infoValue}>{metrics.host.osDescription}</span>
              </div>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>Runtime</span>
                <span className={styles.infoValue}>{metrics.host.runtimeVersion}</span>
              </div>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>Processors</span>
                <span className={styles.infoValue}>
                  {metrics.cpu.processorCount} cores ({metrics.cpu.architecture})
                </span>
              </div>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>Process Name</span>
                <span className={styles.infoValue}>{metrics.process.name}</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default System;
