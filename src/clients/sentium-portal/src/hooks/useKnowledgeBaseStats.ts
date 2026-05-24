import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteKnowledgeMapCollection, fetchKnowledgeBaseStats } from "../services/agentRuntime.service";

const KB_STATS_KEY = ["knowledge-base-stats"] as const;

export const useKnowledgeBaseStats = () => {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: KB_STATS_KEY,
    queryFn: fetchKnowledgeBaseStats,
    staleTime: 30_000,
    retry: 1,
  });

  const deleteMutation = useMutation({
    mutationFn: deleteKnowledgeMapCollection,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KB_STATS_KEY });
    },
  });

  return {
    collections: query.data ?? [],
    isLoading: query.isLoading,
    error: query.error,
    refetch: query.refetch,
    deleteCollection: deleteMutation.mutate,
    isDeleting: deleteMutation.isPending,
  };
};
