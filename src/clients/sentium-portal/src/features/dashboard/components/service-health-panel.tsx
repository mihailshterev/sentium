import { Activity } from "lucide-react";
import styles from "../dashboard.module.css";
import { Badge } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Skeleton } from "@/shared/ui/skeleton";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";

interface ServiceHealthPanelProps {
  services: ServiceHealthStatus[];
  loading: boolean;
}

const STATUS_TONE: Record<string, string> = {
  Healthy: "ok",
  Degraded: "warn",
  Unhealthy: "critical",
};

const SkeletonRow = () => (
  <li className={styles.healthRow}>
    <Skeleton width={8} height={8} radius="full" />
    <Skeleton width="45%" />
    <Skeleton width={56} className={styles.healthSkeletonEnd} />
  </li>
);

export function ServiceHealthPanel({ services, loading }: ServiceHealthPanelProps) {
  const healthyCount = services.filter((s) => s.status === "Healthy").length;

  return (
    <Card padding="none" as="section">
      <CardHeader
        title="Service Health"
        actions={<Badge>{loading ? "…" : `${healthyCount}/${services.length}`}</Badge>}
      />

      {loading ? (
        <ul className={styles.healthList} aria-busy="true">
          {[0, 1, 2, 3].map((i) => (
            <SkeletonRow key={i} />
          ))}
        </ul>
      ) : services.length === 0 ? (
        <EmptyState compact icon={<Activity />} title="No services monitored" />
      ) : (
        <ul className={styles.healthList}>
          {services.slice(0, 8).map((s) => {
            const tone = STATUS_TONE[s.status] ?? "unknown";
            return (
              <li key={s.serviceName} className={styles.healthRow}>
                <span className={styles.healthDot} data-tone={tone} aria-hidden="true" />
                <span className={styles.healthName}>{s.serviceName}</span>
                <span className={styles.healthStatus} data-tone={tone}>
                  {s.status}
                </span>
                <span className={styles.latency}>{s.latencyMs.toFixed(0)}ms</span>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
}
