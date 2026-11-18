import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import {
  cancelEvaluation,
  deleteEvaluation,
  fetchEvaluation,
  fetchEvaluationsPaged,
  startEvaluation,
} from "../api/evaluations.service";
import { isActive, type EvaluationSummary } from "../types/evaluations";

export const EVALUATIONS_KEY = ["evaluations"] as const;

/** How often an Evaluation that is still queued or running is re-read. */
export const EVALUATION_POLL_MS = 2000;

/** The user's Evaluations, newest first; `suiteId` narrows them to one suite. Polls while any is still active. */
export function useEvaluations(suiteId?: string) {
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
