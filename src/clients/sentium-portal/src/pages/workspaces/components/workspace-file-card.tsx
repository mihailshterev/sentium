import { FileText, Trash2 } from "lucide-react";
import styles from "../workspaces.module.scss";
import type { WorkspaceFile } from "../../../types/workspace";
import { formatBytesToMb, formatDateTimeShort } from "../../../utils/formatters";

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

const WorkspaceFileCard = ({ file, onDelete }: WorkspaceFileCardProps) => (
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
      onClick={() => onDelete(file.id)}
    >
      <Trash2 size={12} />
    </button>
  </div>
);

export default WorkspaceFileCard;
