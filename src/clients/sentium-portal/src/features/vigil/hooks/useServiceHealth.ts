import { useQuery } from "@tanstack/react-query";
import { fetchServiceHealth } from "@/features/vigil/api/vigil.service";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";
import { REALTIME_FALLBACK_POLL_MS } from "@/shared/realtime";

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
    // The Vigil stream (mounted in the layout) patches this as targets change; the poll only covers a missed event.
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
    retry: false,
  });

  return { services, isLoading, error, refetch };
}
