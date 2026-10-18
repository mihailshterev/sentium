import { BASE_URL, client, handleUnauthorized } from "@/shared/api/client";
import type { CreateWorkspacePayload, UpdateWorkspacePayload, Workspace, WorkspaceFile } from "../types/workspace";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/agent-runtime";

export const listWorkspaceFiles = (workspaceId?: string): Promise<WorkspaceFile[]> =>
  client.get<WorkspaceFile[]>(
    `${BASE}/workspaces/files${workspaceId ? `?workspaceId=${encodeURIComponent(workspaceId)}` : ""}`,
  );

export const deleteWorkspaceFile = (fileId: string): Promise<void> =>
  client.delete<void>(`${BASE}/workspaces/files/${fileId}`);

export const fetchWorkspacesPaged = (page = 1, pageSize = 100): Promise<PagedResponse<Workspace>> =>
  client.get<PagedResponse<Workspace>>(`${BASE}/workspaces?page=${page}&pageSize=${pageSize}`);

export const fetchWorkspaces = async (): Promise<Workspace[]> => (await fetchWorkspacesPaged(1, 100)).items;

export const createWorkspace = (payload: CreateWorkspacePayload): Promise<Workspace> =>
  client.post<Workspace>(`${BASE}/workspaces`, payload);

export const updateWorkspace = (id: string, payload: UpdateWorkspacePayload): Promise<Workspace> =>
  client.put<Workspace>(`${BASE}/workspaces/${id}`, payload);

export const deleteWorkspace = (id: string): Promise<void> => client.delete<void>(`${BASE}/workspaces/${id}`);

export const fetchWorkspaceFiles = (workspaceId: string): Promise<WorkspaceFile[]> =>
  client.get<WorkspaceFile[]>(`${BASE}/workspaces/${workspaceId}/files`);

export const uploadWorkspaceFile = async (file: File, workspaceId?: string): Promise<WorkspaceFile> => {
  const formData = new FormData();
  formData.append("file", file);
  if (workspaceId) {
    formData.append("workspaceId", workspaceId);
  }

  const response = await fetch(`${BASE_URL}${BASE}/workspaces/files`, {
    method: "POST",
    body: formData,
    credentials: "include",
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error((errorData as { error?: string }).error || `Upload failed: ${response.status}`);
  }

  return response.json() as Promise<WorkspaceFile>;
};
