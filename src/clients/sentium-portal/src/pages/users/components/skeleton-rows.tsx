import styles from "../users.module.scss";

const SkeletonRows = () => (
  <>
    {Array.from({ length: 6 }).map((_, i) => (
      <div key={i} className={styles.skeletonRow}>
        <div className={styles.skeletonAvatar} />
        <div className={styles.colUser}>
          <div className={styles.skeletonCell} style={{ width: "8rem" }} />
          <div className={styles.skeletonCell} style={{ width: "12rem", marginTop: 4 }} />
        </div>
        <div className={styles.skeletonCell} style={{ width: "6rem" }} />
        <div className={styles.skeletonCell} style={{ width: "10rem" }} />
        <div className={styles.skeletonCell} style={{ width: "2rem" }} />
      </div>
    ))}
  </>
);

export default SkeletonRows;
