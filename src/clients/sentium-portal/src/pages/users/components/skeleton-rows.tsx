import styles from "../users.module.scss";

const SkeletonRows = () => (
  <>
    {Array.from({ length: 6 }).map((_, i) => (
      <div key={i} className={styles.skeletonRow}>
        <div className={styles.skeletonCell} style={{ width: "7rem" }} />
        <div className={styles.skeletonCell} style={{ width: "11rem" }} />
        <div className={styles.skeletonCell} style={{ width: "6rem" }} />
        <div className={styles.skeletonCell} style={{ width: "9rem" }} />
        <div className={styles.skeletonCell} style={{ width: "2rem" }} />
      </div>
    ))}
  </>
);

export default SkeletonRows;
