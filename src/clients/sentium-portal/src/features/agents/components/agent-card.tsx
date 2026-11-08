import { Cpu, Pencil, Trash2 } from "lucide-react";
import styles from "../agents.module.css";
import { Button } from "@/shared/ui/button";
import type { AgentRecord } from "@/features/agents/types/agents";
import { describeTools } from "../utils/describe-tools";

interface AgentCardProps {
  agent: AgentRecord;
  onEdit: (agent: AgentRecord) => void;
  onDelete: (agentId: string) => void;
}

function initials(name: string): string {
  const words = name
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .split(/[\s_-]+/)
    .filter(Boolean);
  return (words.length > 1 ? words[0][0] + words[1][0] : name.slice(0, 2)).toUpperCase();
}

export function AgentCard({ agent, onEdit, onDelete }: AgentCardProps) {
  return (
    <li className={styles.agentRow}>
      <span className={styles.avatar} aria-hidden="true">
        {initials(agent.name)}
      </span>

      <div className={styles.agentMain}>
        <div className={styles.agentTitleRow}>
          <span className={styles.agentName}>{agent.name}</span>
          {agent.model && (
            <span className={styles.modelChip}>
              <Cpu size={12} aria-hidden="true" />
              {agent.model}
            </span>
          )}
        </div>
        <p className={styles.agentDescription}>{agent.description || "No description provided."}</p>
        <div className={styles.agentMeta}>
          <span>
            Created{" "}
            {new Date(agent.createdAt).toLocaleDateString("en-GB", {
              year: "numeric",
              month: "short",
              day: "numeric",
            })}
          </span>
          <span aria-hidden="true">·</span>
          <span>{describeTools(agent.tools)}</span>
          <span aria-hidden="true">·</span>
          <span className={styles.agentId}>{agent.id.slice(0, 8)}</span>
        </div>
      </div>

      <div className={styles.rowActions}>
        <Button
          variant="ghost"
          size="sm"
          iconOnly
          icon={<Pencil size={15} />}
          onClick={() => onEdit(agent)}
          title="Edit agent"
          data-testid={`agent-edit-${agent.name}`}
        />
        <Button
          variant="ghost"
          size="sm"
          iconOnly
          icon={<Trash2 size={15} />}
          onClick={() => onDelete(agent.id)}
          title="Delete agent"
          className={styles.deleteButton}
          data-testid={`agent-delete-${agent.name}`}
        />
      </div>
    </li>
  );
}
