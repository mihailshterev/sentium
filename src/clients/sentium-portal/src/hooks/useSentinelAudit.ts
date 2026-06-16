import { useQuery } from "@tanstack/react-query";
import { fetchAuditLog, fetchAuditStats } from "../services/sentinel.service";
import { useInfiniteList } from "./useInfiniteList";
import type { AuditRecord, AuditStats } from "../types/sentinel";

const POLL_INTERVAL = 5_000;
const DEFAULT_PAGE_SIZE = 20;

export const useSentinelAudit = (pageSize = DEFAULT_PAGE_SIZE) => {
  const list = useInfiniteList<AuditRecord>(["sentinel-audit"], fetchAuditLog, {
    pageSize,
    refetchInterval: POLL_INTERVAL,
  });

  return {
    records: list.items,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
    isLoading: list.isLoading,
    error: list.error,
    refetch: list.refetch,
  };
};

export const useSentinelStats = () => {
  const {
    data: stats,
    isLoading,
    error,
  } = useQuery<AuditStats>({
    queryKey: ["sentinel-stats"],
    queryFn: fetchAuditStats,
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { stats, isLoading, error };
};
