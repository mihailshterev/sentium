import { useCallback, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { uploadWorkspaceFile } from "@/features/workspaces/api/workspaces.service";
import { workspaceFileKeys, workspaceKeys } from "@/features/workspaces/api/workspaces.queries";
import { useUploadRules } from "@/features/workspaces/hooks/useUploadRules";
import { uploadProblem } from "@/features/workspaces/utils/upload-validation";
import type { FileProcessingStatus, WorkspaceFile } from "@/features/workspaces/types/workspace";
import type { MessageAttachment } from "@/features/assistant/types/assistant";

/** A file in the composer: picked from a workspace, or being uploaded from the computer. */
export interface ComposerAttachment {
  /** Stable local key; the file id is only known once an upload finishes. */
  key: string;
  fileId: string | null;
  fileName: string;
  state: "uploading" | "ready" | "error";
  /** Upload progress, 0-1. */
  progress: number;
  processingStatus?: FileProcessingStatus;
  error?: string;
}

const fromWorkspaceFile = (file: WorkspaceFile): ComposerAttachment => ({
  key: file.id,
  fileId: file.id,
  fileName: file.fileName,
  state: "ready",
  progress: 1,
  processingStatus: file.processingStatus,
});

/**
 * The composer's attachments. Uploads go to `uploadWorkspaceId` - the conversation's workspace - or, without one, to
 * the user's "Chat uploads" workspace (the server picks it). A file can be sent as soon as its upload finishes; its
 * search indexing carries on in the background.
 */
export function useComposerAttachments(uploadWorkspaceId: string | null) {
  const queryClient = useQueryClient();
  const rules = useUploadRules();
  const [attachments, setAttachments] = useState<ComposerAttachment[]>([]);
  const [problem, setProblem] = useState<string | null>(null);

  const update = useCallback((key: string, patch: Partial<ComposerAttachment>) => {
    setAttachments((prev) => prev.map((a) => (a.key === key ? { ...a, ...patch } : a)));
  }, []);

  const toggleFile = useCallback((file: WorkspaceFile) => {
    setAttachments((prev) =>
      prev.some((a) => a.fileId === file.id) ? prev.filter((a) => a.fileId !== file.id) : [...prev, fromWorkspaceFile(file)],
    );
  }, []);

  const uploadFiles = useCallback(
    (files: readonly File[]) => {
      const problems: string[] = [];

      for (const file of files) {
        const reason = uploadProblem(file, rules);
        if (reason) {
          problems.push(reason);
          continue;
        }

        const key = crypto.randomUUID();
        setAttachments((prev) => [
          ...prev,
          { key, fileId: null, fileName: file.name, state: "uploading", progress: 0 },
        ]);

        uploadWorkspaceFile(file, uploadWorkspaceId, (progress) => update(key, { progress }))
          .then((uploaded) => {
            update(key, { fileId: uploaded.id, state: "ready", progress: 1, processingStatus: uploaded.processingStatus });
            void queryClient.invalidateQueries({ queryKey: workspaceFileKeys.all });
            void queryClient.invalidateQueries({ queryKey: workspaceKeys.all });
          })
          .catch((err: unknown) => {
            update(key, { state: "error", error: err instanceof Error ? err.message : "Upload failed." });
          });
      }

      setProblem(problems.length > 0 ? problems.join(" ") : null);
    },
    [rules, uploadWorkspaceId, update, queryClient],
  );

  const remove = useCallback((key: string) => {
    setAttachments((prev) => prev.filter((a) => a.key !== key));
  }, []);

  const clear = useCallback(() => {
    setAttachments([]);
    setProblem(null);
  }, []);

  const ready: MessageAttachment[] = attachments
    .filter((a): a is ComposerAttachment & { fileId: string } => a.state === "ready" && a.fileId !== null)
    .map((a) => ({ fileId: a.fileId, fileName: a.fileName }));

  return {
    attachments,
    ready,
    isUploading: attachments.some((a) => a.state === "uploading"),
    selectedIds: attachments.flatMap((a) => (a.fileId ? [a.fileId] : [])),
    problem,
    dismissProblem: () => setProblem(null),
    toggleFile,
    uploadFiles,
    remove,
    clear,
  };
}
