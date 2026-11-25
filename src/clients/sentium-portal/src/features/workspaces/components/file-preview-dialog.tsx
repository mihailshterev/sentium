import { useQuery } from "@tanstack/react-query";
import { Download, FileText, Loader2 } from "lucide-react";
import { Dialog } from "@/shared/ui/dialog";
import { StatusMessage } from "@/shared/ui/status-message";
import { workspaceFileContentUrl } from "@/features/workspaces/api/workspaces.service";
import { workspaceQueries } from "@/features/workspaces/api/workspaces.queries";
import styles from "./file-preview-dialog.module.css";

interface FilePreviewDialogProps {
  /** The file to preview; null keeps the dialog closed. */
  file: { id: string; fileName: string } | null;
  onClose: () => void;
}

/** The file's extracted text (PDF and Word included) with a download of the original. */
export function FilePreviewDialog({ file, onClose }: FilePreviewDialogProps) {
  const { data, isLoading, error } = useQuery({
    ...workspaceQueries.fileText(file?.id ?? ""),
    enabled: !!file,
  });

  return (
    <Dialog
      open={!!file}
      onClose={onClose}
      title={file?.fileName ?? ""}
      icon={<FileText size={18} />}
      size="lg"
      footer={
        file && (
          <a className={styles.download} href={workspaceFileContentUrl(file.id)} download={file.fileName}>
            <Download size={14} aria-hidden="true" />
            Download original
          </a>
        )
      }
    >
      {isLoading && (
        <div className={styles.loading} role="status">
          <Loader2 size={16} className="animate-spin" aria-hidden="true" />
          <span>Loading preview…</span>
        </div>
      )}
      {error && (
        <StatusMessage
          variant="error"
          message={error instanceof Error ? error.message : "The preview could not be loaded."}
        />
      )}
      {data && (
        <>
          {data.text.trim().length === 0 ? (
            <p className={styles.empty}>This file has no text content.</p>
          ) : (
            <pre className={styles.text} data-testid="file-preview-text">
              {data.text}
            </pre>
          )}
          {data.truncated && (
            <p className={styles.note}>
              Showing the first {data.text.length.toLocaleString()} of {data.totalChars.toLocaleString()} characters.
            </p>
          )}
        </>
      )}
    </Dialog>
  );
}
