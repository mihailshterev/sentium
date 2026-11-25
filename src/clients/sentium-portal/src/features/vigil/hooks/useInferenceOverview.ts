import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { vigilQueries } from "@/features/vigil/api/vigil.queries";
import type { InferenceRange } from "@/features/vigil/types/inference";

// The collector writes a minute once it is over, so polling faster than this shows nothing new.
const REFRESH_INTERVAL = 30_000;


/** Sovereign-only: pass `enabled: false` for everyone else so no request is made. */
export function useInferenceOverview(range: InferenceRange, enabled = true) {
  const { data, isLoading, isFetching, isPlaceholderData, error } = useQuery({
    ...vigilQueries.inference(range),
    refetchInterval: REFRESH_INTERVAL,
    // Switching range keeps the previous charts on screen until the new ones arrive.
    placeholderData: keepPreviousData,
    enabled,
    retry: false,
  });

  return { overview: data, isLoading, isUpdating: isFetching && isPlaceholderData, error };
}
