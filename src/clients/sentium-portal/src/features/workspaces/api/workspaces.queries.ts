import { queryOptions } from "@tanstack/react-query";
import {
  fetchUploadRules,
  fetchWorkspace,
  fetchWorkspaceFiles,
  fetchWorkspaceFileText,
  fetchWorkspaces,
  listWorkspaceFiles,
} from "./workspaces.service";

export const workspaceKeys = {
  all: ["workspaces"] as const,
  list: () => [...workspaceKeys.all, "list"] as const,
  /** Every workspace at once, for pickers. */
  options: () => [...workspaceKeys.all, "options"] as const,
  detail: (id: string) => [...workspaceKeys.all, "detail", id] as const,
};

/** Invalidating `ofWorkspace(id)` refreshes that workspace's picker list and its paged list together. */
export const workspaceFileKeys = {
  all: ["workspaceFiles"] as const,
  search: (term: string) => [...workspaceFileKeys.all, "all", term] as const,
  ofWorkspace: (workspaceId: string | undefined) => [...workspaceFileKeys.all, workspaceId] as const,
  page: (workspaceId: string | undefined, term: string) =>
    [...workspaceFileKeys.ofWorkspace(workspaceId), "page", term] as const,
  text: (fileId: string) => ["workspaceFileText", fileId] as const,
  uploadRules: () => ["workspaceUploadRules"] as const,
};

export const workspaceQueries = {
  options: () => queryOptions({ queryKey: workspaceKeys.options(), queryFn: () => fetchWorkspaces() }),
  detail: (id: string) => queryOptions({ queryKey: workspaceKeys.detail(id), queryFn: () => fetchWorkspace(id) }),
  /** Searched on the server, so files beyond the newest few hundred are found too. */
  searchFiles: (term: string) =>
    queryOptions({ queryKey: workspaceFileKeys.search(term), queryFn: () => listWorkspaceFiles(undefined, term) }),
  files: (workspaceId: string) =>
    queryOptions({
      queryKey: workspaceFileKeys.ofWorkspace(workspaceId),
      queryFn: () => fetchWorkspaceFiles(workspaceId),
    }),
  fileText: (fileId: string) =>
    queryOptions({
      queryKey: workspaceFileKeys.text(fileId),
      queryFn: () => fetchWorkspaceFileText(fileId),
      staleTime: 60_000,
    }),
  /** Only change with a deployment, so they are fetched once per session. */
  uploadRules: () =>
    queryOptions({
      queryKey: workspaceFileKeys.uploadRules(),
      queryFn: () => fetchUploadRules(),
      staleTime: Infinity,
      gcTime: Infinity,
    }),
};
