import { useMemo } from "react";
import styles from "../agent-orchestration.module.css";
import { AgentTimeline, stepsFromWorkflowLog, type ActivityLogEntry } from "@/shared/agent-activity";
import { parseAssignments } from "@/shared/utils/agent-helpers";
import { OrchestratorPlanView } from "./orchestrator-plan-view";

interface WorkflowTimelineProps {
  logs: ActivityLogEntry[];
  live: boolean;
  getRoleClass: (author: string) => string;
}

export function WorkflowTimeline({ logs, live, getRoleClass }: WorkflowTimelineProps) {
  const steps = useMemo(() => stepsFromWorkflowLog(logs), [logs]);

  return (
    <AgentTimeline
      className={styles.timeline}
      steps={steps}
      live={live}
      renderAuthor={(author) => <span className={`${styles.roleBadge} ${styles[getRoleClass(author)]}`}>{author}</span>}
      renderOutput={(step) => {
        const plan = step.author?.toLowerCase().includes("orchestrator") ? parseAssignments(step.text) : null;
        return plan ? <OrchestratorPlanView assignments={plan} getRoleClass={getRoleClass} /> : undefined;
      }}
    />
  );
}
