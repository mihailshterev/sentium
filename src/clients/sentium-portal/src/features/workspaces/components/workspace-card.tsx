import { File as FileIcon, FolderOpen, Pencil, Trash2 } from "lucide-react";
import styles from "../workspaces.module.css";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import type { Workspace } from "@/features/workspaces/types/workspace";
import { formatDateTimeShort } from "../../../shared/utils/formatters";

interface WorkspaceCardProps {
  workspace: Workspace;
  onOpen: () => void;
  onEdit: (e: React.MouseEvent) => void;
  onDelete: (e: React.MouseEvent) => void;
}

export function WorkspaceCard({ workspace: ws, onOpen, onEdit, onDelete }: WorkspaceCardProps) {
  return (
    <Card interactive padding="md" className={styles.wsCard} onClick={onOpen}>
      <div className={styles.cardTop}>
        <span className={styles.cardIcon} aria-hidden="true">
          <FolderOpen size={17} />
        </span>
        <div className={styles.cardActions}>
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Pencil size={15} />}
            title="Edit workspace"
            onClick={onEdit}
            data-testid={`workspace-edit-${ws.name}`}
          />
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Trash2 size={15} />}
            title="Delete workspace"
            onClick={onDelete}
            className={styles.deleteButton}
            data-testid={`workspace-delete-${ws.name}`}
          />
        </div>
      </div>

      <h2 className={styles.cardName}>{ws.name}</h2>
      <p className={styles.cardDesc}>{ws.description || <span className={styles.muted}>No description</span>}</p>

      <div className={styles.cardFooter}>
        <span className={styles.fileCount}>
          <FileIcon size={13} aria-hidden="true" />
          {ws.fileCount} file{ws.fileCount !== 1 ? "s" : ""}
        </span>
        <span className={styles.cardDate}>{formatDateTimeShort(ws.updatedAt)}</span>
      </div>
    </Card>
  );
}
