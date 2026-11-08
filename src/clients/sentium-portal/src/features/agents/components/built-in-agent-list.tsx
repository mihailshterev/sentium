import { SlidersHorizontal } from "lucide-react";
import styles from "../agents.module.css";
import { Button } from "@/shared/ui/button";
import type { BuiltInAgent } from "../types/agents";
import { describeTools } from "../utils/describe-tools";

interface BuiltInAgentListProps {
  agents: BuiltInAgent[];
  onConfigure: (agent: BuiltInAgent) => void;
}

const MAX_CHIPS = 4;

export function BuiltInAgentList({ agents, onConfigure }: BuiltInAgentListProps) {
  return (
    <ul className={styles.agentList}>
      {agents.map((agent) => {
        const shown = agent.tools?.slice(0, MAX_CHIPS) ?? [];
        const more = (agent.tools?.length ?? 0) - shown.length;
        return (
          <li key={agent.name} className={styles.agentRow}>
            <div className={styles.agentMain}>
              <div className={styles.agentTitleRow}>
                <span className={styles.agentName}>{agent.name}</span>
                {agent.isCustomized && <span className={styles.customizedChip}>customized</span>}
              </div>
              <p className={styles.agentDescription}>{agent.description}</p>
              <div className={styles.toolChips} aria-label={`${agent.name} tools: ${describeTools(agent.tools)}`}>
                {agent.tools === null ? (
                  <span className={styles.toolChip}>all tools</span>
                ) : agent.tools.length === 0 ? (
                  <span className={styles.toolChip}>no tools</span>
                ) : (
                  <>
                    {shown.map((tool) => (
                      <span key={tool} className={styles.toolChip}>
                        {tool}
                      </span>
                    ))}
                    {more > 0 && <span className={styles.toolChipMore}>+{more}</span>}
                  </>
                )}
              </div>
            </div>
            <div className={styles.rowActions}>
              <Button
                variant="ghost"
                size="sm"
                icon={<SlidersHorizontal size={14} />}
                onClick={() => onConfigure(agent)}
                data-testid={`builtin-tools-${agent.name}`}
              >
                Tools
              </Button>
            </div>
          </li>
        );
      })}
    </ul>
  );
}
