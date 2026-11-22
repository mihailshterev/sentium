import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";
import {
  cancelEvaluation,
  deleteEvaluation,
  fetchEvaluation,
  fetchEvaluationsPaged,
  startEvaluation,
} from "../api/evaluations.service";
import { isActive, type EvaluationSummary } from "../types/evaluations";

export const EVALUATIONS_KEY = ["evaluations"] as const;

/**
 * How often an Evaluation that is still queued or running is re-read on its own. Its progress arrives as realtime
 * events; this only covers a missed one.
 */
export const EVALUATION_POLL_MS = REALTIME_FALLBACK_POLL_MS;

const EVALUATION_KEYS = [EVALUATIONS_KEY, ["benchmarks"]] as const;

/** The user's Evaluations, newest first; `suiteId` narrows them to one suite. Live while any is still active. */
export function useEvaluations(suiteId?: string) {
  useRealtimeInvalidate(REALTIME_TOPICS.evaluations, EVALUATION_KEYS);

  const list = useInfiniteList<EvaluationSummary>(
    [...EVALUATIONS_KEY, "list", suiteId ?? "all"],
    (page, pageSize) => fetchEvaluationsPaged(suiteId, page, pageSize),
    { refetchInterval: (items) => (items.some((e) => isActive(e.status)) ? EVALUATION_POLL_MS : false) },
  );

  return {
    evaluations: list.items,
    isLoading: list.isLoading,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
  };
}

export function useEvaluation(id: string | undefined) {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: EVALUATIONS_KEY });
  useRealtimeInvalidate(REALTIME_TOPICS.evaluations, EVALUATION_KEYS);

  const query = useQuery({
    queryKey: [...EVALUATIONS_KEY, id],
    queryFn: () => fetchEvaluation(id!),
    enabled: id !== undefined,
    refetchInterval: (q) => (q.state.data && isActive(q.state.data.summary.status) ? EVALUATION_POLL_MS : false),
  });

  const cancelMutation = useMutation({ mutationFn: () => cancelEvaluation(id!), onSuccess: invalidate });
  const deleteMutation = useMutation({ mutationFn: () => deleteEvaluation(id!), onSuccess: invalidate });

  return {
    evaluation: query.data,
    isLoading: query.isLoading,
    error: query.error,
    cancel: cancelMutation.mutateAsync,
    isCancelling: cancelMutation.isPending,
    remove: deleteMutation.mutateAsync,
    isDeleting: deleteMutation.isPending,
  };
}

export function useStartEvaluation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: startEvaluation,
    onSuccess: () => qc.invalidateQueries({ queryKey: EVALUATIONS_KEY }),
  });
}
