import { CheckCircle, Siren, ShieldOff } from "lucide-react";
import styles from "../sentinel.module.scss";

const EffectBadge = ({ allowed, effect }: { allowed: boolean; effect: string }) => {
  if (allowed) {
    return (
      <span className={`${styles.effectBadge} ${styles.effectAllow}`}>
        <CheckCircle size={11} /> Allow
      </span>
    );
  }
  if (effect === "DenyWithAlert") {
    return (
      <span className={`${styles.effectBadge} ${styles.effectAlert}`}>
        <Siren size={11} /> Alert
      </span>
    );
  }
  return (
    <span className={`${styles.effectBadge} ${styles.effectDeny}`}>
      <ShieldOff size={11} /> Deny
    </span>
  );
};

export default EffectBadge;
