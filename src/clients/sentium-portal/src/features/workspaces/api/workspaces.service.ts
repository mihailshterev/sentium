import { ApiError, BASE_URL, client, handleUnauthorized } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type {
  CreateWorkspacePayload,
  UpdateWorkspacePayload,
  UploadRules,
  Workspace,
  WorkspaceFile,
  WorkspaceFileText,
} from "../types/workspace";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/praxis";
const url = pathIn(BASE);

/** The newest files (optionally of one workspace, optionally whose name contains `search`), at most 500. */
export const listWorkspaceFiles = (workspaceId?: string, search?: string): Promise<WorkspaceFile[]> =>
  client.get<WorkspaceFile[]>(withQuery(`${BASE}/workspaces/files`, { workspaceId, search: search?.trim() }));

export const deleteWorkspaceFile = (fileId: string): Promise<void> =>
  client.delete<void>(url`/workspaces/files/${fileId}`);

export const fetchWorkspacesPaged = (page = 1, pageSize = 100): Promise<PagedResponse<Workspace>> =>
  client.get<PagedResponse<Workspace>>(withQuery(`${BASE}/workspaces`, { page, pageSize }));

export const fetchWorkspaces = async (): Promise<Workspace[]> => (await fetchWorkspacesPaged(1, 100)).items;

export const fetchWorkspace = (id: string): Promise<Workspace> => client.get<Workspace>(url`/workspaces/${id}`);

export const createWorkspace = (payload: CreateWorkspacePayload): Promise<Workspace> =>
  client.post<Workspace>(`${BASE}/workspaces`, payload);

export const updateWorkspace = (id: string, payload: UpdateWorkspacePayload): Promise<Workspace> =>
  client.put<Workspace>(url`/workspaces/${id}`, payload);

export const deleteWorkspace = (id: string): Promise<void> => client.delete<void>(url`/workspaces/${id}`);

export const fetchWorkspaceFiles = (workspaceId: string): Promise<WorkspaceFile[]> =>
  client.get<WorkspaceFile[]>(url`/workspaces/${workspaceId}/files`);

/** A page of a workspace's files, newest first, optionally only those whose name contains `search`. */
export const fetchWorkspaceFilesPage = (
  workspaceId: string,
  page = 1,
  pageSize = 50,
  search?: string,
): Promise<PagedResponse<WorkspaceFile>> =>
  client.get<PagedResponse<WorkspaceFile>>(
    withQuery(url`/workspaces/${workspaceId}/files/page`, { page, pageSize, search: search?.trim() }),
  );

export const fetchUploadRules = (): Promise<UploadRules> => client.get<UploadRules>(`${BASE}/workspaces/files/rules`);

export const reindexWorkspaceFile = (fileId: string): Promise<void> =>
  client.post<void>(url`/workspaces/files/${fileId}/reindex`, undefined);

export const fetchWorkspaceFileText = (fileId: string, maxChars = 20_000): Promise<WorkspaceFileText> =>
  client.get<WorkspaceFileText>(withQuery(url`/workspaces/files/${fileId}/text`, { maxChars }));

/** Downloads as an attachment (the server never serves uploaded files inline). */
export const workspaceFileContentUrl = (fileId: string): string =>
  BASE_URL + url`/workspaces/files/${fileId}/content`;

const uploadError = (xhr: XMLHttpRequest): ApiError => {
  let detail: string | undefined;
  try {
    const body = JSON.parse(xhr.responseText) as { detail?: string; title?: string };
    detail = body.detail ?? body.title;
  } catch {
    detail = undefined;
  }
  return new ApiError(xhr.status, detail ?? `Upload failed (${xhr.status}).`);
};

/**
 * Uploads through XMLHttpRequest rather than fetch so the caller can show progress. Without a workspace the server
 * puts the file in the user's "Chat uploads" workspace.
 */
export const uploadWorkspaceFile = (
  file: File,
  workspaceId?: string | null,
  onProgress?: (fraction: number) => void,
): Promise<WorkspaceFile> =>
  new Promise((resolve, reject) => {
    const formData = new FormData();
    formData.append("file", file);
    if (workspaceId) {
      formData.append("workspaceId", workspaceId);
    }

    const xhr = new XMLHttpRequest();
    xhr.open("POST", `${BASE_URL}${BASE}/workspaces/files`);
    xhr.withCredentials = true;

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress?.(event.loaded / event.total);
      }
    };

    xhr.onload = () => {
      if (xhr.status === 401) {
        try {
          handleUnauthorized();
        } catch (err) {
          reject(err);
          return;
        }
      }
      if (xhr.status < 200 || xhr.status >= 300) {
        reject(uploadError(xhr));
        return;
      }
      try {
        resolve(JSON.parse(xhr.responseText) as WorkspaceFile);
      } catch {
        reject(new ApiError(xhr.status, "The server returned an unreadable response."));
      }
    };
    xhr.onerror = () => reject(new ApiError(0, "Upload failed: the server could not be reached."));
    xhr.onabort = () => reject(new ApiError(0, "Upload cancelled."));

    xhr.send(formData);
  });
