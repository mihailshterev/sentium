import { Bot, Cpu, FileText, GitBranch, ChevronDown } from "lucide-react";
import type { ElementType } from "react";
import styles from "../agent-orchestration.module.scss";
import type { AgentAssignment } from "../../../utils/agent-helpers";

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

const OrchestratorPlanView = ({ assignments, getRoleClass }: OrchestratorPlanViewProps) => (
  <div className={styles.planFlow}>
    {assignments.map((a, i) => {
      const Icon = roleIcon(a.agent);
      const roleClass = styles[getRoleClass(a.agent)];
      return (
        <div key={`${a.agent}-${i}`} className={styles.planStep}>
          <div className={`${styles.planCard} ${roleClass}`}>
            <span className={styles.planStepNum}>{i + 1}</span>
            <div className={styles.planCardMain}>
              <div className={styles.planCardHeader}>
                <span className={`${styles.planCardIcon} ${roleClass}`}>
                  <Icon size={14} />
                </span>
                <span className={styles.planAgentName}>{a.agent}</span>
              </div>
              <div className={styles.planCardTask}>{a.task}</div>
            </div>
          </div>
          {i < assignments.length - 1 && (
            <div className={styles.planConnector} aria-hidden="true">
              <ChevronDown size={14} />
            </div>
          )}
        </div>
      );
    })}
  </div>
);

export default OrchestratorPlanView;
