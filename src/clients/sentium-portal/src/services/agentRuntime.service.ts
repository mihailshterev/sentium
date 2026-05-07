import type { AgentRecord } from "../types/agents";
import type { WorkflowRecord } from "../types/workflows";
import type { WorkflowRun } from "../types/workflowRuns";
import type { NetworkEvent } from "../types/sentinel";
import type { ConversationSummary } from "../types/assistant";
import type { Workspace, WorkspaceFile, CreateWorkspacePayload, UpdateWorkspacePayload } from "../types/workspace";
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
  workspaceId?: string;
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

export const fetchModels = async (): Promise<string[]> => {
  const models = await client.get<OllamaModel[]>(`${BASE}/models`);
  return models.map((m) => m.name);
};

export interface OllamaModelDetails {
  format: string;
  family: string;
  parameter_size: string;
  quantization_level: string;
}

export interface OllamaModel {
  name: string;
  modified_at: string;
  size: number;
  digest: string;
  details: OllamaModelDetails;
}

export interface PullProgress {
  status: string;
  digest?: string;
  total?: number;
  completed?: number;
}

export const fetchOllamaModels = () => client.get<OllamaModel[]>(`${BASE}/models`);

export const pullModel = (name: string, signal?: AbortSignal): Promise<Response> => {
  return fetch(`${BASE_URL}${BASE}/models/pull`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name }),
    credentials: "include",
    signal,
  });
};

export interface DeleteModelResult {
  deletedModel: string;
  defaultModel: string;
  agentsReset: number;
}

export const deleteOllamaModel = (name: string): Promise<DeleteModelResult> =>
  client.delete<DeleteModelResult>(`${BASE}/models?name=${encodeURIComponent(name)}`);

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

export const fetchWorkflowRuns = (count = 15): Promise<WorkflowRun[]> =>
  client.get<WorkflowRun[]>(`${BASE}/workflows/runs?count=${count}`);

export const sendChatMessage = (payload: ChatPayload, signal?: AbortSignal): Promise<Response> => {
  return fetch(`${BASE_URL}${BASE}/assistant/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
    credentials: "include",
    signal,
  });
};

export const listWorkspaceFiles = (workspaceId?: string): Promise<WorkspaceFile[]> =>
  client.get<WorkspaceFile[]>(
    `${BASE}/workspace/files${workspaceId ? `?workspaceId=${encodeURIComponent(workspaceId)}` : ""}`,
  );

export const deleteWorkspaceFile = (fileId: string): Promise<void> =>
  client.delete<void>(`${BASE}/workspace/files/${fileId}`);

export const fetchWorkspaces = (): Promise<Workspace[]> => client.get<Workspace[]>(`${BASE}/workspaces`);

export const createWorkspace = (payload: CreateWorkspacePayload): Promise<Workspace> =>
  client.post<Workspace>(`${BASE}/workspaces`, payload);

export const updateWorkspace = (id: string, payload: UpdateWorkspacePayload): Promise<Workspace> =>
  client.put<Workspace>(`${BASE}/workspaces/${id}`, payload);

export const deleteWorkspace = (id: string): Promise<void> => client.delete<void>(`${BASE}/workspaces/${id}`);

export const fetchWorkspaceFiles = (workspaceId: string): Promise<WorkspaceFile[]> =>
  client.get<WorkspaceFile[]>(`${BASE}/workspaces/${workspaceId}/files`);

export const uploadWorkspaceFile = async (file: File, workspaceId?: string): Promise<WorkspaceFile> => {
  const formData = new FormData();
  formData.append("file", file);
  if (workspaceId) {
    formData.append("workspaceId", workspaceId);
  }

  const response = await fetch(`${BASE_URL}${BASE}/workspace/files`, {
    method: "POST",
    body: formData,
    credentials: "include",
  });

  if (response.status === 401) {
    window.location.href = `${BASE_URL.replace("/api", "")}/bff/login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
    throw new Error("Session expired.");
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error((errorData as { error?: string }).error || `Upload failed: ${response.status}`);
  }

  return response.json() as Promise<WorkspaceFile>;
};
