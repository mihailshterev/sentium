import { client } from "@/shared/api/client";
import type { UpdateWorkflowPayload, WorkflowPayload, WorkflowRecord, WorkflowRun } from "../types/workflows";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/agent-runtime";

export const fetchWorkflowsPaged = (page = 1, pageSize = 100): Promise<PagedResponse<WorkflowRecord>> =>
  client.get<PagedResponse<WorkflowRecord>>(`${BASE}/workflows?page=${page}&pageSize=${pageSize}`);

export const createWorkflow = (payload: WorkflowPayload) => client.post<WorkflowRecord>(`${BASE}/workflows`, payload);

export const updateWorkflow = ({ id, ...payload }: UpdateWorkflowPayload) =>
  client.put<WorkflowRecord>(`${BASE}/workflows/${id}`, payload);

export const deleteWorkflow = (id: string) => client.delete<void>(`${BASE}/workflows/${id}`);

export const fetchWorkflowRunsPaged = (page = 1, pageSize = 20): Promise<PagedResponse<WorkflowRun>> =>
  client.get<PagedResponse<WorkflowRun>>(`${BASE}/workflows/runs?page=${page}&pageSize=${pageSize}`);

export const fetchWorkflowRuns = async (pageSize = 15): Promise<WorkflowRun[]> =>
  (await fetchWorkflowRunsPaged(1, pageSize)).items;

export const fetchWorkflowRun = (runId: string): Promise<WorkflowRun> =>
  client.get<WorkflowRun>(`${BASE}/workflows/runs/${runId}`);
