import styles from "./aurora-background.module.scss";

const AuroraBackground = () => (
  <div className={styles.aurora} aria-hidden="true">
    <span className={`${styles.blob} ${styles.blob1}`} />
    <span className={`${styles.blob} ${styles.blob2}`} />
    <span className={`${styles.blob} ${styles.blob3}`} />
    <span className={`${styles.blob} ${styles.blob4}`} />
  </div>
);

export default AuroraBackground;
