import type { LogEntry } from "./orchestration";

export interface WorkflowRecord {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  updatedAt: string;
  agents: WorkflowAgentEntry[];
}

export interface SortableAgentItem {
  sortId: string; // unique ID for DnD (agentId may repeat if user adds same agent twice)
  agentId: string;
  name: string;
  model: string;
  description: string;
}

export interface WorkflowAgentEntry {
  agentId: string;
  order: number;
}

export interface WorkflowPayload {
  name: string;
  description: string;
  agents: WorkflowAgentEntry[];
}

export interface UpdateWorkflowPayload extends WorkflowPayload {
  id: string;
}

export interface RunWorkflowPayload {
  workflowId: string;
  scenario: string;
  workspaceId?: string;
}

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
