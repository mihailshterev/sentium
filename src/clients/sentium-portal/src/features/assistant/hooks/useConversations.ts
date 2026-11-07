import { useEffect, useMemo } from "react";
import { keepPreviousData, useInfiniteQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  fetchConversations,
  createConversation,
  deleteConversation,
  renameConversation,
} from "@/features/assistant/api/assistant.service";
import { useConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import type { CreateConversationPayload } from "@/features/assistant/types/assistant";

const CONVERSATIONS_KEY = ["conversations"] as const;
const PAGE_SIZE = 20;

interface RenameArgs {
  id: string;
  title: string;
}

export function useConversations(search = "") {
  const term = search.trim();
  const queryClient = useQueryClient();
  const generatedTitles = useConversationStore((s) => s.conversationTitles);
  const clearConversationTitle = useConversationStore((s) => s.clearConversationTitle);

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useInfiniteQuery({
    queryKey: [...CONVERSATIONS_KEY, term],
    queryFn: ({ pageParam }) => fetchConversations(pageParam, PAGE_SIZE, term || undefined),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.page < lastPage.totalPages ? lastPage.page + 1 : undefined),
    placeholderData: keepPreviousData,
  });

  useEffect(() => {
    if (Object.keys(generatedTitles).length > 0) {
      void queryClient.invalidateQueries({ queryKey: CONVERSATIONS_KEY });
    }
  }, [generatedTitles, queryClient]);

  const conversations = useMemo(
    () =>
      (data?.pages.flatMap((page) => page.items) ?? []).map((conv) =>
        generatedTitles[conv.id] ? { ...conv, title: generatedTitles[conv.id] } : conv,
      ),
    [data, generatedTitles],
  );

  const createMutation = useMutation({
    mutationFn: (payload: CreateConversationPayload) => createConversation(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CONVERSATIONS_KEY }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteConversation(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CONVERSATIONS_KEY }),
  });

  const renameMutation = useMutation({
    mutationFn: ({ id, title }: RenameArgs) => renameConversation(id, title),
    onSuccess: (_data, { id }) => {
      clearConversationTitle(id);
      return queryClient.invalidateQueries({ queryKey: CONVERSATIONS_KEY });
    },
  });

  return {
    conversations,
    totalCount: data?.pages[0]?.totalCount ?? 0,
    isLoading,
    hasMore: hasNextPage,
    loadMore: () => void fetchNextPage(),
    isLoadingMore: isFetchingNextPage,
    createConversation: createMutation.mutateAsync,
    isCreating: createMutation.isPending,
    deleteConversation: deleteMutation.mutate,
    isDeleting: deleteMutation.isPending,
    renameConversation: renameMutation.mutate,
  };
}
