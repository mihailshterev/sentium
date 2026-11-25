import { useEffect, useMemo } from "react";
import { keepPreviousData, useInfiniteQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  fetchConversations,
  createConversation,
  deleteConversation,
  renameConversation,
  setConversationArchived,
  setConversationPinned,
} from "@/features/assistant/api/assistant.service";
import { conversationKeys } from "@/features/assistant/api/assistant.queries";
import { useConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import type { CreateConversationPayload } from "@/features/assistant/types/assistant";

const PAGE_SIZE = 20;

interface RenameArgs {
  id: string;
  title: string;
}

interface FlagArgs {
  id: string;
  value: boolean;
}

export function useConversations(search = "", archived = false) {
  const term = search.trim();
  const queryClient = useQueryClient();
  const generatedTitles = useConversationStore((s) => s.conversationTitles);
  const clearConversationTitle = useConversationStore((s) => s.clearConversationTitle);

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useInfiniteQuery({
    queryKey: conversationKeys.list(term, archived),
    queryFn: ({ pageParam }) => fetchConversations(pageParam, PAGE_SIZE, term || undefined, archived),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.page < lastPage.totalPages ? lastPage.page + 1 : undefined),
    placeholderData: keepPreviousData,
  });

  useEffect(() => {
    if (Object.keys(generatedTitles).length > 0) {
      void queryClient.invalidateQueries({ queryKey: conversationKeys.all });
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
    onSuccess: () => queryClient.invalidateQueries({ queryKey: conversationKeys.all }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteConversation(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: conversationKeys.all }),
  });

  const renameMutation = useMutation({
    mutationFn: ({ id, title }: RenameArgs) => renameConversation(id, title),
    onSuccess: (_data, { id }) => {
      clearConversationTitle(id);
      return queryClient.invalidateQueries({ queryKey: conversationKeys.all });
    },
  });

  const pinMutation = useMutation({
    mutationFn: ({ id, value }: FlagArgs) => setConversationPinned(id, value),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: conversationKeys.all }),
  });

  const archiveMutation = useMutation({
    mutationFn: ({ id, value }: FlagArgs) => setConversationArchived(id, value),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: conversationKeys.all }),
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
    setPinned: (id: string, value: boolean) => pinMutation.mutate({ id, value }),
    setArchived: (id: string, value: boolean) => archiveMutation.mutate({ id, value }),
  };
}
