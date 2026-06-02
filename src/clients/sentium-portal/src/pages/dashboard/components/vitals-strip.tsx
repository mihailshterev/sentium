import { Cpu, MemoryStick, Clock, Server } from "lucide-react";
import styles from "../dashboard.module.scss";
import { formatUptime } from "../../system/system-utils";
import type { SystemMetrics } from "../../../types/system";
import type { ServiceHealthStatus } from "../../../types/serviceHealth";

interface VitalsStripProps {
  metrics: SystemMetrics | undefined;
  services: ServiceHealthStatus[];
  loading: boolean;
}

function barIconClass(pct: number): string {
  if (pct > 90) {
    return styles.vitalsIconRed;
  }
  if (pct > 70) {
    return styles.vitalsIconAmber;
  }
  return styles.vitalsIconGreen;
}

const VitalsStrip = ({ metrics, services, loading }: VitalsStripProps) => {
  const cpuPct = metrics?.cpu.processCpuPercent ?? 0;
  const memPct = metrics?.memory.memoryLoadPercent ?? 0;
  const uptime = metrics ? formatUptime(metrics.host.uptime) : "—";
  const healthyCount = services.filter((s) => s.status === "Healthy").length;
  const totalServices = services.length;

  return (
    <div className={styles.vitalsStrip}>
      <div className={styles.vitalsSegment}>
        <Cpu size={14} className={loading ? styles.vitalsIconMuted : barIconClass(cpuPct)} />
        <div className={styles.vitalsTextGroup}>
          <span className={styles.vitalsValue}>{loading ? "—" : `${cpuPct.toFixed(1)}%`}</span>
          <span className={styles.vitalsLabel}>CPU</span>
        </div>
        <div className={styles.vitalsBar}>
          <div
            className={styles.vitalsBarFill}
            style={{ width: loading ? "0%" : `${Math.min(cpuPct, 100)}%` }}
            data-warn={cpuPct > 70 && cpuPct <= 90 ? "true" : undefined}
            data-critical={cpuPct > 90 ? "true" : undefined}
          />
        </div>
      </div>

      <div className={styles.vitalsDivider} />

      <div className={styles.vitalsSegment}>
        <MemoryStick size={14} className={loading ? styles.vitalsIconMuted : barIconClass(memPct)} />
        <div className={styles.vitalsTextGroup}>
          <span className={styles.vitalsValue}>{loading ? "—" : `${memPct.toFixed(1)}%`}</span>
          <span className={styles.vitalsLabel}>Memory</span>
        </div>
        <div className={styles.vitalsBar}>
          <div
            className={styles.vitalsBarFill}
            style={{ width: loading ? "0%" : `${Math.min(memPct, 100)}%` }}
            data-warn={memPct > 70 && memPct <= 90 ? "true" : undefined}
            data-critical={memPct > 90 ? "true" : undefined}
          />
        </div>
      </div>

      <div className={styles.vitalsDivider} />

      <div className={styles.vitalsSegment}>
        <Clock size={14} className={styles.vitalsIconMuted} />
        <div className={styles.vitalsTextGroup}>
          <span className={styles.vitalsValue}>{uptime}</span>
          <span className={styles.vitalsLabel}>Uptime</span>
        </div>
      </div>

      <div className={styles.vitalsDivider} />

      <div className={styles.vitalsSegment}>
        <Server
          size={14}
          className={
            totalServices > 0 && healthyCount === totalServices ? styles.vitalsIconGreen : styles.vitalsIconAmber
          }
        />
        <div className={styles.vitalsTextGroup}>
          <span className={styles.vitalsValue}>
            {loading || totalServices === 0 ? "—" : `${healthyCount}/${totalServices}`}
          </span>
          <span className={styles.vitalsLabel}>Services Healthy</span>
        </div>
      </div>
    </div>
  );
};

export default VitalsStrip;
