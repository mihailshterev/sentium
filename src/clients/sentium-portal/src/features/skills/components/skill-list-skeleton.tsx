import styles from "../skills.module.css";
import { Skeleton } from "@/shared/ui/skeleton";

export function SkillListSkeleton() {
  return (
    <>
      <span className="visually-hidden">Loading skills…</span>
      <ul className={styles.skillList} aria-busy="true">
        {[0, 1, 2].map((i) => (
          <li key={i} className={`${styles.skillRow} ${styles.skeletonRow}`}>
            <Skeleton width="25%" />
            <Skeleton width="60%" />
          </li>
        ))}
      </ul>
    </>
  );
}
