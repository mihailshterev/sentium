export interface AgentRecord {
  id: string;
  name: string;
  description: string;
  model: string;
  createdAt: string;
  updatedAt: string;
  tools?: string[] | null;
}

export interface CreateAgentPayload {
  name: string;
  description: string;
  model: string;
  tools: string[] | null;
}

export interface UpdateAgentPayload extends CreateAgentPayload {
  id: string;
}

export interface AgentRosterEntry {
  name: string;
  description: string;
  kind: "BuiltIn" | "Custom";
  model?: string | null;
}

export interface ToolDescriptor {
  name: string;
  description: string;
  resource: string;
  action: string;
  risk: string;
  requiresApproval: boolean;
}

export interface BuiltInAgent {
  name: string;
  description: string;
  defaultTools: string[] | null;
  tools: string[] | null;
  isCustomized: boolean;
}
