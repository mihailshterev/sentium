import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import { createEvalSuite, deleteEvalSuite, fetchEvalSuitesPaged, updateEvalSuite } from "../api/eval-suites.service";
import { evalSuiteKeys, evaluationQueries } from "../api/evaluations.queries";
import type { CreateEvalSuitePayload, EvalSuiteSummary, UpdateEvalSuitePayload } from "../types/eval-suites";

export function useEvalSuites() {
  const qc = useQueryClient();
  const list = useInfiniteList<EvalSuiteSummary>(evalSuiteKeys.list(), fetchEvalSuitesPaged);

  const deleteMutation = useMutation({
    mutationFn: deleteEvalSuite,
    onSuccess: () => qc.invalidateQueries({ queryKey: evalSuiteKeys.all }),
  });

  return {
    suites: list.items,
    isLoading: list.isLoading,
    error: list.error,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
    deleteSuite: deleteMutation.mutateAsync,
    isDeleting: deleteMutation.isPending,
  };
}

/**
 * One suite for the editor. `version` opens an earlier version read-only; leave it undefined for the current one.
 */
export function useEvalSuite(id: string | undefined, version?: number) {
  const qc = useQueryClient();
  const enabled = id !== undefined;

  const suiteQuery = useQuery({ ...evaluationQueries.suite(id ?? "", version), enabled });
  const versionsQuery = useQuery({ ...evaluationQueries.suiteVersions(id ?? ""), enabled });

  const invalidate = () => qc.invalidateQueries({ queryKey: evalSuiteKeys.all });

  const createMutation = useMutation({
    mutationFn: (payload: CreateEvalSuitePayload) => createEvalSuite(payload),
    onSuccess: invalidate,
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateEvalSuitePayload) => updateEvalSuite(id!, payload),
    onSuccess: invalidate,
  });

  return {
    suite: suiteQuery.data,
    isLoading: enabled && suiteQuery.isLoading,
    error: suiteQuery.error,
    versions: versionsQuery.data ?? [],
    createSuite: createMutation.mutateAsync,
    updateSuite: updateMutation.mutateAsync,
    isSaving: createMutation.isPending || updateMutation.isPending,
  };
}
