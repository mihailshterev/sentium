import { useRef, useState } from "react";
import { Download, FileText, Image as ImageIcon, Paperclip, Trash2, UploadCloud } from "lucide-react";
import styles from "../inventory.module.css";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { Dialog } from "@/shared/ui/dialog";
import { Select } from "@/shared/ui/form-controls";
import { StatusMessage } from "@/shared/ui/status-message";
import { formatBytesToMb } from "@/shared/utils/formatters";
import { downloadUrl } from "../api/inventory.service";
import { LIMITS } from "../schemas/inventory.form";
import type { Attachment, AttachmentKind } from "../types/inventory";

export const ACCEPTED_TYPES =
  "image/jpeg,image/png,image/webp,image/gif,image/heic,image/heif,application/pdf,.txt,.md,.csv,.log";

const KINDS: AttachmentKind[] = ["Photo", "Manual", "Receipt", "Other"];

const guessKind = (file: File): AttachmentKind =>
  file.type.startsWith("image/") ? "Photo" : file.type === "application/pdf" ? "Manual" : "Other";

interface AttachmentsPanelProps {
  attachments: Attachment[];
  onUpload: (file: File, kind: AttachmentKind) => void;
  onDelete: (attachment: Attachment) => void;
  isUploading: boolean;
  uploadError?: string | null;
}

export function AttachmentsPanel({ attachments, onUpload, onDelete, isUploading, uploadError }: AttachmentsPanelProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragActive, setDragActive] = useState(false);
  const [kind, setKind] = useState<AttachmentKind | "auto">("auto");
  const [localError, setLocalError] = useState<string | null>(null);
  const [preview, setPreview] = useState<Attachment | null>(null);
  const [pendingDelete, setPendingDelete] = useState<Attachment | null>(null);

  const submit = (file: File | undefined) => {
    if (!file) return;
    if (file.size > LIMITS.attachmentBytes) {
      setLocalError(`"${file.name}" is larger than ${LIMITS.attachmentBytes / (1024 * 1024)} MB.`);
      return;
    }
    setLocalError(null);
    onUpload(file, kind === "auto" ? guessKind(file) : kind);
  };

  const images = attachments.filter((a) => a.isImage);
  const files = attachments.filter((a) => !a.isImage);

  return (
    <Card as="section" padding="md" className={styles.attachmentsCard}>
      <CardHeader
        icon={<Paperclip />}
        title="Attachments"
        description="Photos, manuals and receipts — never shared with assistants"
      />

      <div
        className={styles.dropZone}
        data-active={dragActive || undefined}
        role="button"
        tabIndex={0}
        aria-label="Upload an attachment"
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setDragActive(true);
        }}
        onDragLeave={() => setDragActive(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragActive(false);
          submit(e.dataTransfer.files[0]);
        }}
      >
        <UploadCloud size={20} aria-hidden="true" />
        <span className={styles.dropText}>{isUploading ? "Uploading…" : "Drop a file or click to browse"}</span>
        <span className={styles.dropHint}>Images, PDF or text · up to 25 MB</span>
      </div>
      <input
        ref={inputRef}
        type="file"
        hidden
        accept={ACCEPTED_TYPES}
        data-testid="attachment-input"
        onChange={(e) => {
          submit(e.target.files?.[0]);
          e.target.value = "";
        }}
      />
      <Select
        aria-label="Attachment type"
        controlSize="sm"
        value={kind}
        onChange={(e) => setKind(e.target.value as AttachmentKind | "auto")}
      >
        <option value="auto">Type: detect automatically</option>
        {KINDS.map((k) => (
          <option key={k} value={k}>
            Type: {k}
          </option>
        ))}
      </Select>

      {(localError || uploadError) && <StatusMessage variant="error" message={localError ?? uploadError ?? ""} />}

      {images.length > 0 && (
        <ul className={styles.thumbGrid} aria-label="Photos">
          {images.map((a) => (
            <li key={a.id} className={styles.thumb}>
              <button
                type="button"
                className={styles.thumbButton}
                onClick={() => setPreview(a)}
                aria-label={`View ${a.fileName}`}
              >
                <img src={a.contentUrl} alt={a.fileName} loading="lazy" />
              </button>
              <button
                type="button"
                className={styles.thumbDelete}
                aria-label={`Delete ${a.fileName}`}
                data-testid={`attachment-delete-${a.fileName}`}
                onClick={() => setPendingDelete(a)}
              >
                <Trash2 size={12} />
              </button>
            </li>
          ))}
        </ul>
      )}

      {files.length > 0 && (
        <ul className={styles.fileList} aria-label="Files">
          {files.map((a) => (
            <li key={a.id} className={styles.fileRow}>
              <FileText size={16} aria-hidden="true" className={styles.fileIcon} />
              <span className={styles.fileInfo}>
                <span className={styles.fileName}>{a.fileName}</span>
                <span className={styles.fileMeta}>
                  {a.kind} · {formatBytesToMb(a.sizeBytes)}
                </span>
              </span>
              <a
                className={styles.iconLink}
                href={downloadUrl(a)}
                aria-label={`Download ${a.fileName}`}
                title="Download"
              >
                <Download size={15} />
              </a>
              <Button
                variant="ghost"
                size="sm"
                iconOnly
                icon={<Trash2 size={14} />}
                aria-label={`Delete ${a.fileName}`}
                data-testid={`attachment-delete-${a.fileName}`}
                onClick={() => setPendingDelete(a)}
              />
            </li>
          ))}
        </ul>
      )}

      {attachments.length === 0 && (
        <p className={styles.muted}>
          <ImageIcon size={14} aria-hidden="true" /> No attachments yet.
        </p>
      )}

      {preview && (
        <Dialog open onClose={() => setPreview(null)} title={preview.fileName} size="lg">
          <figure className={styles.lightbox}>
            <img src={preview.contentUrl} alt={preview.fileName} />
            <figcaption>
              <a href={downloadUrl(preview)}>Download original</a> · {formatBytesToMb(preview.sizeBytes)}
            </figcaption>
          </figure>
        </Dialog>
      )}

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete attachment?"
        description={pendingDelete ? `"${pendingDelete.fileName}" will be permanently deleted.` : ""}
        confirmLabel="Delete"
        variant="danger"
        onConfirm={() => {
          if (pendingDelete) onDelete(pendingDelete);
          setPendingDelete(null);
        }}
        onCancel={() => setPendingDelete(null)}
      />
    </Card>
  );
}
