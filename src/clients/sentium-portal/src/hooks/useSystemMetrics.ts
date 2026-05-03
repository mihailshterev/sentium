import { useQuery } from "@tanstack/react-query";
import { fetchSystemMetrics } from "../services/watchdog.service";

const REFRESH_INTERVAL = 5000;

const SYSTEM_METRICS_KEY = ["system-metrics"] as const;

const useSystemMetrics = () => {
  const {
    data: metrics,
    isLoading,
    isRefetching,
    error,
    refetch,
  } = useQuery({
    queryKey: SYSTEM_METRICS_KEY,
    queryFn: fetchSystemMetrics,
    refetchInterval: REFRESH_INTERVAL,
    retry: false,
  });

  return { metrics, isLoading, isRefetching, error, refetch };
};

export default useSystemMetrics;
