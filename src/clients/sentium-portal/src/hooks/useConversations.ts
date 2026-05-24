import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchConversations, createConversation, deleteConversation } from "../services/agentRuntime.service";
import type { CreateConversationPayload } from "../types/assistant";

const CONVERSATIONS_KEY = ["conversations"] as const;

const useConversations = () => {
  const queryClient = useQueryClient();

  const { data: conversations = [] } = useQuery({
    queryKey: CONVERSATIONS_KEY,
    queryFn: fetchConversations,
  });

  const createMutation = useMutation({
    mutationFn: (payload: CreateConversationPayload) => createConversation(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CONVERSATIONS_KEY }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteConversation(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CONVERSATIONS_KEY }),
  });

  return {
    conversations,
    createConversation: createMutation.mutateAsync,
    isCreating: createMutation.isPending,
    deleteConversation: deleteMutation.mutate,
    isDeleting: deleteMutation.isPending,
  };
};

export default useConversations;
