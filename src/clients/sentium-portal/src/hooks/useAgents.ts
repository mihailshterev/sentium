import { useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchAgentsPaged, createAgent, updateAgent, deleteAgent } from "../services/agentRuntime.service";
import { useInfiniteList } from "./useInfiniteList";
import type { AgentRecord, CreateAgentPayload, UpdateAgentPayload } from "../types/agents";

const AGENTS_KEY = ["agents"] as const;

const useAgents = () => {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: AGENTS_KEY });

  const list = useInfiniteList<AgentRecord>(AGENTS_KEY, fetchAgentsPaged, { pageSize: 100 });

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
};

export default useAgents;
