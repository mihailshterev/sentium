import { useState } from "react";
import { ChevronRight, FolderOpen, FolderPlus, Pencil, Trash2 } from "lucide-react";
import styles from "../workspaces.module.css";
import type { Workspace } from "@/features/workspaces/types/workspace";
import { Button } from "@/shared/ui/button";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";

interface WorkspaceSidebarProps {
  workspaces: Workspace[];
  selectedWorkspace: Workspace | null;
  isWorkspacesError: boolean;
  onSelect: (ws: Workspace) => void;
  onEdit: (ws: Workspace) => void;
  onDelete: (id: string) => void;
  onCreateNew: () => void;
}

export function WorkspaceSidebar({
  workspaces,
  selectedWorkspace,
  isWorkspacesError,
  onSelect,
  onEdit,
  onDelete,
  onCreateNew,
}: WorkspaceSidebarProps) {
  const [workspaceToDelete, setWorkspaceToDelete] = useState<{ id: string; name: string } | null>(null);

  return (
    <aside className={styles.sidebar} aria-label="Workspaces">
      <p className={styles.sidebarTitle}>Workspaces {workspaces.length > 0 && `· ${workspaces.length}`}</p>
      {isWorkspacesError && <p className={styles.errorText}>Failed to load workspaces.</p>}
      {!isWorkspacesError && workspaces.length === 0 && (
        <EmptyState
          compact
          icon={<FolderPlus />}
          title="No workspaces yet."
          action={
            <Button variant="secondary" size="sm" onClick={onCreateNew}>
              Create one
            </Button>
          }
        />
      )}
      <div className={styles.sidebarList}>
        {workspaces.map((ws) => (
          <div
            key={ws.id}
            className={styles.sidebarItem}
            data-active={selectedWorkspace?.id === ws.id || undefined}
            onClick={() => onSelect(ws)}
          >
            <FolderOpen size={15} className={styles.fileIcon} aria-hidden="true" />
            <div className={styles.fileInfo}>
              <span className={styles.fileName}>{ws.name}</span>
              <span className={styles.fileMeta}>
                {ws.fileCount} file{ws.fileCount !== 1 ? "s" : ""}
                {ws.description ? ` · ${ws.description}` : ""}
              </span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              icon={<Pencil size={14} />}
              title="Edit workspace"
              onClick={(e) => {
                e.stopPropagation();
                onEdit(ws);
              }}
            />
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              icon={<Trash2 size={14} />}
              title="Delete workspace"
              className={styles.deleteButton}
              onClick={(e) => {
                e.stopPropagation();
                setWorkspaceToDelete({ id: ws.id, name: ws.name });
              }}
            />
            <ChevronRight size={14} className={styles.fileIcon} aria-hidden="true" />
          </div>
        ))}
      </div>

      <ConfirmDialog
        open={workspaceToDelete !== null}
        variant="danger"
        title="Delete Workspace"
        description={`Are you sure you want to delete the workspace "${workspaceToDelete?.name || ""}"? All data files connected inside this environment cluster will be disassociated immediately.`}
        confirmLabel="Delete Workspace"
        cancelLabel="Cancel"
        onConfirm={() => {
          if (workspaceToDelete) {
            onDelete(workspaceToDelete.id);
          }
          setWorkspaceToDelete(null);
        }}
        onCancel={() => setWorkspaceToDelete(null)}
        confirmWord={workspaceToDelete?.name}
      />
    </aside>
  );
}
