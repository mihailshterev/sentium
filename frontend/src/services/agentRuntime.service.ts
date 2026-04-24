import type { AgentRecord } from "../types/agents";
import type { WorkflowRecord } from "../types/workflows";
import type { ConversationSummary } from "../types/assistant";
import { BASE_URL, client } from "../api/client";

const BASE = "/agent-runtime";

export interface CreateAgentPayload {
  name: string;
  description: string;
  model: string;
}

export interface UpdateAgentPayload extends CreateAgentPayload {
  id: string;
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

export interface ConversationDetail {
  id: string;
  title: string;
  model: string;
  messages: { id: string; role: "user" | "assistant"; content: string; timestamp: string }[];
}

export interface CreateConversationPayload {
  title: string;
  model: string;
}

export interface CreateConversationResult {
  id: string;
}

export interface RunWorkflowPayload {
  workflowId: string;
  scenario: string;
}

export interface ChatMessage {
  role: string;
  content: string;
}

export interface ChatPayload {
  conversationId?: string;
  model: string;
  messages: ChatMessage[];
  stream: boolean;
}

// TODO: Reorganize
export const fetchAgents = () => client.get<AgentRecord[]>(`${BASE}/agents`);

export const createAgent = (payload: CreateAgentPayload) => client.post<AgentRecord>(`${BASE}/agents`, payload);

export const updateAgent = ({ id, ...payload }: UpdateAgentPayload) =>
  client.put<AgentRecord>(`${BASE}/agents/${id}`, { id, ...payload });

export const deleteAgent = (id: string) => client.delete<void>(`${BASE}/agents/${id}`);

export const fetchModels = () => client.get<string[]>(`${BASE}/assistant/models`);

export const fetchWorkflows = () => client.get<WorkflowRecord[]>(`${BASE}/workflows`);

export const createWorkflow = (payload: WorkflowPayload) => client.post<WorkflowRecord>(`${BASE}/workflows`, payload);

export const updateWorkflow = ({ id, ...payload }: UpdateWorkflowPayload) =>
  client.put<WorkflowRecord>(`${BASE}/workflows/${id}`, payload);

export const deleteWorkflow = (id: string) => client.delete<void>(`${BASE}/workflows/${id}`);

export const fetchConversations = () => client.get<ConversationSummary[]>(`${BASE}/conversations`);

export const fetchConversation = (id: string) => client.get<ConversationDetail>(`${BASE}/conversations/${id}`);

export const createConversation = (payload: CreateConversationPayload) =>
  client.post<CreateConversationResult>(`${BASE}/conversations`, payload);

export const deleteConversation = (id: string) => client.delete<void>(`${BASE}/conversations/${id}`);

export const runPipeline = (payload: Record<string, string>) =>
  client.post<{ eventId: string }>(`${BASE}/agents/test-pipeline`, payload);

export const runWorkflowPipeline = (payload: RunWorkflowPayload) =>
  client.post<{ eventId: string }>(`${BASE}/agents/run-workflow`, payload);

export const triggerNetworkAnalysis = (event: NetworkEvent): Promise<{ eventId: string }> =>
  client.post<{ eventId: string }>(`${BASE}/agents/analyze-network-event`, event);

export const sendChatMessage = (payload: ChatPayload): Promise<Response> => {
  return fetch(`${BASE_URL}${BASE}/assistant/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
    credentials: "include",
  });
};
