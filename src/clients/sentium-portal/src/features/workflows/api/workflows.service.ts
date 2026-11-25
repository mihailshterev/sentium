import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { UpdateWorkflowPayload, WorkflowPayload, WorkflowRecord, WorkflowRun } from "../types/workflows";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/praxis";
const url = pathIn(BASE);

export const fetchWorkflowsPaged = (page = 1, pageSize = 100): Promise<PagedResponse<WorkflowRecord>> =>
  client.get<PagedResponse<WorkflowRecord>>(withQuery(`${BASE}/workflows`, { page, pageSize }));

export const createWorkflow = (payload: WorkflowPayload) => client.post<WorkflowRecord>(`${BASE}/workflows`, payload);

export const updateWorkflow = ({ id, ...payload }: UpdateWorkflowPayload) =>
  client.put<void>(url`/workflows/${id}`, payload);

export const deleteWorkflow = (id: string) => client.delete<void>(url`/workflows/${id}`);

export const fetchWorkflowRunsPaged = (page = 1, pageSize = 20, search?: string): Promise<PagedResponse<WorkflowRun>> =>
  client.get<PagedResponse<WorkflowRun>>(
    withQuery(`${BASE}/workflows/runs`, { page, pageSize, search: search?.trim() }),
  );

export const fetchWorkflowRuns = async (pageSize = 15): Promise<WorkflowRun[]> =>
  (await fetchWorkflowRunsPaged(1, pageSize)).items;

export const fetchWorkflowRun = (runId: string): Promise<WorkflowRun> =>
  client.get<WorkflowRun>(url`/workflows/runs/${runId}`);

export const renameWorkflowRun = (runId: string, title: string) =>
  client.put<void>(url`/workflows/runs/${runId}/title`, { title });
