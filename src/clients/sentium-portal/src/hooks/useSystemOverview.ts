import { useQuery } from "@tanstack/react-query";
import { fetchSystemOverview } from "../services/watchdog.service";
import type { SystemHealthOverview } from "../types/serviceHealth";

export const SYSTEM_OVERVIEW_KEY = ["watchdog-overview"] as const;

const POLL_INTERVAL = 15_000;

const useSystemOverview = () => {
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
};

export default useSystemOverview;
