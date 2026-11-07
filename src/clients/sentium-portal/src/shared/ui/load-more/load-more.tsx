import { Button } from "@/shared/ui/button";
import styles from "./load-more.module.css";

interface LoadMoreProps {
  hasMore: boolean;
  isLoading: boolean;
  onLoadMore: () => void;
}

export function LoadMore({ hasMore, isLoading, onLoadMore }: LoadMoreProps) {
  if (!hasMore) {
    return null;
  }

  return (
    <div className={styles.wrap}>
      <Button variant="secondary" size="sm" onClick={onLoadMore} loading={isLoading}>
        {isLoading ? "Loading…" : "Load more"}
      </Button>
    </div>
  );
}
