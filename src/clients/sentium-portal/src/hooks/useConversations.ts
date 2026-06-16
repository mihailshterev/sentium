import { useInfiniteQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchConversations, createConversation, deleteConversation } from "../services/agentRuntime.service";
import type { CreateConversationPayload } from "../types/assistant";

const CONVERSATIONS_KEY = ["conversations"] as const;
const PAGE_SIZE = 20;

const useConversations = () => {
  const queryClient = useQueryClient();

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useInfiniteQuery({
    queryKey: CONVERSATIONS_KEY,
    queryFn: ({ pageParam }) => fetchConversations(pageParam, PAGE_SIZE),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.page < lastPage.totalPages ? lastPage.page + 1 : undefined),
  });

  const conversations = data?.pages.flatMap((page) => page.items) ?? [];

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
    isLoading,
    hasMore: hasNextPage,
    loadMore: () => void fetchNextPage(),
    isLoadingMore: isFetchingNextPage,
    createConversation: createMutation.mutateAsync,
    isCreating: createMutation.isPending,
    deleteConversation: deleteMutation.mutate,
    isDeleting: deleteMutation.isPending,
  };
};

export default useConversations;
