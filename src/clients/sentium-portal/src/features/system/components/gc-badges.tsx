import styles from "../system.module.css";

interface GcBadgesProps {
  gen0: number;
  gen1: number;
  gen2: number;
}

export function GcBadges({ gen0, gen1, gen2 }: GcBadgesProps) {
  return (
    <div className={styles.counters}>
      {[
        ["Gen 0", gen0],
        ["Gen 1", gen1],
        ["Gen 2", gen2],
      ].map(([label, value]) => (
        <div key={label} className={styles.counter}>
          <span className={styles.counterValue}>{value}</span>
          <span className={styles.counterLabel}>{label}</span>
        </div>
      ))}
    </div>
  );
}
