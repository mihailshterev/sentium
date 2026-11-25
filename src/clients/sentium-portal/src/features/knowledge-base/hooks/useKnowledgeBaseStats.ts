import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteKnowledgeMapCollection } from "@/features/knowledge-base/api/knowledge-base.service";
import { knowledgeBaseKeys, knowledgeBaseQueries } from "@/features/knowledge-base/api/knowledge-base.queries";

export const useKnowledgeBaseStats = () => {
  const queryClient = useQueryClient();

  const query = useQuery({ ...knowledgeBaseQueries.collectionStats(), retry: 1 });

  const deleteMutation = useMutation({
    mutationFn: deleteKnowledgeMapCollection,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: knowledgeBaseKeys.stats() });
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
