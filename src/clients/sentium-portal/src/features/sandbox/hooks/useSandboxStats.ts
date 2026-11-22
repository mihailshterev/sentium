import { useQuery } from "@tanstack/react-query";
import { fetchSandboxStats } from "@/features/sandbox/api/sandbox.service";
import type { SandboxStats } from "@/features/sandbox/types/sandbox";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

const STATS_KEYS = [["sandbox-stats"]] as const;

export const useSandboxStats = () => {
  useRealtimeInvalidate(REALTIME_TOPICS.sandboxExecutions, STATS_KEYS);
  const { data, isLoading, error, refetch } = useQuery<SandboxStats>({
    queryKey: ["sandbox-stats"],
    queryFn: fetchSandboxStats,
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
    retry: false,
  });

  return { stats: data ?? null, isLoading, error, refetch };
};
