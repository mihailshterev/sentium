import { Tooltip } from "@/shared/ui/tooltip";
import type { RealtimeStatus } from "./realtime-client";
import { useRealtimeStatus } from "./hooks";
import styles from "./realtime-indicator.module.css";

const LABELS: Record<RealtimeStatus, string> = {
  live: "Live updates on",
  connecting: "Connecting live updates…",
  reconnecting: "Reconnecting - showing the last data received",
  offline: "Live updates off",
};

interface RealtimeIndicatorProps {
  className?: string;
  tooltipSide?: "top" | "right" | "bottom";
}

/** A quiet dot while live; it only draws attention when the connection is degraded. */
export function RealtimeIndicator({ className, tooltipSide = "right" }: RealtimeIndicatorProps) {
  const status = useRealtimeStatus();
  const label = LABELS[status];

  return (
    <Tooltip content={label} side={tooltipSide}>
      <span
        className={className ? `${styles.indicator} ${className}` : styles.indicator}
        data-status={status}
        role="status"
        aria-label={label}
        data-testid="realtime-status"
      >
        <span className={styles.dot} aria-hidden="true" />
      </span>
    </Tooltip>
  );
}
