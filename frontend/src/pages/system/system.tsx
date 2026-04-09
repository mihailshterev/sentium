import { useState, useEffect, useCallback } from "react";
import {
  Cpu,
  HardDrive,
  MemoryStick,
  Monitor,
  RefreshCw,
  Server,
  Activity,
  Clock,
  AlertCircle,
  Database,
} from "lucide-react";
import styles from "./system.module.scss";
import { API_BASE } from "../../utils/constants";
import type { SystemMetrics } from "../../types/system";

const REFRESH_INTERVAL = 5000;

function formatUptime(uptime: string): string {
  // .NET TimeSpan comes as "d.hh:mm:ss.fffffff" or "hh:mm:ss.fffffff"
  const parts = uptime.split(":");
  if (parts.length < 3) {
    return uptime;
  }

  let days = 0;
  let hours = parseInt(parts[0], 10);
  const minutes = parseInt(parts[1], 10);

  if (parts[0].includes(".")) {
    const dp = parts[0].split(".");
    days = parseInt(dp[0], 10);
    hours = parseInt(dp[1], 10);
  }

  const segments: string[] = [];
  if (days > 0) {
    segments.push(`${days}d`);
  }
  if (hours > 0) {
    segments.push(`${hours}h`);
  }
  segments.push(`${minutes}m`);
  return segments.join(" ");
}

function formatMb(mb: number): string {
  if (mb >= 1024) {
    return `${(mb / 1024).toFixed(1)} GB`;
  }
  return `${mb.toFixed(0)} MB`;
}

