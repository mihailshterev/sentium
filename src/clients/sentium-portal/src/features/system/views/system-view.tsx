import { useState } from "react";
import { Activity, AlertCircle, Clock, Cpu, HardDrive, MemoryStick, Monitor, RefreshCw, Server } from "lucide-react";
import styles from "../system.module.css";
import { useSystemMetrics } from "@/features/vigil/hooks/useSystemMetrics";
import { formatMb, formatUptime, usageTone } from "../system-utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";
import { DiskCard } from "../components/disk-card";
import { GcBadges } from "../components/gc-badges";
import { ProgressGroup } from "../components/progress-group";

export function SystemView() {
  const { metrics, isLoading, isRefetching, error, refetch } = useSystemMetrics();
  const [isManualRefetching, setIsManualRefetching] = useState(false);

  const handleManualRefresh = async () => {
    setIsManualRefetching(true);
    await refetch();
    setIsManualRefetching(false);
  };

  if (error && !metrics) {
    return (
      <Page title="System" subtitle="Host resource usage and runtime diagnostics">
        <Card>
          <EmptyState
            icon={<AlertCircle />}
            title={`Unable to load system metrics: ${error instanceof Error ? error.message : "Unknown error"}`}
            action={
              <Button variant="secondary" onClick={handleManualRefresh}>
                Retry
              </Button>
            }
          />
        </Card>
      </Page>
    );
  }

  const spinning = isManualRefetching || isRefetching;

  return (
    <Page
      title="System"
      subtitle="Host resource usage and runtime diagnostics"
      actions={
        <>
          <Badge tone="success" dot>
            Live
          </Badge>
          <Button
            variant="secondary"
            icon={<RefreshCw size={15} className={spinning ? "animate-spin" : undefined} />}
            onClick={handleManualRefresh}
          >
            Refresh
          </Button>
        </>
      }
    >
      {(isLoading || metrics) && (
        <StatGrid minCellWidth={190} aria-busy={isLoading || undefined}>
          <StatCard
            icon={<Cpu />}
            value={metrics ? `${metrics.cpu.processCpuPercent}%` : <Skeleton width={72} height={22} />}
            label="CPU (Process)"
          />
          <StatCard
            icon={<MemoryStick />}
            value={metrics ? `${metrics.memory.memoryLoadPercent.toFixed(1)}%` : <Skeleton width={72} height={22} />}
            label="Memory Usage"
          />
          <StatCard
            icon={<Monitor />}
            value={metrics ? metrics.cpu.processorCount : <Skeleton width={40} height={22} />}
            label="Logical Cores"
          />
          <StatCard
            icon={<Clock />}
            value={metrics ? formatUptime(metrics.host.uptime) : <Skeleton width={96} height={22} />}
            label="System Uptime"
          />
        </StatGrid>
      )}

      {metrics && (
        <div className={styles.grid}>
          <Card padding="md" as="section">
            <CardHeader
              icon={<MemoryStick />}
              title="Memory"
              actions={
                <span className={styles.headerMeta}>
                  {formatMb(metrics.memory.usedMb)} / {formatMb(metrics.memory.totalMb)}
                </span>
              }
            />
            <div className={styles.sectionBody}>
              <ProgressGroup
                name="System Memory"
                value={`${metrics.memory.memoryLoadPercent.toFixed(1)}%`}
                percent={metrics.memory.memoryLoadPercent}
                tone={usageTone(metrics.memory.memoryLoadPercent)}
              />
              <ProgressGroup
                name="GC Heap"
                value={formatMb(metrics.memory.gcHeapSizeMb)}
                percent={metrics.memory.totalMb > 0 ? (metrics.memory.gcHeapSizeMb / metrics.memory.totalMb) * 100 : 0}
                tone="neutral"
              />
              <GcBadges
                gen0={metrics.memory.gcGen0Collections}
                gen1={metrics.memory.gcGen1Collections}
                gen2={metrics.memory.gcGen2Collections}
              />
            </div>
          </Card>

          <Card padding="md" as="section">
            <CardHeader
              icon={<Activity />}
              title="Process"
              actions={<span className={styles.headerMeta}>PID {metrics.process.id}</span>}
            />
            <div className={styles.sectionBody}>
              <ProgressGroup
                name="CPU Time"
                value={`${metrics.cpu.processCpuPercent}%`}
                percent={metrics.cpu.processCpuPercent}
                tone={usageTone(metrics.cpu.processCpuPercent)}
              />
              <ProgressGroup
                name="Working Set"
                value={formatMb(metrics.process.workingSetMb)}
                percent={metrics.memory.totalMb > 0 ? (metrics.process.workingSetMb / metrics.memory.totalMb) * 100 : 0}
                tone="neutral"
              />
              <div className={styles.counters}>
                <div className={styles.counter}>
                  <span className={styles.counterValue}>{metrics.process.threadCount}</span>
                  <span className={styles.counterLabel}>Threads</span>
                </div>
                <div className={styles.counter}>
                  <span className={styles.counterValue}>{metrics.process.handleCount}</span>
                  <span className={styles.counterLabel}>Handles</span>
                </div>
              </div>
            </div>
          </Card>

          <Card padding="md" as="section" className={styles.full}>
            <CardHeader
              icon={<HardDrive />}
              title="Disk Volumes"
              actions={
                <span className={styles.headerMeta}>
                  {metrics.disks.length} volume{metrics.disks.length !== 1 ? "s" : ""}
                </span>
              }
            />
            <div className={styles.diskGrid}>
              {metrics.disks.map((disk) => (
                <DiskCard key={disk.name} disk={disk} />
              ))}
            </div>
          </Card>

          <Card padding="md" as="section" className={styles.full}>
            <CardHeader icon={<Server />} title="Host Information" />
            <dl className={styles.info}>
              {[
                ["Machine", metrics.host.machineName],
                ["Architecture", metrics.host.osArchitecture],
                ["Operating System", metrics.host.osDescription],
                ["Runtime", metrics.host.runtimeVersion],
                ["Processors", `${metrics.cpu.processorCount} cores (${metrics.cpu.architecture})`],
                ["Process Name", metrics.process.name],
              ].map(([label, value]) => (
                <div key={label} className={styles.infoItem}>
                  <dt>{label}</dt>
                  <dd>{value}</dd>
                </div>
              ))}
            </dl>
          </Card>
        </div>
      )}
    </Page>
  );
}
