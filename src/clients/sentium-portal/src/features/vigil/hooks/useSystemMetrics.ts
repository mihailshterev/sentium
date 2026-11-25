import { useQuery } from "@tanstack/react-query";
import { vigilQueries } from "@/features/vigil/api/vigil.queries";

const REFRESH_INTERVAL = 5000;

export function useSystemMetrics() {
  const {
    data: metrics,
    isLoading,
    isRefetching,
    error,
    refetch,
  } = useQuery({
    ...vigilQueries.systemMetrics(),
    refetchInterval: REFRESH_INTERVAL,
    retry: false,
  });

  return { metrics, isLoading, isRefetching, error, refetch };
}