function formatGb(gb: number): string {
  if (gb >= 1024) {
    return `${(gb / 1024).toFixed(1)} TB`;
  }
  return `${gb.toFixed(1)} GB`;
}

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
  const [metrics, setMetrics] = useState<SystemMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const fetchMetrics = useCallback(async (isManual = false) => {
    if (isManual) {
      setRefreshing(true);
    }
    try {
      const res = await fetch(`${API_BASE}/watchdog/system/metrics`);
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      setMetrics(await res.json());
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to fetch metrics");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    fetchMetrics();
    const id = setInterval(() => fetchMetrics(), REFRESH_INTERVAL);
    return () => clearInterval(id);
  }, [fetchMetrics]);

  if (error && !metrics) {
    return (
      <div className={styles.root}>
        <div className={styles.errorState}>
          <AlertCircle size={32} className={styles.errorIcon} />
          <span className={styles.errorMessage}>Unable to load system metrics: {error}</span>
          <button className={styles.retryBtn} onClick={() => fetchMetrics(true)}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h1 className={styles.pageTitle}>System</h1>
          <p className={styles.pageSubtitle}>Host resource usage and runtime diagnostics</p>
        </div>
        <div className={styles.headerRight}>
          <div className={styles.liveBadge}>
            <span className={styles.liveDot} />
            Live
          </div>
          <button
            className={`${styles.refreshBtn} ${refreshing ? styles.spinning : ""}`}
            onClick={() => fetchMetrics(true)}
          >
            <RefreshCw size={12} />
            Refresh
          </button>
        </div>
      </div>

      <div className={styles.statsRow}>
        {loading ? (
          <>
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
          </>
        ) : metrics ? (
          <>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconBlue}`}>
                <Cpu size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{metrics.cpu.processCpuPercent}%</span>
                <span className={styles.statLabel}>CPU (Process)</span>
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconPurple}`}>
                <MemoryStick size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{metrics.memory.memoryLoadPercent.toFixed(1)}%</span>
                <span className={styles.statLabel}>Memory Usage</span>
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconGreen}`}>
                <Monitor size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{metrics.cpu.processorCount}</span>
                <span className={styles.statLabel}>Logical Cores</span>
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconAmber}`}>
                <Clock size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{formatUptime(metrics.host.uptime)}</span>
                <span className={styles.statLabel}>System Uptime</span>
              </div>
            </div>
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
              <div className={styles.progressGroup}>
                <div className={styles.progressLabel}>
                  <span className={styles.progressName}>System Memory</span>
                  <span className={styles.progressValue}>{metrics.memory.memoryLoadPercent.toFixed(1)}%</span>
                </div>
                <div className={styles.progressTrack}>
                  <div
                    className={`${styles.progressFill} ${getUsageColor(metrics.memory.memoryLoadPercent)}`}
                    style={{ width: `${Math.min(metrics.memory.memoryLoadPercent, 100)}%` }}
                  />
                </div>
              </div>
              <div className={styles.progressGroup}>
                <div className={styles.progressLabel}>
                  <span className={styles.progressName}>GC Heap</span>
                  <span className={styles.progressValue}>{formatMb(metrics.memory.gcHeapSizeMb)}</span>
                </div>
                <div className={styles.progressTrack}>
                  <div
                    className={`${styles.progressFill} ${styles.fillPurple}`}
                    style={{
                      width: `${Math.min(
                        metrics.memory.totalMb > 0 ? (metrics.memory.gcHeapSizeMb / metrics.memory.totalMb) * 100 : 0,
                        100,
                      )}%`,
                    }}
                  />
                </div>
              </div>
              <div className={styles.gcRow}>
                <div className={styles.gcBadge}>
                  <span className={styles.gcBadgeValue}>{metrics.memory.gcGen0Collections}</span>
                  <span className={styles.gcBadgeLabel}>Gen 0</span>
                </div>
                <div className={styles.gcBadge}>
                  <span className={styles.gcBadgeValue}>{metrics.memory.gcGen1Collections}</span>
                  <span className={styles.gcBadgeLabel}>Gen 1</span>
                </div>
                <div className={styles.gcBadge}>
                  <span className={styles.gcBadgeValue}>{metrics.memory.gcGen2Collections}</span>
                  <span className={styles.gcBadgeLabel}>Gen 2</span>
                </div>
              </div>
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
              <div className={styles.progressGroup}>
                <div className={styles.progressLabel}>
                  <span className={styles.progressName}>CPU Time</span>
                  <span className={styles.progressValue}>{metrics.cpu.processCpuPercent}%</span>
                </div>
                <div className={styles.progressTrack}>
                  <div
                    className={`${styles.progressFill} ${getUsageColor(metrics.cpu.processCpuPercent)}`}
                    style={{ width: `${Math.min(metrics.cpu.processCpuPercent, 100)}%` }}
                  />
                </div>
              </div>
              <div className={styles.progressGroup}>
                <div className={styles.progressLabel}>
                  <span className={styles.progressName}>Working Set</span>
                  <span className={styles.progressValue}>{formatMb(metrics.process.workingSetMb)}</span>
                </div>
                <div className={styles.progressTrack}>
                  <div
                    className={`${styles.progressFill} ${styles.fillBlue}`}
                    style={{
                      width: `${Math.min(
                        metrics.memory.totalMb > 0 ? (metrics.process.workingSetMb / metrics.memory.totalMb) * 100 : 0,
                        100,
                      )}%`,
                    }}
                  />
                </div>
              </div>
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
                  <div key={disk.name} className={styles.diskCard}>
                    <div className={styles.diskHeader}>
                      <span className={styles.diskName}>
                        <Database size={13} />
                        {disk.name}
                      </span>
                      <span className={styles.diskLabel}>{disk.label || disk.fileSystem}</span>
                    </div>
                    <div className={styles.progressGroup}>
                      <div className={styles.progressLabel}>
                        <span className={styles.progressName}>{formatGb(disk.usedGb)} used</span>
                        <span className={styles.progressValue}>{disk.usagePercent.toFixed(1)}%</span>
                      </div>
                      <div className={styles.progressTrack}>
                        <div
                          className={`${styles.progressFill} ${getUsageColor(disk.usagePercent)}`}
                          style={{ width: `${Math.min(disk.usagePercent, 100)}%` }}
                        />
                      </div>
                    </div>
                    <span className={styles.diskMeta}>
                      {formatGb(disk.availableGb)} free of {formatGb(disk.totalGb)}
                    </span>
                  </div>
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
