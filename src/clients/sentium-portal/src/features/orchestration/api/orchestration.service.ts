import { client } from "@/shared/api/client";
import { pathIn } from "@/shared/api/url";
import type { RunDynamicWorkflowPayload, RunWorkflowPayload } from "@/features/workflows/types/workflows";

const BASE = "/praxis";
const url = pathIn(BASE);

export const runDynamicWorkflow = (payload: RunDynamicWorkflowPayload) =>
  client.post<{ eventId: string }>(`${BASE}/orchestration/run-dynamic-workflow`, payload);

export const runWorkflowPipeline = (payload: RunWorkflowPayload) =>
  client.post<{ eventId: string }>(`${BASE}/orchestration/run-workflow`, payload);

export const cancelOrchestrationRun = (streamId: string) =>
  client.post<void>(url`/orchestration/cancel/${streamId}`, {});
