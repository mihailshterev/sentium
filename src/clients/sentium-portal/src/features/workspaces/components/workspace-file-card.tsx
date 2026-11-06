import { useState } from "react";
import { FileText, Trash2 } from "lucide-react";
import styles from "../workspaces.module.css";
import type { WorkspaceFile } from "@/features/workspaces/types/workspace";
import { Button } from "@/shared/ui/button";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { formatBytesToMb, formatDateTimeShort } from "../../../shared/utils/formatters";
import { FileStatusBadge } from "./file-status-badge";

interface WorkspaceFileCardProps {
  file: WorkspaceFile;
  onDelete: (id: string) => void;
}

export function WorkspaceFileCard({ file, onDelete }: WorkspaceFileCardProps) {
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  return (
    <div className={styles.fileRow}>
      <FileText size={16} className={styles.fileIcon} aria-hidden="true" />
      <div className={styles.fileInfo}>
        <span className={styles.fileName}>{file.fileName}</span>
        <span className={styles.fileMeta}>
          {formatBytesToMb(file.sizeBytes)} · {formatDateTimeShort(file.createdAt)}
        </span>
      </div>
      <FileStatusBadge status={file.processingStatus} />
      <Button
        variant="ghost"
        size="sm"
        iconOnly
        icon={<Trash2 size={15} />}
        title="Delete file"
        onClick={() => setIsConfirmOpen(true)}
        className={styles.deleteButton}
      />

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Remove File from Workspace"
        description={`Are you sure you want to permanently remove "${file.fileName}"? This will delete the raw document and remove its contents from the indexed search vector store.`}
        confirmLabel="Remove File"
        cancelLabel="Cancel"
        onConfirm={() => {
          setIsConfirmOpen(false);
          onDelete(file.id);
        }}
        onCancel={() => setIsConfirmOpen(false)}
      />
    </div>
  );
}
