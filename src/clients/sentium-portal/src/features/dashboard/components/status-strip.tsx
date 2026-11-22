import styles from "../dashboard.module.css";
import type { AttentionItem } from "../hooks/useAttention";
import type { SystemMetrics } from "@/features/system/types/system";
import { formatUptime } from "@/features/system/system-utils";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";

interface StatusStripProps {
  items: AttentionItem[];
  services: ServiceHealthStatus[];
  metrics?: SystemMetrics | null;
  loading: boolean;
}

type StripTone = "neutral" | "ok" | "warn" | "critical";

function summarize(items: AttentionItem[], loading: boolean): { tone: StripTone; text: string } {
  if (loading) {
    return { tone: "neutral", text: "Checking status…" };
  }

  const critical = items.filter((i) => i.tone === "critical").length;
  if (critical > 0) {
    return { tone: "critical", text: `${critical} critical issue${critical === 1 ? "" : "s"} need attention` };
  }

  if (items.length > 0) {
    return { tone: "warn", text: `Operational with ${items.length} warning${items.length === 1 ? "" : "s"}` };
  }

  return { tone: "ok", text: "All systems operational" };
}

/** One line answering "is everything OK?", agreeing with the attention list below it. */
export function StatusStrip({ items, services, metrics, loading }: StatusStripProps) {
  const { tone, text } = summarize(items, loading);
  const healthy = services.filter((s) => s.status === "Healthy").length;

  return (
    <div className={styles.statusStrip} data-tone={tone} role="status">
      <span className={styles.statusDot} aria-hidden="true" />
      <strong className={styles.statusText}>{text}</strong>
      <span className={styles.statusFacts}>
        <span>
          {healthy}/{services.length} services healthy
        </span>
        {metrics && (
          <>
            <span>memory {Math.round(metrics.memory.memoryLoadPercent)}%</span>
            <span>CPU {metrics.cpu.processCpuPercent.toFixed(1)}%</span>
            <span>up {formatUptime(metrics.host.uptime)}</span>
          </>
        )}
      </span>
    </div>
  );
}
