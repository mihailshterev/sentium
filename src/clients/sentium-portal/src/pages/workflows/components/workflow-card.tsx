import { Bot, GitBranch, Pencil, Trash2 } from "lucide-react";
import styles from "../workflows.module.scss";
import type { WorkflowRecord } from "../../../types/workflows";

interface WorkflowCardProps {
  workflow: WorkflowRecord;
  isActive: boolean;
  onSelect: (workflow: WorkflowRecord) => void;
  onEdit: (workflow: WorkflowRecord) => void;
  onDelete: (workflowId: string) => void;
}

const WorkflowCard = ({ workflow, isActive, onSelect, onEdit, onDelete }: WorkflowCardProps) => {
  return (
    <div
      className={`${styles.workflowCard} ${isActive ? styles.workflowCardActive : ""}`}
      onClick={() => onSelect(workflow)}
    >
      <div className={styles.workflowCardHeader}>
        <GitBranch size={13} className={styles.wfIcon} />
        <span className={styles.wfName}>{workflow.name}</span>
      </div>
      <p className={styles.wfDescription}>{workflow.description || "No description"}</p>
      <div className={styles.wfMeta}>
        <span className={styles.wfAgentCount}>
          <Bot size={11} />
          {workflow.agents.length} agent{workflow.agents.length !== 1 ? "s" : ""}
        </span>
        <div className={styles.wfActions}>
          <button
            className={styles.wfActionBtn}
            onClick={(e) => {
              e.stopPropagation();
              onEdit(workflow);
            }}
            title="Edit"
            data-testid={`workflow-edit-${workflow.name}`}
          >
            <Pencil size={11} />
          </button>
          <button
            className={`${styles.wfActionBtn} ${styles.wfActionBtnDanger}`}
            onClick={(e) => {
              e.stopPropagation();
              onDelete(workflow.id);
            }}
            title="Delete"
            data-testid={`workflow-delete-${workflow.name}`}
          >
            <Trash2 size={11} />
          </button>
        </div>
      </div>
    </div>
  );
};

export default WorkflowCard;
