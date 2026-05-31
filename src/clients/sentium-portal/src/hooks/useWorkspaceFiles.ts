import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchWorkspaceFiles, uploadWorkspaceFile, deleteWorkspaceFile } from "../services/agentRuntime.service";
import type { WorkspaceFile } from "../types/workspace";

const hasPending = (files: WorkspaceFile[]) =>
  files.some((f) => f.processingStatus === "Pending" || f.processingStatus === "Processing");

const STATUS_POLL_MS = 4000;

const filesKey = (workspaceId: string | undefined) => ["workspaceFiles", workspaceId] as const;
const workspacesKey = ["workspaces"] as const;

const useWorkspaceFiles = (workspaceId: string | undefined) => {
  const queryClient = useQueryClient();

  const { data: files = [], isError: isFilesError } = useQuery({
    queryKey: filesKey(workspaceId),
    queryFn: () => fetchWorkspaceFiles(workspaceId!),
    enabled: !!workspaceId,
    refetchInterval: (query) => (query.state.data && hasPending(query.state.data) ? STATUS_POLL_MS : false),
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadWorkspaceFile(file, workspaceId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: filesKey(workspaceId) });
      queryClient.invalidateQueries({ queryKey: workspacesKey });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (fileId: string) => deleteWorkspaceFile(fileId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: filesKey(workspaceId) });
      queryClient.invalidateQueries({ queryKey: workspacesKey });
    },
  });

  return {
    files,
    isFilesError,
    uploadFile: uploadMutation.mutate,
    isUploading: uploadMutation.isPending,
    isUploadSuccess: uploadMutation.isSuccess,
    isUploadError: uploadMutation.isError,
    uploadError: uploadMutation.error,
    resetUpload: uploadMutation.reset,
    deleteFile: deleteMutation.mutate,
  };
};

export default useWorkspaceFiles;
