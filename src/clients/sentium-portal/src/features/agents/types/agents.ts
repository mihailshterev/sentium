export interface AgentRecord {
  id: string;
  name: string;
  description: string;
  model: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateAgentPayload {
  name: string;
  description: string;
  model: string;
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
