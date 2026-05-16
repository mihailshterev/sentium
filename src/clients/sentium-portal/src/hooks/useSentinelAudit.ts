import { useQuery } from "@tanstack/react-query";
import { fetchAuditLog, fetchAuditStats } from "../services/sentinel.service";
import type { AuditRecord, AuditStats } from "../types/sentinel";

const POLL_INTERVAL = 5_000;

export const useSentinelAudit = (count = 100) => {
  const {
    data: records = [],
    isLoading,
    error,
    refetch,
  } = useQuery<AuditRecord[]>({
    queryKey: ["sentinel-audit", count],
    queryFn: () => fetchAuditLog(count),
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { records, isLoading, error, refetch };
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
