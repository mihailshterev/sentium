import styles from "../sentinel.module.scss";
import type { PolicyRiskLevel } from "../../../types/sentinel";

const RiskBadge = ({ risk }: { risk: PolicyRiskLevel }) => {
  const cls = {
    Low: styles.riskLow,
    Medium: styles.riskMedium,
    High: styles.riskHigh,
    Critical: styles.riskCritical,
  }[risk];
  return <span className={`${styles.riskBadge} ${cls}`}>{risk}</span>;
};

export default RiskBadge;
