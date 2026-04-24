import { useQuery } from "@tanstack/react-query";
import { fetchNetworkEvents } from "../services/sentinel.service";
import type { NetworkEvent } from "../types/sentinel";

export default function useSentinelEvents(count = 100) {
  const { data, isLoading, isRefetching, error, refetch } = useQuery<NetworkEvent[]>({
    queryKey: ["sentinel-network-events", count],
    queryFn: () => fetchNetworkEvents(count),
    refetchInterval: 5000,
  });

  return {
    events: data ?? [],
    isLoading,
    isRefetching,
    error,
    refetch,
  };
}
