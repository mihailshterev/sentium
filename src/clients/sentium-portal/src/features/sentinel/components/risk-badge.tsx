import styles from "../sentinel.module.scss";
import type { PolicyRiskLevel } from "@/features/sentinel/types/sentinel";

export function RiskBadge({ risk }: { risk: PolicyRiskLevel }) {
  const cls = {
    Low: styles.riskLow,
    Medium: styles.riskMedium,
    High: styles.riskHigh,
    Critical: styles.riskCritical,
  }[risk];
  return <span className={`${styles.riskBadge} ${cls}`}>{risk}</span>;
}


