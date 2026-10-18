import { Loader2 } from "lucide-react";
import styles from "./load-more.module.scss";

interface LoadMoreProps {
  hasMore: boolean;
  isLoading: boolean;
  onLoadMore: () => void;
}

const LoadMore = ({ hasMore, isLoading, onLoadMore }: LoadMoreProps) => {
  if (!hasMore) {
    return null;
  }

  return (
    <div className={styles.wrap}>
      <button className={styles.btn} onClick={onLoadMore} disabled={isLoading}>
        {isLoading ? (
          <>
            <Loader2 size={13} className={styles.spinner} />
            Loading…
          </>
        ) : (
          "Load more"
        )}
      </button>
    </div>
  );
};

export default LoadMore;
