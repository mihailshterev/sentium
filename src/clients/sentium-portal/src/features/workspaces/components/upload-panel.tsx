import { UploadCloud } from "lucide-react";
import styles from "../workspaces.module.css";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { StatusMessage } from "@/shared/ui/status-message";
import { formatBytesToMb } from "@/shared/utils/formatters";
import type { UploadRules } from "@/features/workspaces/types/workspace";

interface UploadPanelProps {
  workspaceName: string;
  selectedFiles: readonly File[];
  dragActive: boolean;
  /** Why some of the chosen files were not selected. */
  problems: readonly string[];
  isUploading: boolean;
  /** Overall progress of the running upload, 0-1. */
  uploadProgress: number | null;
  isUploadSuccess: boolean;
  isUploadError: boolean;
  uploadError: Error | null;
  rules: UploadRules | null;
  onDrop: (e: React.DragEvent) => void;
  onDragOver: (e: React.DragEvent) => void;
  onDragLeave: () => void;
  onInputChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onSubmit: (e: React.FormEvent) => void;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
}

const TYPE_HINT = "Text, code, PDF and Word files";

export function UploadPanel({
  workspaceName,
  selectedFiles,
  dragActive,
  problems,
  isUploading,
  uploadProgress,
  isUploadSuccess,
  isUploadError,
  uploadError,
  rules,
  onDrop,
  onDragOver,
  onDragLeave,
  onInputChange,
  onSubmit,
  fileInputRef,
}: UploadPanelProps) {
  const totalBytes = selectedFiles.reduce((sum, f) => sum + f.size, 0);
  const limit = rules ? ` up to ${formatBytesToMb(rules.maxFileSizeBytes)} each` : "";

  return (
    <Card as="section" padding="md" className={styles.uploadCard}>
      <form className={styles.uploadForm} onSubmit={onSubmit}>
        <CardHeader
          icon={<UploadCloud />}
          title="Upload Files"
          description="Agents can read files right away; search indexing runs in the background"
        />
        <div
          className={styles.dropZone}
          data-active={dragActive || undefined}
          onClick={() => fileInputRef.current?.click()}
          onDrop={onDrop}
          onDragOver={onDragOver}
          onDragLeave={onDragLeave}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && fileInputRef.current?.click()}
        >
          <span className={styles.dropIcon} aria-hidden="true">
            <UploadCloud size={22} />
          </span>
          {selectedFiles.length === 1 ? (
            <>
              <span className={styles.dropText}>{selectedFiles[0].name}</span>
              <span className={styles.dropHint}>{formatBytesToMb(selectedFiles[0].size)}</span>
            </>
          ) : selectedFiles.length > 1 ? (
            <>
              <span className={styles.dropText}>{selectedFiles.length} files selected</span>
              <span className={styles.dropHint}>{formatBytesToMb(totalBytes)} in total</span>
            </>
          ) : (
            <>
              <span className={styles.dropText}>Drop files or click to browse</span>
              <span className={styles.dropHint}>
                {TYPE_HINT}
                {limit}
              </span>
            </>
          )}
        </div>
        <input
          ref={fileInputRef}
          type="file"
          multiple
          hidden
          onChange={onInputChange}
          accept={rules?.extensions.join(",")}
        />

        {problems.map((p) => (
          <StatusMessage key={p} variant="error" message={p} />
        ))}
        {isUploading && (
          <StatusMessage
            variant="loading"
            message={uploadProgress === null ? "Uploading…" : `Uploading… ${Math.round(uploadProgress * 100)}%`}
          />
        )}
        {isUploadSuccess && <StatusMessage variant="success" message="Uploaded - indexing in background." />}
        {isUploadError && (
          <StatusMessage
            variant="error"
            message={uploadError instanceof Error ? uploadError.message : "Upload failed."}
          />
        )}

        <Button
          type="submit"
          variant="primary"
          fullWidth
          icon={<UploadCloud size={16} />}
          loading={isUploading}
          disabled={selectedFiles.length === 0}
        >
          Upload to {workspaceName}
        </Button>
      </form>
    </Card>
  );
}
