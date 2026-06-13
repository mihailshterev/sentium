import { Bot, Clock, Cpu, Pencil, X } from "lucide-react";
import styles from "../agents.module.scss";
import type { AgentRecord } from "../../../types/agents";

interface AgentCardProps {
  agent: AgentRecord;
  index: number;
  onEdit: (agent: AgentRecord) => void;
  onDelete: (agentId: string) => void;
}

const AgentCard = ({ agent, index, onEdit, onDelete }: AgentCardProps) => {
  return (
    <div className={styles.agentCard}>
      <div className={styles.agentCardLeft}>
        <div className={styles.agentAvatar}>
          <Bot size={16} />
        </div>
      </div>
      <div className={styles.agentCardContent}>
        <div className={styles.agentCardHeader}>
          <span className={styles.agentName}>{agent.name}</span>
          <span className={styles.agentIndex}>#{String(index + 1).padStart(2, "0")}</span>
          <span className={styles.agentStatusBadge}>Active</span>
        </div>
        <p className={styles.agentDescription}>{agent.description || "No description provided."}</p>
        <div className={styles.agentMeta}>
          {agent.model && (
            <span className={styles.agentMetaItem}>
              <Cpu size={11} />
              {agent.model}
            </span>
          )}
          <span className={styles.agentMetaItem}>
            <Clock size={11} />
            {new Date(agent.createdAt).toLocaleDateString("en-GB", {
              year: "numeric",
              month: "short",
              day: "numeric",
            })}
          </span>
          <span className={styles.agentMetaItem}>ID: {agent.id.slice(0, 8)}</span>
        </div>
      </div>
      <div className={styles.agentCardActions}>
        <button
          className={styles.iconBtn}
          onClick={() => onEdit(agent)}
          title="Edit agent"
          data-testid={`agent-edit-${agent.name}`}
        >
          <Pencil size={13} />
        </button>
        <button
          className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
          onClick={() => onDelete(agent.id)}
          title="Delete agent"
          data-testid={`agent-delete-${agent.name}`}
        >
          <X size={13} />
        </button>
      </div>
    </div>
  );
};

export default AgentCard;
