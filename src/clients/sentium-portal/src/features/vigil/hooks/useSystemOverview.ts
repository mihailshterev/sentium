import { useQuery } from "@tanstack/react-query";
import { fetchSystemOverview } from "@/features/vigil/api/vigil.service";
import type { SystemHealthOverview } from "@/features/vigil/types/serviceHealth";

export const SYSTEM_OVERVIEW_KEY = ["vigil-overview"] as const;

const POLL_INTERVAL = 15_000;

export function useSystemOverview() {
  const {
    data: overview,
    isLoading,
    error,
    refetch,
  } = useQuery<SystemHealthOverview>({
    queryKey: SYSTEM_OVERVIEW_KEY,
    queryFn: fetchSystemOverview,
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { overview, isLoading, error, refetch };
}


