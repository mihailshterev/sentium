import { ArrowRight } from "lucide-react";
import styles from "../dashboard.module.css";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import type { AuditStats } from "@/features/sentinel/types/sentinel";

interface SecurityPanelProps {
  stats: AuditStats | undefined;
  loading: boolean;
  onNavigate: (to: string) => void;
}

function scoreTone(score: number): "ok" | "warn" | "critical" {
  if (score >= 0.7) {
    return "ok";
  }
  return score >= 0.4 ? "warn" : "critical";
}

export function SecurityPanel({ stats, loading, onNavigate }: SecurityPanelProps) {
  const counts = [
    { label: "Total", value: stats?.total ?? 0, tone: "neutral" },
    { label: "Allowed", value: stats?.allowed ?? 0, tone: "ok" },
    { label: "Denied", value: stats?.denied ?? 0, tone: "critical" },
    { label: "Alerts", value: stats?.alerts ?? 0, tone: "warn" },
  ];

  const risks = [
    { label: "Low", value: stats?.lowRisk ?? 0, tone: "low" },
    { label: "Medium", value: stats?.mediumRisk ?? 0, tone: "medium" },
    { label: "High", value: stats?.highRisk ?? 0, tone: "high" },
    { label: "Critical", value: stats?.criticalRisk ?? 0, tone: "critical" },
  ];
  const riskTotal = risks.reduce((sum, r) => sum + r.value, 0);

  return (
    <Card padding="md" as="section">
      <CardHeader title="Security Overview" description="Sentinel policy decisions" />

      <dl className={styles.auditCounts}>
        {counts.map((c) => (
          <div key={c.label} className={styles.auditCount}>
            <dt className={styles.auditLabel}>{c.label}</dt>
            <dd className={styles.auditValue} data-tone={c.tone}>
              {loading ? "—" : c.value.toLocaleString()}
            </dd>
          </div>
        ))}
      </dl>

      <div className={styles.riskBlock}>
        <div className={styles.riskBar} aria-hidden="true">
          {riskTotal > 0 &&
            risks.map((r) =>
              r.value > 0 ? (
                <span key={r.label} className={styles.riskSegment} data-tone={r.tone} style={{ flexGrow: r.value }} />
              ) : null,
            )}
        </div>
        <ul className={styles.riskLegend}>
          {risks.map((r) => (
            <li key={r.label} className={styles.riskLegendItem}>
              <span className={styles.riskSwatch} data-tone={r.tone} aria-hidden="true" />
              <span>{r.label}</span>
              <span className={styles.riskLegendValue}>{r.value.toLocaleString()}</span>
            </li>
          ))}
        </ul>
      </div>

      {stats?.latestAlignmentScore != null && (
        <div className={styles.alignmentRow}>
          <span className={styles.alignmentLabel}>Alignment score</span>
          <span className={styles.alignmentScore} data-tone={scoreTone(stats.latestAlignmentScore)}>
            {(stats.latestAlignmentScore * 100).toFixed(0)}%
          </span>
        </div>
      )}

      <Button
        variant="secondary"
        fullWidth
        iconRight={<ArrowRight size={14} />}
        onClick={() => onNavigate("/sentinel")}
        className={styles.auditButton}
      >
        View Audit Log
      </Button>
    </Card>
  );
}
