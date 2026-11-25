import { useQuery } from "@tanstack/react-query";
import { vigilQueries } from "@/features/vigil/api/vigil.queries";
import { REALTIME_FALLBACK_POLL_MS } from "@/shared/realtime";

export function useServiceHealth() {
  const {
    data: services = [],
    isLoading,
    error,
    refetch,
  } = useQuery({
    ...vigilQueries.serviceHealth(),
    // The Vigil stream (mounted in the layout) patches this as targets change; the poll only covers a missed event.
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
    retry: false,
  });

  return { services, isLoading, error, refetch };
}
