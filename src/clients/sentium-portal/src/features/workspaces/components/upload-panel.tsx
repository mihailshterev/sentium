import { UploadCloud } from "lucide-react";
import styles from "../workspaces.module.css";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { StatusMessage } from "@/shared/ui/status-message";
import { formatBytesToMb } from "../../../shared/utils/formatters";

interface UploadPanelProps {
  workspaceName: string;
  selectedFile: File | null;
  dragActive: boolean;
  fileTypeError: string | null;
  isUploading: boolean;
  isUploadSuccess: boolean;
  isUploadError: boolean;
  uploadError: Error | null;
  acceptedExtensions: string;
  onDrop: (e: React.DragEvent) => void;
  onDragOver: (e: React.DragEvent) => void;
  onDragLeave: () => void;
  onInputChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onSubmit: (e: React.FormEvent) => void;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
}

export function UploadPanel({
  workspaceName,
  selectedFile,
  dragActive,
  fileTypeError,
  isUploading,
  isUploadSuccess,
  isUploadError,
  uploadError,
  acceptedExtensions,
  onDrop,
  onDragOver,
  onDragLeave,
  onInputChange,
  onSubmit,
  fileInputRef,
}: UploadPanelProps) {
  return (
    <Card as="section" padding="md" className={styles.uploadCard}>
      <form className={styles.uploadForm} onSubmit={onSubmit}>
        <CardHeader icon={<UploadCloud />} title="Upload File" description="Files are indexed for RAG automatically" />
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
          {selectedFile ? (
            <>
              <span className={styles.dropText}>{selectedFile.name}</span>
              <span className={styles.dropHint}>{formatBytesToMb(selectedFile.size)}</span>
            </>
          ) : (
            <>
              <span className={styles.dropText}>Drop a file or click to browse</span>
              <span className={styles.dropHint}>Text files up to 100 MB — .txt .md .json .csv .yaml .py .ts …</span>
            </>
          )}
        </div>
        <input ref={fileInputRef} type="file" hidden onChange={onInputChange} accept={acceptedExtensions} />

        {fileTypeError && <StatusMessage variant="error" message={fileTypeError} />}
        {isUploading && <StatusMessage variant="loading" message="Uploading…" />}
        {isUploadSuccess && <StatusMessage variant="success" message="File uploaded — indexing in background." />}
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
          disabled={!selectedFile}
        >
          Upload to {workspaceName}
        </Button>
      </form>
    </Card>
  );
}
