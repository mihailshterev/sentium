import { FolderOpen, File as FileIcon, Pencil, Trash2 } from "lucide-react";
import styles from "../workspaces.module.scss";
import type { Workspace } from "../../../types/workspace";
import { formatDateTimeShort } from "../../../utils/formatters";

interface WorkspaceCardProps {
  workspace: Workspace;
  onOpen: () => void;
  onEdit: (e: React.MouseEvent) => void;
  onDelete: (e: React.MouseEvent) => void;
}

const WorkspaceCard = ({ workspace: ws, onOpen, onEdit, onDelete }: WorkspaceCardProps) => (
  <div className={styles.wsCard} onClick={onOpen}>
    <div className={styles.wsCardTop}>
      <div className={styles.wsCardIconBox}>
        <FolderOpen size={22} />
      </div>
      <div className={styles.wsCardActions}>
        <button
          className={styles.wsCardActionBtn}
          title="Edit workspace"
          onClick={onEdit}
          data-testid={`workspace-edit-${ws.name}`}
        >
          <Pencil size={12} />
        </button>
        <button
          className={`${styles.wsCardActionBtn} ${styles.wsCardActionBtnDanger}`}
          title="Delete workspace"
          onClick={onDelete}
          data-testid={`workspace-delete-${ws.name}`}
        >
          <Trash2 size={12} />
        </button>
      </div>
    </div>

    <div className={styles.wsCardBody}>
      <h3 className={styles.wsCardName}>{ws.name}</h3>
      <p className={styles.wsCardDesc}>
        {ws.description || <span style={{ fontStyle: "italic", opacity: 0.6 }}>No description</span>}
      </p>
    </div>

    <div className={styles.wsCardFooter}>
      <span className={styles.wsFileBadge}>
        <FileIcon size={11} />
        {ws.fileCount} file{ws.fileCount !== 1 ? "s" : ""}
      </span>
      <span className={styles.wsCardDate}>{formatDateTimeShort(ws.updatedAt)}</span>
    </div>
  </div>
);

export default WorkspaceCard;
