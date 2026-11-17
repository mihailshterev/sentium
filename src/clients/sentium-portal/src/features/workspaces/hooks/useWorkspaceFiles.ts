import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  fetchWorkspaceFiles,
  uploadWorkspaceFile,
  deleteWorkspaceFile,
  reindexWorkspaceFile,
} from "@/features/workspaces/api/workspaces.service";
import type { WorkspaceFile } from "@/features/workspaces/types/workspace";

const hasPending = (files: WorkspaceFile[]) =>
  files.some((f) => f.processingStatus === "Pending" || f.processingStatus === "Processing");

const STATUS_POLL_MS = 4000;

const filesKey = (workspaceId: string | undefined) => ["workspaceFiles", workspaceId] as const;
const workspacesKey = ["workspaces"] as const;

export function useWorkspaceFiles(workspaceId: string | undefined) {
  const queryClient = useQueryClient();
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: filesKey(workspaceId) });
    void queryClient.invalidateQueries({ queryKey: workspacesKey });
  };

  const {
    data: files = [],
    isLoading: isFilesLoading,
    isError: isFilesError,
  } = useQuery({
    queryKey: filesKey(workspaceId),
    queryFn: () => fetchWorkspaceFiles(workspaceId!),
    enabled: !!workspaceId,
    refetchInterval: (query) => (query.state.data && hasPending(query.state.data) ? STATUS_POLL_MS : false),
  });

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
    files,
    isFilesLoading,
    isFilesError,
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
