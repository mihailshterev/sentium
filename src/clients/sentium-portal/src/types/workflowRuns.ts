import type { LogEntry } from "./orchestration";

export interface WorkflowRun {
  id: string;
  triggerType: string;
  triggerPayload: string;
  explanation: string;
  risk: string;
  recommendation: string;
  startedAt: string;
  completedAt: string;
  logs: LogEntry[];
}
