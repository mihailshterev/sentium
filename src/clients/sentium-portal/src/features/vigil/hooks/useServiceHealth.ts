import { useQuery } from "@tanstack/react-query";
import { fetchServiceHealth } from "@/features/vigil/api/vigil.service";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";

const POLL_INTERVAL = 15_000;
export const SERVICE_HEALTH_KEY = ["service-health"] as const;

export function useServiceHealth() {
  const {
    data: services = [],
    isLoading,
    error,
    refetch,
  } = useQuery<ServiceHealthStatus[]>({
    queryKey: SERVICE_HEALTH_KEY,
    queryFn: fetchServiceHealth,
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { services, isLoading, error, refetch };
}


