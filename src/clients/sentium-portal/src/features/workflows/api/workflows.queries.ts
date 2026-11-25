import { queryOptions } from "@tanstack/react-query";
import { fetchWorkflowRun, fetchWorkflowRuns } from "./workflows.service";

export const workflowKeys = {
  all: ["workflows"] as const,
  list: () => [...workflowKeys.all, "list"] as const,
};

/** Every run query lives under `all`, so one realtime event or rename refreshes the lists and the open run. */
export const workflowRunKeys = {
  all: ["workflow-runs"] as const,
  history: (search: string) => [...workflowRunKeys.all, "history", search] as const,
  recent: (count: number) => [...workflowRunKeys.all, "recent", count] as const,
  detail: (runId: string) => [...workflowRunKeys.all, "detail", runId] as const,
};

export const workflowQueries = {
  recentRuns: (count: number) =>
    queryOptions({ queryKey: workflowRunKeys.recent(count), queryFn: () => fetchWorkflowRuns(count) }),
  run: (runId: string) => queryOptions({ queryKey: workflowRunKeys.detail(runId), queryFn: () => fetchWorkflowRun(runId) }),
};
