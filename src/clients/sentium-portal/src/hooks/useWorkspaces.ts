import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchWorkspaces, createWorkspace, updateWorkspace, deleteWorkspace } from "../services/agentRuntime.service";
import type { CreateWorkspacePayload, UpdateWorkspacePayload } from "../types/workspace";

const WORKSPACES_KEY = ["workspaces"] as const;

const useWorkspaces = () => {
  const queryClient = useQueryClient();

  const {
    data: workspaces = [],
    isLoading,
    isError,
  } = useQuery({
    queryKey: WORKSPACES_KEY,
    queryFn: fetchWorkspaces,
  });

  const createMutation = useMutation({
    mutationFn: (payload: CreateWorkspacePayload) => createWorkspace(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WORKSPACES_KEY }),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, ...payload }: { id: string } & UpdateWorkspacePayload) => updateWorkspace(id, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WORKSPACES_KEY }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWorkspace(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WORKSPACES_KEY }),
  });

  return {
    workspaces,
    isLoading,
    isError,
    createWorkspace: createMutation.mutate,
    isCreatingWorkspace: createMutation.isPending,
    updateWorkspace: updateMutation.mutate,
    isUpdatingWorkspace: updateMutation.isPending,
    deleteWorkspace: deleteMutation.mutate,
  };
};

export default useWorkspaces;
