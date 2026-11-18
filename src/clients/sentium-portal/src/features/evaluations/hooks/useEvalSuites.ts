import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import {
  createEvalSuite,
  deleteEvalSuite,
  fetchEvalSuite,
  fetchEvalSuitesPaged,
  fetchEvalSuiteVersion,
  fetchEvalSuiteVersions,
  updateEvalSuite,
} from "../api/eval-suites.service";
import type { CreateEvalSuitePayload, EvalSuiteSummary, UpdateEvalSuitePayload } from "../types/eval-suites";

export const EVAL_SUITES_KEY = ["eval-suites"] as const;

export function useEvalSuites() {
  const qc = useQueryClient();
  const list = useInfiniteList<EvalSuiteSummary>([...EVAL_SUITES_KEY, "list"], fetchEvalSuitesPaged);

  const deleteMutation = useMutation({
    mutationFn: deleteEvalSuite,
    onSuccess: () => qc.invalidateQueries({ queryKey: EVAL_SUITES_KEY }),
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

  const suiteQuery = useQuery({
    queryKey: [...EVAL_SUITES_KEY, id, version ?? "current"],
    queryFn: () => (version === undefined ? fetchEvalSuite(id!) : fetchEvalSuiteVersion(id!, version)),
    enabled,
  });

  const versionsQuery = useQuery({
    queryKey: [...EVAL_SUITES_KEY, id, "versions"],
    queryFn: () => fetchEvalSuiteVersions(id!),
    enabled,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: EVAL_SUITES_KEY });

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
