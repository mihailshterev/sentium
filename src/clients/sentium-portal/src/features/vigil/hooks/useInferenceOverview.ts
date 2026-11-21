import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { fetchInferenceOverview } from "@/features/vigil/api/vigil.service";
import type { InferenceRange } from "@/features/vigil/types/inference";

// The collector writes a minute once it is over, so polling faster than this shows nothing new.
const REFRESH_INTERVAL = 30_000;

export const inferenceOverviewKey = (range: InferenceRange) => ["vigil", "inference", range] as const;

/** Sovereign-only: pass `enabled: false` for everyone else so no request is made. */
export function useInferenceOverview(range: InferenceRange, enabled = true) {
  const { data, isLoading, isFetching, isPlaceholderData, error } = useQuery({
    queryKey: inferenceOverviewKey(range),
    queryFn: () => fetchInferenceOverview(range),
    refetchInterval: REFRESH_INTERVAL,
    // Switching range keeps the previous charts on screen until the new ones arrive.
    placeholderData: keepPreviousData,
    enabled,
    retry: false,
  });

  return { overview: data, isLoading, isUpdating: isFetching && isPlaceholderData, error };
}
