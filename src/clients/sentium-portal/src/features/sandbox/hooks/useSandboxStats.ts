import { useQuery } from "@tanstack/react-query";
import { sandboxKeys, sandboxQueries } from "@/features/sandbox/api/sandbox.queries";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

const STATS_KEYS = [sandboxKeys.stats()] as const;

export const useSandboxStats = () => {
  useRealtimeInvalidate(REALTIME_TOPICS.sandboxExecutions, STATS_KEYS);
  const { data, isLoading, error, refetch } = useQuery({
    ...sandboxQueries.stats(),
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
    retry: false,
  });

  return { stats: data ?? null, isLoading, error, refetch };
};
