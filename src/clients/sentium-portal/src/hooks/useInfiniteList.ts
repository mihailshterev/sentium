import { useInfiniteQuery, type QueryKey } from "@tanstack/react-query";
import type { PagedResponse } from "../types/pagination";

interface UseInfiniteListOptions {
  pageSize?: number;
  refetchInterval?: number;
  enabled?: boolean;
  staleTime?: number;
}

export function useInfiniteList<T>(
  queryKey: QueryKey,
  fetchPage: (page: number, pageSize: number) => Promise<PagedResponse<T>>,
  options: UseInfiniteListOptions = {},
) {
  const pageSize = options.pageSize ?? 20;

  const query = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => fetchPage(pageParam, pageSize),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.page < lastPage.totalPages ? lastPage.page + 1 : undefined),
    refetchInterval: options.refetchInterval,
    enabled: options.enabled,
    staleTime: options.staleTime,
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
