import styles from "../system.module.scss";

interface GcBadgesProps {
  gen0: number;
  gen1: number;
  gen2: number;
}

const GcBadges = ({ gen0, gen1, gen2 }: GcBadgesProps) => (
  <div className={styles.gcRow}>
    <div className={styles.gcBadge}>
      <span className={styles.gcBadgeValue}>{gen0}</span>
      <span className={styles.gcBadgeLabel}>Gen 0</span>
    </div>
    <div className={styles.gcBadge}>
      <span className={styles.gcBadgeValue}>{gen1}</span>
      <span className={styles.gcBadgeLabel}>Gen 1</span>
    </div>
    <div className={styles.gcBadge}>
      <span className={styles.gcBadgeValue}>{gen2}</span>
      <span className={styles.gcBadgeLabel}>Gen 2</span>
    </div>
  </div>
);

export default GcBadges;
