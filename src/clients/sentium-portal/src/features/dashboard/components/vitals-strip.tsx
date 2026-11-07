import { Clock, Cpu, MemoryStick, Server } from "lucide-react";
import styles from "../dashboard.module.css";
import { formatUptime } from "../../system/system-utils";
import { Card } from "@/shared/ui/card";
import type { SystemMetrics } from "@/features/system/types/system";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";

interface VitalsStripProps {
  metrics: SystemMetrics | undefined;
  services: ServiceHealthStatus[];
  loading: boolean;
}

function level(pct: number): "ok" | "warn" | "critical" {
  if (pct > 90) {
    return "critical";
  }
  return pct > 70 ? "warn" : "ok";
}

interface VitalProps {
  icon: React.ReactNode;
  label: string;
  value: string;
  /** 0-100; renders a meter when provided. */
  percent?: number;
  tone?: "ok" | "warn" | "critical";
}

function Vital({ icon, label, value, percent, tone = "ok" }: VitalProps) {
  return (
    <div className={styles.vital}>
      <span className={styles.vitalIcon} data-tone={tone} aria-hidden="true">
        {icon}
      </span>
      <div className={styles.vitalText}>
        <span className={styles.vitalValue}>{value}</span>
        <span className={styles.vitalLabel}>{label}</span>
      </div>
      {percent !== undefined && (
        <div
          className={styles.meter}
          role="meter"
          aria-label={label}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={Math.round(percent)}
        >
          <div className={styles.meterFill} data-tone={tone} style={{ width: `${Math.min(percent, 100)}%` }} />
        </div>
      )}
    </div>
  );
}

export function VitalsStrip({ metrics, services, loading }: VitalsStripProps) {
  const cpuPct = metrics?.cpu.processCpuPercent ?? 0;
  const memPct = metrics?.memory.memoryLoadPercent ?? 0;
  const healthyCount = services.filter((s) => s.status === "Healthy").length;
  const totalServices = services.length;

  return (
    <Card className={styles.vitals} padding="none" aria-busy={loading || undefined}>
      <Vital
        icon={<Cpu />}
        label="CPU"
        value={loading ? "—" : `${cpuPct.toFixed(1)}%`}
        percent={loading ? 0 : cpuPct}
        tone={level(cpuPct)}
      />
      <Vital
        icon={<MemoryStick />}
        label="Memory"
        value={loading ? "—" : `${memPct.toFixed(1)}%`}
        percent={loading ? 0 : memPct}
        tone={level(memPct)}
      />
      <Vital icon={<Clock />} label="Uptime" value={metrics ? formatUptime(metrics.host.uptime) : "—"} />
      <Vital
        icon={<Server />}
        label="Services Healthy"
        value={loading || totalServices === 0 ? "—" : `${healthyCount}/${totalServices}`}
        tone={totalServices > 0 && healthyCount === totalServices ? "ok" : "warn"}
      />
    </Card>
  );
}
