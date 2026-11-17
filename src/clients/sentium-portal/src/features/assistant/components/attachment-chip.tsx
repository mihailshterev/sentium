import { AlertTriangle, FileText, Loader2, X } from "lucide-react";
import styles from "../assistant.module.css";
import type { ComposerAttachment } from "../hooks/useComposerAttachments";

interface AttachmentChipProps {
  attachment: ComposerAttachment;
  onRemove: (key: string) => void;
}

const statusLabel = (a: ComposerAttachment): string | null => {
  if (a.state === "uploading") {
    return `${Math.round(a.progress * 100)}%`;
  }
  if (a.state === "error") {
    return "failed";
  }
  if (a.processingStatus === "Pending" || a.processingStatus === "Processing") {
    return "indexing";
  }
  return null;
};

/** A file in the composer with its upload / indexing state. */
export function AttachmentChip({ attachment, onRemove }: AttachmentChipProps) {
  const label = statusLabel(attachment);
  const busy = attachment.state === "uploading";

  return (
    <span
      className={styles.contextPill}
      data-state={attachment.state}
      title={attachment.error ?? attachment.fileName}
      data-testid="composer-attachment"
    >
      {busy ? (
        <Loader2 size={12} className="animate-spin" aria-hidden="true" />
      ) : attachment.state === "error" ? (
        <AlertTriangle size={12} aria-hidden="true" />
      ) : (
        <FileText size={12} aria-hidden="true" />
      )}
      <span className={styles.contextPillName}>{attachment.fileName}</span>
      {label && <span className={styles.contextPillStatus}>{label}</span>}
      <button type="button" onClick={() => onRemove(attachment.key)} aria-label={`Remove ${attachment.fileName}`}>
        <X size={12} />
      </button>
    </span>
  );
}
