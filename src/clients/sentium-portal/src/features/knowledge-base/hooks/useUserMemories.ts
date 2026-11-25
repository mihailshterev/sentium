import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { addMemory, deleteMemory, updateMemory } from "../api/memories.service";
import { knowledgeBaseQueries, memoryKeys } from "../api/knowledge-base.queries";

export function useUserMemories() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: memoryKeys.all });

  const query = useQuery(knowledgeBaseQueries.memories());

  const addMutation = useMutation({ mutationFn: (content: string) => addMemory(content), onSuccess: invalidate });

  const updateMutation = useMutation({
    mutationFn: ({ id, content }: { id: string; content: string }) => updateMemory(id, content),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({ mutationFn: (id: string) => deleteMemory(id), onSuccess: invalidate });

  return {
    memories: query.data ?? [],
    isLoading: query.isLoading,
    error: query.error,
    addMemory: addMutation.mutateAsync,
    isAdding: addMutation.isPending,
    addError: addMutation.error,
    updateMemory: updateMutation.mutate,
    updatingId: updateMutation.isPending ? updateMutation.variables?.id : null,
    deleteMemory: deleteMutation.mutate,
    deletingId: deleteMutation.isPending ? deleteMutation.variables : null,
    actionError: updateMutation.error ?? deleteMutation.error,
  };
}
