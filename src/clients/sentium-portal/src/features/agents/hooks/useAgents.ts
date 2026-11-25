import { useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchAgentsPaged, createAgent, updateAgent, deleteAgent } from "@/features/agents/api/agents.service";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import { agentKeys } from "@/features/agents/api/agents.queries";
import type { AgentRecord, CreateAgentPayload, UpdateAgentPayload } from "@/features/agents/types/agents";

export function useAgents() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: agentKeys.all });

  const list = useInfiniteList<AgentRecord>(agentKeys.list(), fetchAgentsPaged, { pageSize: 100 });

  const createMutation = useMutation({
    mutationFn: (payload: CreateAgentPayload) => createAgent(payload),
    onSuccess: invalidate,
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateAgentPayload) => updateAgent(payload),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAgent(id),
    onSuccess: invalidate,
  });

  return {
    agents: list.items,
    totalCount: list.totalCount,
    isLoading: list.isLoading,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
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
}
