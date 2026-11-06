import styles from "../users.module.css";
import { Skeleton } from "@/shared/ui/skeleton";

export function SkeletonRows() {
  return (
    <div className={styles.skeleton} aria-busy="true">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className={styles.skeletonRow}>
          <Skeleton width={32} height={32} radius="full" />
          <span className={styles.skeletonText}>
            <Skeleton width="8rem" />
            <Skeleton width="12rem" />
          </span>
          <Skeleton width="6rem" />
          <Skeleton width="10rem" />
        </div>
      ))}
    </div>
  );
}
