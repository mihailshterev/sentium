import { client } from "@/shared/api/client";
import type { RunWorkflowPayload } from "@/features/workflows/types/workflows";

const BASE = "/agent-runtime";

export const runDynamicWorkflow = (payload: Record<string, string>) =>
  client.post<{ eventId: string }>(`${BASE}/orchestration/run-dynamic-workflow`, payload);

export const runWorkflowPipeline = (payload: RunWorkflowPayload) =>
  client.post<{ eventId: string }>(`${BASE}/orchestration/run-workflow`, payload);

export const cancelOrchestrationRun = (streamId: string) =>
  client.post<void>(`${BASE}/orchestration/cancel/${streamId}`, {});
