import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteAgentLearning,
  fetchAgentLearnings,
  fetchAgentLearningStats,
  updateAgentLearning,
} from "../services/agentRuntime.service";
import { useInfiniteList } from "./useInfiniteList";
import type { AgentLearning } from "../types/agentConfig";

const STATS_KEY = ["agent-learnings", "stats"] as const;

export const useAgentLearnings = (agentName?: string, pageSize = 20) => {
  const qc = useQueryClient();

  const list = useInfiniteList<AgentLearning>(
    ["agent-learnings", agentName ?? "all"],
    (page, ps) => fetchAgentLearnings(agentName, page, ps),
    { pageSize, staleTime: 15_000 },
  );

  const statsQuery = useQuery({
    queryKey: STATS_KEY,
    queryFn: fetchAgentLearningStats,
    staleTime: 15_000,
    retry: 1,
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, content, tags }: { id: string; content: string; tags: string }) =>
      updateAgentLearning(id, { content, tags }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["agent-learnings"] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAgentLearning(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["agent-learnings"] });
    },
  });

  return {
    learnings: list.items,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
    isLoading: list.isLoading,
    isFetching: list.isFetching,
    error: list.error,
    stats: statsQuery.data,
    isStatsLoading: statsQuery.isLoading,
    updateLearning: updateMutation.mutate,
    isUpdating: updateMutation.isPending,
    updatingId: updateMutation.isPending ? (updateMutation.variables as { id: string } | undefined)?.id : null,
    deleteLearning: deleteMutation.mutate,
    isDeleting: deleteMutation.isPending,
  };
};
