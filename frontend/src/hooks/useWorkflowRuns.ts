import { useQuery } from "@tanstack/react-query";
import { fetchWorkflowRuns } from "../services/agentRuntime.service";
import type { WorkflowRun } from "../types/workflowRuns";

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
