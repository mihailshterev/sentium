import { useQuery } from "@tanstack/react-query";
import { fetchIncidents } from "@/features/vigil/api/vigil.service";
import type { Incident } from "@/features/vigil/types/serviceHealth";

export const INCIDENTS_KEY = ["vigil-incidents"] as const;

/** The Vigil stream refetches this on every change; the poll only covers a missed event. */
const POLL_INTERVAL = 60_000;

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


