import { useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchWorkflowsPaged, createWorkflow, updateWorkflow, deleteWorkflow } from "../services/agentRuntime.service";
import { useInfiniteList } from "./useInfiniteList";
import type { WorkflowPayload, UpdateWorkflowPayload, WorkflowRecord } from "../types/workflows";

const WORKFLOWS_KEY = ["workflows"] as const;

const useWorkflows = () => {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: WORKFLOWS_KEY });

  const list = useInfiniteList<WorkflowRecord>(WORKFLOWS_KEY, fetchWorkflowsPaged, { pageSize: 100 });

  const createMutation = useMutation({
    mutationFn: (payload: WorkflowPayload) => createWorkflow(payload),
    onSuccess: invalidate,
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateWorkflowPayload) => updateWorkflow(payload),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWorkflow(id),
    onSuccess: invalidate,
  });

  return {
    workflows: list.items,
    totalCount: list.totalCount,
    isLoading: list.isLoading,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
    createWorkflow: createMutation.mutate,
    isCreatingWorkflow: createMutation.isPending,
    isCreateSuccess: createMutation.isSuccess,
    isCreateError: createMutation.isError,
    createWorkflowError: createMutation.error,
    resetCreate: createMutation.reset,
    updateWorkflow: updateMutation.mutate,
    isUpdatingWorkflow: updateMutation.isPending,
    isUpdateSuccess: updateMutation.isSuccess,
    isUpdateError: updateMutation.isError,
    updateWorkflowError: updateMutation.error,
    resetUpdate: updateMutation.reset,
    deleteWorkflow: deleteMutation.mutate,
    isDeletingWorkflow: deleteMutation.isPending,
  };
};

export default useWorkflows;
