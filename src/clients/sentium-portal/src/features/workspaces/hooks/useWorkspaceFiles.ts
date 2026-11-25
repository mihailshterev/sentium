import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  fetchWorkspaceFilesPage,
  uploadWorkspaceFile,
  deleteWorkspaceFile,
  reindexWorkspaceFile,
} from "@/features/workspaces/api/workspaces.service";
import { workspaceFileKeys, workspaceKeys } from "@/features/workspaces/api/workspaces.queries";
import type { WorkspaceFile } from "@/features/workspaces/types/workspace";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

const hasPending = (files: WorkspaceFile[]) =>
  files.some((f) => f.processingStatus === "Pending" || f.processingStatus === "Processing");

const PAGE_SIZE = 50;

/** A workspace's files a page at a time, newest first, optionally only those whose name contains `search`. */
export function useWorkspaceFiles(workspaceId: string | undefined, search = "") {
  const queryClient = useQueryClient();
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: workspaceFileKeys.ofWorkspace(workspaceId) });
    void queryClient.invalidateQueries({ queryKey: workspaceKeys.all });
  };

  // Ingestion reports each status change; the poll while files are pending only covers a missed event.
  useRealtimeInvalidate(REALTIME_TOPICS.workspaceFiles, [workspaceFileKeys.all, workspaceKeys.all]);

  const term = search.trim();
  const list = useInfiniteList<WorkspaceFile>(
    workspaceFileKeys.page(workspaceId, term),
    (page, pageSize) => fetchWorkspaceFilesPage(workspaceId!, page, pageSize, term || undefined),
    {
      pageSize: PAGE_SIZE,
      enabled: !!workspaceId,
      keepPrevious: true,
      refetchInterval: (items) => (hasPending(items) ? REALTIME_FALLBACK_POLL_MS : false),
    },
  );

  // Files go up one at a time so progress is meaningful; the ones before a failure stay uploaded.
  const uploadMutation = useMutation({
    mutationFn: async (selected: readonly File[]) => {
      const uploaded: WorkspaceFile[] = [];
      for (const [index, file] of selected.entries()) {
        const onProgress = (fraction: number) => setUploadProgress((index + fraction) / selected.length);
        uploaded.push(await uploadWorkspaceFile(file, workspaceId, onProgress));
      }
      return uploaded;
    },
    onSettled: () => {
      setUploadProgress(null);
      invalidate();
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (fileId: string) => deleteWorkspaceFile(fileId),
    onSuccess: invalidate,
  });

  const reindexMutation = useMutation({
    mutationFn: (fileId: string) => reindexWorkspaceFile(fileId),
    onSuccess: invalidate,
  });

  return {
    files: list.items,
    totalCount: list.totalCount,
    hasMoreFiles: !!list.hasMore,
    loadMoreFiles: () => void list.loadMore(),
    isLoadingMoreFiles: list.isLoadingMore,
    isFilesLoading: list.isLoading,
    isFilesError: !!list.error,
    uploadFiles: uploadMutation.mutate,
    isUploading: uploadMutation.isPending,
    uploadProgress,
    isUploadSuccess: uploadMutation.isSuccess,
    isUploadError: uploadMutation.isError,
    uploadError: uploadMutation.error,
    resetUpload: uploadMutation.reset,
    deleteFile: deleteMutation.mutate,
    reindexFile: reindexMutation.mutate,
    reindexingFileId: reindexMutation.isPending ? reindexMutation.variables : undefined,
  };
}
