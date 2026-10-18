import { useQuery } from "@tanstack/react-query";
import { fetchIncidents } from "@/features/watchdog/api/watchdog.service";
import type { Incident } from "@/features/watchdog/types/serviceHealth";

export const INCIDENTS_KEY = ["watchdog-incidents"] as const;

const POLL_INTERVAL = 15_000;

export function useIncidents() {
  const {
    data: incidents = [],
    isLoading,
    error,
    refetch,
  } = useQuery<Incident[]>({
    queryKey: INCIDENTS_KEY,
    queryFn: fetchIncidents,
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { incidents, isLoading, error, refetch };
}


