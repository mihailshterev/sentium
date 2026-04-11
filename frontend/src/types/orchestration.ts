export interface LogEntry {
  Author: string;
  Text: string;
}

export interface WorkflowRecord {
  id: string;
  name: string;
  description: string;
  agents: { agentId: string; order: number }[];
}

export type Phase = "IDLE" | "PLANNING" | "SQUAD" | "VALIDATING" | "COMPLETE";
