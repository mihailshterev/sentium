import { useQuery } from "@tanstack/react-query";
import { fetchAuditLog } from "@/features/sentinel/api/sentinel.service";
import { sentinelKeys, sentinelQueries } from "@/features/sentinel/api/sentinel.queries";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";
import type { AuditRecord } from "@/features/sentinel/types/sentinel";

const AUDIT_KEYS = [sentinelKeys.audit(), sentinelKeys.stats()] as const;
const DEFAULT_PAGE_SIZE = 20;

export const useSentinelAudit = (pageSize = DEFAULT_PAGE_SIZE) => {
  useRealtimeInvalidate(REALTIME_TOPICS.sentinelAudit, AUDIT_KEYS);
  const list = useInfiniteList<AuditRecord>(sentinelKeys.audit(), fetchAuditLog, {
    pageSize,
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
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
  useRealtimeInvalidate(REALTIME_TOPICS.sentinelAudit, AUDIT_KEYS);
  const {
    data: stats,
    isLoading,
    error,
  } = useQuery({
    ...sentinelQueries.stats(),
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
    retry: false,
  });

  return { stats, isLoading, error };
};
