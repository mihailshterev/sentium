import { keepPreviousData, useInfiniteQuery, type QueryKey } from "@tanstack/react-query";
import type { PagedResponse } from "@/shared/types/pagination";

interface UseInfiniteListOptions<T> {
  pageSize?: number;
  /** A fixed interval, or one decided from the loaded items (`false` stops polling). */
  refetchInterval?: number | ((items: T[]) => number | false);
  enabled?: boolean;
  staleTime?: number;
  keepPrevious?: boolean;
}

export function useInfiniteList<T>(
  queryKey: QueryKey,
  fetchPage: (page: number, pageSize: number) => Promise<PagedResponse<T>>,
  options: UseInfiniteListOptions<T> = {},
) {
  const pageSize = options.pageSize ?? 20;
  const { refetchInterval } = options;

  const query = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => fetchPage(pageParam, pageSize),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.page < lastPage.totalPages ? lastPage.page + 1 : undefined),
    refetchInterval:
      typeof refetchInterval === "function"
        ? (q) => refetchInterval(q.state.data?.pages.flatMap((page) => page.items) ?? [])
        : refetchInterval,
    enabled: options.enabled,
    staleTime: options.staleTime,
    placeholderData: options.keepPrevious ? keepPreviousData : undefined,
  });

  return {
    items: query.data?.pages.flatMap((page) => page.items) ?? [],
    totalCount: query.data?.pages[0]?.totalCount ?? 0,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    hasMore: query.hasNextPage,
    loadMore: query.fetchNextPage,
    isLoadingMore: query.isFetchingNextPage,
    refetch: query.refetch,
  };
}
