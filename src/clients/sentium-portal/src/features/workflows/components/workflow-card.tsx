import { GitBranch, Pencil, Trash2 } from "lucide-react";
import styles from "../workflows.module.css";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import type { WorkflowRecord } from "@/features/workflows/types/workflows";

interface WorkflowCardProps {
  workflow: WorkflowRecord;
  isActive: boolean;
  onSelect: (workflow: WorkflowRecord) => void;
  onEdit: (workflow: WorkflowRecord) => void;
  onDelete: (workflowId: string) => void;
}

/** Max pipeline nodes drawn; longer pipelines show a "+N" tail. */
const MAX_NODES = 6;

export function WorkflowCard({ workflow, isActive, onSelect, onEdit, onDelete }: WorkflowCardProps) {
  const count = workflow.agents.length;
  const nodes = Math.min(count, MAX_NODES);

  return (
    <Card
      interactive
      padding="md"
      className={`${styles.workflowCard} ${isActive ? styles.workflowCardActive : ""}`}
      onClick={() => onSelect(workflow)}
    >
      <div className={styles.cardTop}>
        <span className={styles.cardIcon} aria-hidden="true">
          <GitBranch size={16} />
        </span>
        <div className={styles.cardActions}>
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Pencil size={15} />}
            onClick={(e) => {
              e.stopPropagation();
              onEdit(workflow);
            }}
            title="Edit"
            data-testid={`workflow-edit-${workflow.name}`}
          />
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Trash2 size={15} />}
            onClick={(e) => {
              e.stopPropagation();
              onDelete(workflow.id);
            }}
            title="Delete"
            className={styles.deleteButton}
            data-testid={`workflow-delete-${workflow.name}`}
          />
        </div>
      </div>

      <span className={styles.cardName}>{workflow.name}</span>
      <p className={styles.cardDescription}>{workflow.description || "No description"}</p>

      <div className={styles.cardFooter}>
        <div className={styles.pipelinePreview} aria-hidden="true">
          {Array.from({ length: nodes }, (_, i) => (
            <span key={i} className={styles.previewNode} />
          ))}
          {count > MAX_NODES && <span className={styles.previewMore}>+{count - MAX_NODES}</span>}
        </div>
        <span className={styles.agentCount}>
          {count} agent{count !== 1 ? "s" : ""}
        </span>
      </div>
    </Card>
  );
}
