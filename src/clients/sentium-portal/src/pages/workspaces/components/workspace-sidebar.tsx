import { useState } from "react"; // Added useState import
import { FolderOpen, FolderPlus, ChevronRight, Pencil, Trash2 } from "lucide-react";
import styles from "../workspaces.module.scss";
import type { Workspace } from "../../../types/workspace";
import ConfirmDialog from "../../../components/ui/confirm-dialog"; // Added import

interface WorkspaceSidebarProps {
  workspaces: Workspace[];
  selectedWorkspace: Workspace | null;
  isWorkspacesError: boolean;
  onSelect: (ws: Workspace) => void;
  onEdit: (ws: Workspace) => void;
  onDelete: (id: string) => void;
  onCreateNew: () => void;
}

const WorkspaceSidebar = ({
  workspaces,
  selectedWorkspace,
  isWorkspacesError,
  onSelect,
  onEdit,
  onDelete,
  onCreateNew,
}: WorkspaceSidebarProps) => {
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const [workspaceToDelete, setWorkspaceToDelete] = useState<{ id: string; name: string } | null>(null);

  const handleOpenDeleteConfirm = (e: React.MouseEvent, ws: Workspace) => {
    e.stopPropagation();
    setWorkspaceToDelete({ id: ws.id, name: ws.name });
    setIsConfirmOpen(true);
  };

  const handleConfirmDelete = () => {
    if (!workspaceToDelete) {
      return;
    }

    onDelete(workspaceToDelete.id);
    setIsConfirmOpen(false);
    setWorkspaceToDelete(null);
  };

  const handleCancelDelete = () => {
    setIsConfirmOpen(false);
    setWorkspaceToDelete(null);
  };

  return (
    <aside className={styles.sidebar}>
      <p className={styles.sidebarTitle}>Workspaces {workspaces.length > 0 && `· ${workspaces.length}`}</p>
      {isWorkspacesError && <p className={styles.errorText}>Failed to load workspaces.</p>}
      {!isWorkspacesError && workspaces.length === 0 && (
        <div className={styles.emptyState}>
          <FolderPlus size={28} className={styles.emptyIcon} />
          <p>No workspaces yet.</p>
          <button className={styles.emptyAction} onClick={onCreateNew}>
            Create one
          </button>
        </div>
      )}
      <div className={styles.workspaceList}>
        {workspaces.map((ws) => (
          <div
            key={ws.id}
            className={`${styles.workspaceCard} ${selectedWorkspace?.id === ws.id ? styles.workspaceCardActive : ""}`}
            onClick={() => onSelect(ws)}
          >
            <div className={styles.wsCardMain}>
              <FolderOpen size={13} className={styles.wsIcon} />
              <div className={styles.wsInfo}>
                <span className={styles.wsName}>{ws.name}</span>
                {ws.description && <span className={styles.wsDesc}>{ws.description}</span>}
              </div>
              <ChevronRight size={12} className={styles.wsChevron} />
            </div>
            <div className={styles.wsCardMeta}>
              <span className={styles.wsFileCount}>
                {ws.fileCount} file{ws.fileCount !== 1 ? "s" : ""}
              </span>
              <div className={styles.wsActions}>
                <button
                  className={styles.iconBtn}
                  title="Edit workspace"
                  onClick={(e) => {
                    e.stopPropagation();
                    onEdit(ws);
                  }}
                >
                  <Pencil size={12} />
                </button>
                <button
                  className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
                  title="Delete workspace"
                  onClick={(e) => handleOpenDeleteConfirm(e, ws)}
                >
                  <Trash2 size={12} />
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Delete Workspace"
        description={`Are you sure you want to delete the workspace "${workspaceToDelete?.name || ""}"? All data files connected inside this environment cluster will be disassociated immediately.`}
        confirmLabel="Delete Workspace"
        cancelLabel="Cancel"
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
        confirmWord={workspaceToDelete?.name}
      />
    </aside>
  );
};

export default WorkspaceSidebar;
