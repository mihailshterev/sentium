import { useQuery } from "@tanstack/react-query";
import { vigilQueries } from "@/features/vigil/api/vigil.queries";

/** The Vigil stream refetches this on every change; the poll only covers a missed event. */
const POLL_INTERVAL = 60_000;

export function useSystemOverview() {
  const {
    data: overview,
    isLoading,
    error,
    refetch,
  } = useQuery({
    ...vigilQueries.overview(),
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { overview, isLoading, error, refetch };
}


