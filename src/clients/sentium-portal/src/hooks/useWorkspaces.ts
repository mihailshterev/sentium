import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  fetchWorkspacesPaged,
  createWorkspace,
  updateWorkspace,
  deleteWorkspace,
} from "../services/agentRuntime.service";
import { useInfiniteList } from "./useInfiniteList";
import type { Workspace, CreateWorkspacePayload, UpdateWorkspacePayload } from "../types/workspace";

const WORKSPACES_KEY = ["workspaces"] as const;

const useWorkspaces = () => {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: WORKSPACES_KEY });

  const list = useInfiniteList<Workspace>(WORKSPACES_KEY, fetchWorkspacesPaged, { pageSize: 100 });

  const createMutation = useMutation({
    mutationFn: (payload: CreateWorkspacePayload) => createWorkspace(payload),
    onSuccess: invalidate,
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, ...payload }: { id: string } & UpdateWorkspacePayload) => updateWorkspace(id, payload),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWorkspace(id),
    onSuccess: invalidate,
  });

  return {
    workspaces: list.items,
    totalCount: list.totalCount,
    isLoading: list.isLoading,
    isError: !!list.error,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
    createWorkspace: createMutation.mutate,
    isCreatingWorkspace: createMutation.isPending,
    updateWorkspace: updateMutation.mutate,
    isUpdatingWorkspace: updateMutation.isPending,
    deleteWorkspace: deleteMutation.mutate,
  };
};

export default useWorkspaces;
