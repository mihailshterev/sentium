import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteAgentLearning,
  fetchAgentLearnings,
  updateAgentLearning,
} from "@/features/knowledge-base/api/knowledge-base.service";
import { knowledgeBaseQueries, learningKeys } from "@/features/knowledge-base/api/knowledge-base.queries";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import type { AgentLearning } from "@/features/knowledge-base/types/agent-learning";

export const useAgentLearnings = (agentName?: string, pageSize = 20) => {
  const qc = useQueryClient();

  const list = useInfiniteList<AgentLearning>(
    learningKeys.list(agentName),
    (page, ps) => fetchAgentLearnings(agentName, page, ps),
    { pageSize, staleTime: 15_000 },
  );

  const statsQuery = useQuery({ ...knowledgeBaseQueries.learningStats(), retry: 1 });

  const updateMutation = useMutation({
    mutationFn: ({ id, content, tags }: { id: string; content: string; tags: string }) =>
      updateAgentLearning(id, { content, tags }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: learningKeys.all });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAgentLearning(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: learningKeys.all });
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
