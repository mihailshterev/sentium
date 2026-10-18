import { useEffect, useRef } from "react";
import { Loader2 } from "lucide-react";
import styles from "./infinite-scroll-sentinel.module.scss";

interface InfiniteScrollSentinelProps {
  hasMore: boolean;
  isLoading: boolean;
  onLoadMore: () => void;
  rootMargin?: string;
}

const getScrollParent = (node: HTMLElement | null): HTMLElement | null => {
  let parent = node?.parentElement ?? null;
  while (parent) {
    const overflowY = getComputedStyle(parent).overflowY;
    if ((overflowY === "auto" || overflowY === "scroll") && parent.scrollHeight > parent.clientHeight) {
      return parent;
    }
    parent = parent.parentElement;
  }
  return null;
};

const InfiniteScrollSentinel = ({
  hasMore,
  isLoading,
  onLoadMore,
  rootMargin = "150px",
}: InfiniteScrollSentinelProps) => {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = ref.current;
    if (!el || !hasMore) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && !isLoading) {
          onLoadMore();
        }
      },
      { root: getScrollParent(el), rootMargin },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [hasMore, isLoading, onLoadMore, rootMargin]);

  if (!hasMore && !isLoading) {
    return null;
  }

  return (
    <div ref={ref} className={styles.sentinel} aria-hidden>
      {isLoading && <Loader2 size={14} className={styles.spinner} />}
    </div>
  );
};

export default InfiniteScrollSentinel;
