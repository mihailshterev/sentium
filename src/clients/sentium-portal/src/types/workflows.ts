export interface WorkflowAgentRef {
  agentId: string;
  order: number;
}

export interface WorkflowRecord {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  updatedAt: string;
  agents: WorkflowAgentRef[];
}

export interface SortableAgentItem {
  sortId: string; // unique ID for DnD (agentId may repeat if user adds same agent twice)
  agentId: string;
  name: string;
}
