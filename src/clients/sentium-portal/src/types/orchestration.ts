export interface LogEntry {
  author: string;
  text: string;
  type: "message" | "thought" | "tool" | "approval" | "status";
}

export interface WorkflowRecord {
  id: string;
  name: string;
  description: string;
  agents: { agentId: string; order: number }[];
}

export type Phase = "IDLE" | "PLANNING" | "SQUAD" | "VALIDATING" | "COMPLETE";
