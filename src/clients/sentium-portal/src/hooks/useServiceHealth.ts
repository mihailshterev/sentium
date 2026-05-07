import { useQuery } from "@tanstack/react-query";
import { fetchServiceHealth } from "../services/watchdog.service";
import type { ServiceHealthStatus } from "../types/serviceHealth";

const POLL_INTERVAL = 15_000;
const SERVICE_HEALTH_KEY = ["service-health"] as const;

const useServiceHealth = () => {
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
};

export default useServiceHealth;
