import { Bot, Cpu, FileText, GitBranch } from "lucide-react";
import type { ElementType } from "react";
import styles from "../agent-orchestration.module.css";
import type { AgentAssignment } from "../../../shared/utils/agent-helpers";

const roleIcon = (author: string): ElementType => {
  const a = author.toLowerCase();
  if (a.includes("summar")) return FileText;
  if (a.includes("architect")) return GitBranch;
  if (a.includes("develop")) return Cpu;
  return Bot;
};

interface OrchestratorPlanViewProps {
  assignments: AgentAssignment[];
  getRoleClass: (author: string) => string;
}

export function OrchestratorPlanView({ assignments, getRoleClass }: OrchestratorPlanViewProps) {
  return (
    <ol className={styles.plan} aria-label="Plan">
      {assignments.map((a, i) => {
        const Icon = roleIcon(a.agent);
        return (
          <li key={`${a.agent}-${i}`} className={`${styles.planStep} ${styles[getRoleClass(a.agent)]}`}>
            <span className={styles.planNum}>{i + 1}</span>
            <div className={styles.planMain}>
              <span className={styles.planAgent}>
                <Icon size={14} aria-hidden="true" />
                {a.agent}
              </span>
              <span className={styles.planTask}>{a.task}</span>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
