import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchWorkflowRuns, fetchWorkflowRun, renameWorkflowRun } from "@/features/workflows/api/workflows.service";
import type { WorkflowRun } from "@/features/workflows/types/workflows";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

const RUN_QUERY_KEYS = [["workflowRuns"], ["workflow-runs"], ["workflow-run"]] as const;

/** Keeps every run list live: each finished run is reported as it is recorded. */
export function useLiveWorkflowRuns() {
  useRealtimeInvalidate(REALTIME_TOPICS.workflowRuns, RUN_QUERY_KEYS);
}

export function useWorkflowRuns(count = 15) {
  useLiveWorkflowRuns();
  const { data, isLoading } = useQuery<WorkflowRun[]>({
    queryKey: ["workflow-runs", count],
    queryFn: () => fetchWorkflowRuns(count),
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
  });

  return {
    runs: data ?? [],
    isLoading,
  };
}

export function useWorkflowRun(runId: string | undefined) {
  const { data, isLoading, error } = useQuery<WorkflowRun>({
    queryKey: ["workflow-run", runId],
    queryFn: () => fetchWorkflowRun(runId as string),
    enabled: !!runId,
    retry: false,
  });

  return { run: data, isLoading, error };
}

export function useRenameWorkflowRun() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ runId, title }: { runId: string; title: string }) => renameWorkflowRun(runId, title),
    onSuccess: () =>
      Promise.all(RUN_QUERY_KEYS.map((queryKey) => queryClient.invalidateQueries({ queryKey: [...queryKey] }))),
  });
}
