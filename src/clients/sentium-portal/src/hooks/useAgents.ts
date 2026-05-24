import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchAgents, createAgent, updateAgent, deleteAgent } from "../services/agentRuntime.service";
import type { CreateAgentPayload, UpdateAgentPayload } from "../types/agents";

const AGENTS_KEY = ["agents"] as const;

const useAgents = () => {
  const queryClient = useQueryClient();

  const { data: agents = [], isLoading } = useQuery({
    queryKey: AGENTS_KEY,
    queryFn: fetchAgents,
  });

  const createMutation = useMutation({
    mutationFn: (payload: CreateAgentPayload) => createAgent(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: AGENTS_KEY }),
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateAgentPayload) => updateAgent(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: AGENTS_KEY }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAgent(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: AGENTS_KEY }),
  });

  return {
    agents,
    isLoading,
    createAgent: createMutation.mutate,
    isCreatingAgent: createMutation.isPending,
    isCreateSuccess: createMutation.isSuccess,
    isCreateError: createMutation.isError,
    createAgentError: createMutation.error,
    resetCreate: createMutation.reset,
    updateAgent: updateMutation.mutate,
    isUpdatingAgent: updateMutation.isPending,
    isUpdateSuccess: updateMutation.isSuccess,
    isUpdateError: updateMutation.isError,
    updateAgentError: updateMutation.error,
    resetUpdate: updateMutation.reset,
    deleteAgent: deleteMutation.mutate,
    isDeletingAgent: deleteMutation.isPending,
  };
};

export default useAgents;
