import { BrickWallShield, ArrowRight } from "lucide-react";
import styles from "../dashboard.module.scss";

interface SecurityRowProps {
  onNavigate: (to: string) => void;
}

const SecurityRow = ({ onNavigate }: SecurityRowProps) => (
  <div className={styles.securityRow}>
    <div className={styles.securityCard}>
      <BrickWallShield size={16} />
      <div className={styles.securityCardContent}>
        <span className={styles.securityCardLabel}>Sentinel</span>
        <span className={styles.securityCardSub}>Agent security guardrails active</span>
      </div>
      <button className={styles.securityCardBtn} onClick={() => onNavigate("/sentinel")}>
        View <ArrowRight size={12} />
      </button>
    </div>
  </div>
);

export default SecurityRow;
