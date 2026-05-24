import styles from "../sentinel.module.scss";

const AlignmentBadge = ({ verdict }: { verdict: string | null }) => {
  if (!verdict) {
    return <span className={styles.alignNone}>—</span>;
  }

  const cls =
    verdict === "Aligned" ? styles.alignGood : verdict === "Misaligned" ? styles.alignBad : styles.alignNeutral;

  return <span className={`${styles.alignBadge} ${cls}`}>{verdict}</span>;
};

export default AlignmentBadge;
