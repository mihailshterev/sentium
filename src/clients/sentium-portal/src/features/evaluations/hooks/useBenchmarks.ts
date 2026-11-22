import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import { REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";
import { cancelBenchmark, deleteBenchmark, fetchBenchmark, fetchBenchmarksPaged, startBenchmark } from "../api/benchmarks.service";
import type { BenchmarkSummary } from "../types/benchmarks";
import { isActive } from "../types/evaluations";
import { EVALUATION_POLL_MS, EVALUATIONS_KEY } from "./useEvaluations";

export const BENCHMARKS_KEY = ["benchmarks"] as const;

const BENCHMARK_KEYS = [BENCHMARKS_KEY, EVALUATIONS_KEY] as const;

/** The user's Benchmarks, newest first. Live while any of their Evaluations is still active. */
export function useBenchmarks() {
  useRealtimeInvalidate(REALTIME_TOPICS.evaluations, BENCHMARK_KEYS);
  const list = useInfiniteList<BenchmarkSummary>([...BENCHMARKS_KEY, "list"], fetchBenchmarksPaged, {
    refetchInterval: (items) =>
      items.some((b) => b.evaluations.some((e) => isActive(e.status))) ? EVALUATION_POLL_MS : false,
  });

  return {
    benchmarks: list.items,
    isLoading: list.isLoading,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
  };
}

export function useBenchmark(id: string | undefined) {
  const qc = useQueryClient();
  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: BENCHMARKS_KEY });
    await qc.invalidateQueries({ queryKey: EVALUATIONS_KEY });
  };
  useRealtimeInvalidate(REALTIME_TOPICS.evaluations, BENCHMARK_KEYS);

  const query = useQuery({
    queryKey: [...BENCHMARKS_KEY, id],
    queryFn: () => fetchBenchmark(id!),
    enabled: id !== undefined,
    refetchInterval: (q) =>
      q.state.data?.evaluations.some((e) => isActive(e.summary.status)) ? EVALUATION_POLL_MS : false,
  });

  const cancelMutation = useMutation({ mutationFn: () => cancelBenchmark(id!), onSuccess: invalidate });
  const deleteMutation = useMutation({ mutationFn: () => deleteBenchmark(id!), onSuccess: invalidate });

  return {
    benchmark: query.data,
    isLoading: query.isLoading,
    error: query.error,
    cancel: cancelMutation.mutateAsync,
    isCancelling: cancelMutation.isPending,
    remove: deleteMutation.mutateAsync,
    isDeleting: deleteMutation.isPending,
  };
}

export function useStartBenchmark() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: startBenchmark,
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: BENCHMARKS_KEY });
      await qc.invalidateQueries({ queryKey: EVALUATIONS_KEY });
    },
  });
}
