import { useQuery } from "@tanstack/react-query";
import { vigilQueries } from "@/features/vigil/api/vigil.queries";

/** The Vigil stream refetches this on every change; the poll only covers a missed event. */
const POLL_INTERVAL = 60_000;

export function useIncidents() {
  const {
    data: incidents = [],
    isLoading,
    error,
    refetch,
  } = useQuery({
    ...vigilQueries.incidents(),
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { incidents, isLoading, error, refetch };
}


