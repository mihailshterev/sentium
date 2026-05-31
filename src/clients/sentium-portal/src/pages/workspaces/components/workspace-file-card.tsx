import { useState } from "react";
import { FileText, Trash2 } from "lucide-react";
import styles from "../workspaces.module.scss";
import type { WorkspaceFile } from "../../../types/workspace";
import { formatBytesToMb, formatDateTimeShort } from "../../../utils/formatters";
import ConfirmDialog from "../../../components/ui/confirm-dialog";

const statusClass = (status: WorkspaceFile["processingStatus"]) => {
  switch (status) {
    case "Pending":
      return styles.pending;
    case "Processing":
      return styles.processing;
    case "Completed":
      return styles.completed;
    case "Failed":
      return styles.failed;
  }
};

interface WorkspaceFileCardProps {
  file: WorkspaceFile;
  onDelete: (id: string) => void;
}

const WorkspaceFileCard = ({ file, onDelete }: WorkspaceFileCardProps) => {
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  const handleConfirmDelete = () => {
    setIsConfirmOpen(false);
    onDelete(file.id);
  };

  return (
    <div className={styles.fileCard}>
      <FileText size={14} className={styles.fileIcon} />
      <div className={styles.fileInfo}>
        <span className={styles.fileName}>{file.fileName}</span>
        <p className={styles.fileMeta}>
          {formatBytesToMb(file.sizeBytes)} · {formatDateTimeShort(file.createdAt)}
        </p>
      </div>
      <span className={`${styles.statusBadge} ${statusClass(file.processingStatus)}`}>{file.processingStatus}</span>
      <button
        className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
        title="Delete file"
        onClick={() => setIsConfirmOpen(true)}
      >
        <Trash2 size={12} />
      </button>

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Remove File from Workspace"
        description={`Are you sure you want to permanently remove "${file.fileName}"? This will delete the raw document and remove its contents from the indexed search vector store.`}
        confirmLabel="Remove File"
        cancelLabel="Cancel"
        onConfirm={handleConfirmDelete}
        onCancel={() => setIsConfirmOpen(false)}
      />
    </div>
  );
};

export default WorkspaceFileCard;
