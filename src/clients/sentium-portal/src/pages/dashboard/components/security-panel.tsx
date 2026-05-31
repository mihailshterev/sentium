import { ArrowRight, BrickWallShield } from "lucide-react";
import styles from "../dashboard.module.scss";
import type { AuditStats } from "../../../types/sentinel";

interface SecurityPanelProps {
  stats: AuditStats | undefined;
  loading: boolean;
  onNavigate: (to: string) => void;
}

function scoreColor(score: number): string {
  if (score >= 0.7) {
    return "var(--accent-green)";
  }
  if (score >= 0.4) {
    return "var(--accent-amber)";
  }
  return "var(--accent-red)";
}

const SecurityPanel = ({ stats, loading, onNavigate }: SecurityPanelProps) => (
  <section className={styles.section}>
    <div className={styles.sectionHeader}>
      <div className={styles.sectionTitleRow}>
        <BrickWallShield size={14} style={{ color: "var(--accent-amber)", flexShrink: 0 }} />
        <span className={styles.sectionTitle}>Security Overview</span>
      </div>
    </div>

    <div className={styles.auditCounts}>
      <div className={styles.auditCount}>
        <span className={styles.auditCountValue}>{loading ? "—" : (stats?.total ?? 0)}</span>
        <span className={styles.auditCountLabel}>Total</span>
      </div>
      <div className={styles.auditCount}>
        <span className={styles.auditCountValue} style={{ color: "var(--accent-green)" }}>
          {loading ? "—" : (stats?.allowed ?? 0)}
        </span>
        <span className={styles.auditCountLabel}>Allowed</span>
      </div>
      <div className={styles.auditCount}>
        <span className={styles.auditCountValue} style={{ color: "var(--accent-red)" }}>
          {loading ? "—" : (stats?.denied ?? 0)}
        </span>
        <span className={styles.auditCountLabel}>Denied</span>
      </div>
      <div className={styles.auditCount}>
        <span className={styles.auditCountValue} style={{ color: "var(--accent-amber)" }}>
          {loading ? "—" : (stats?.alerts ?? 0)}
        </span>
        <span className={styles.auditCountLabel}>Alerts</span>
      </div>
    </div>

    <div className={styles.riskPillsRow}>
      <span className={`${styles.riskPill} ${styles.riskPillLow}`}>Low {stats?.lowRisk ?? 0}</span>
      <span className={`${styles.riskPill} ${styles.riskPillMedium}`}>Med {stats?.mediumRisk ?? 0}</span>
      <span className={`${styles.riskPill} ${styles.riskPillHigh}`}>High {stats?.highRisk ?? 0}</span>
      <span className={`${styles.riskPill} ${styles.riskPillCritical}`}>Crit {stats?.criticalRisk ?? 0}</span>
    </div>

    {stats?.latestAlignmentScore != null && (
      <div className={styles.alignmentRow}>
        <span className={styles.alignmentLabel}>Alignment Score</span>
        <span className={styles.alignmentScore} style={{ color: scoreColor(stats.latestAlignmentScore) }}>
          {(stats.latestAlignmentScore * 100).toFixed(0)}%
        </span>
      </div>
    )}

    <div className={styles.securityFooter}>
      <button className={styles.viewAuditBtn} onClick={() => onNavigate("/sentinel")}>
        View Audit Log <ArrowRight size={12} />
      </button>
    </div>
  </section>
);

export default SecurityPanel;
