import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  fetchWorkflows,
  createWorkflow,
  updateWorkflow,
  deleteWorkflow,
  type WorkflowPayload,
  type UpdateWorkflowPayload,
} from "../services/agentRuntime.service";

const WORKFLOWS_KEY = ["workflows"] as const;

const useWorkflows = () => {
  const queryClient = useQueryClient();

  const { data: workflows = [], isLoading } = useQuery({
    queryKey: WORKFLOWS_KEY,
    queryFn: fetchWorkflows,
  });

  const createMutation = useMutation({
    mutationFn: (payload: WorkflowPayload) => createWorkflow(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WORKFLOWS_KEY }),
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateWorkflowPayload) => updateWorkflow(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WORKFLOWS_KEY }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWorkflow(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WORKFLOWS_KEY }),
  });

  return {
    workflows,
    isLoading,
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
