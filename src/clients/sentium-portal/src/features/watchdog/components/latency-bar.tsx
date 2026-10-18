import styles from "../watchdog.module.scss";

const LatencyBar = ({ latencyMs }: { latencyMs: number }) => {
  const pct = Math.min((latencyMs / 1000) * 100, 100);
  const color = latencyMs < 100 ? "green" : latencyMs < 400 ? "amber" : "red";
  return (
    <div className={styles.latencyBarWrap}>
      <div className={`${styles.latencyBar} ${styles[`latencyBar_${color}`]}`} style={{ width: `${pct}%` }} />
    </div>
  );
};

export default LatencyBar;
