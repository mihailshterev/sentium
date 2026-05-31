import { useQuery } from "@tanstack/react-query";
import { fetchWorkflowRuns, fetchWorkflowRun } from "../services/agentRuntime.service";
import type { WorkflowRun } from "../types/workflows";

export default function useWorkflowRuns(count = 15) {
  const { data, isLoading } = useQuery<WorkflowRun[]>({
    queryKey: ["workflow-runs", count],
    queryFn: () => fetchWorkflowRuns(count),
    refetchInterval: 10000,
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
