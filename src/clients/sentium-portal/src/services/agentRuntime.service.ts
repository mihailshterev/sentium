import type { AgentRecord, CreateAgentPayload, UpdateAgentPayload } from "../types/agents";
import type {
  RunWorkflowPayload,
  UpdateWorkflowPayload,
  WorkflowPayload,
  WorkflowRecord,
  WorkflowRun,
} from "../types/workflows";
import type {
  ChatPayload,
  ConversationDetail,
  ConversationSummary,
  CreateConversationPayload,
  CreateConversationResult,
} from "../types/assistant";
import type { Workspace, WorkspaceFile, CreateWorkspacePayload, UpdateWorkspacePayload } from "../types/workspace";
import type {
  SystemSettings,
  UpdateSystemSettingsPayload,
  AgentLearning,
  AgentLearningStats,
  CaptureAgentLearningPayload,
  KnowledgeBaseCollectionStats,
} from "../types/agentConfig";
import type { DeleteModelResult, OllamaModel } from "../types/models";
import type { AgentSkill, BuiltInSkill, CreateSkillPayload, UpdateSkillPayload } from "../types/skills";
import { BASE_URL, client } from "../api/client";
import type { KnowledgeMapResponse, KnowledgeMapSearchResponse } from "../types/knowledge-map";

const BASE = "/agent-runtime";

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

export const approveToolCall = (requestId: string, approved: boolean, signal?: AbortSignal): Promise<Response> => {
  return fetch(`${BASE_URL}${BASE}/assistant/chat/approve`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ requestId, approved }),
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

export const fetchSystemSettings = (): Promise<SystemSettings> => client.get<SystemSettings>(`${BASE}/system-settings`);

export const updateSystemSettings = (payload: UpdateSystemSettingsPayload): Promise<SystemSettings> =>
  client.put<SystemSettings>(`${BASE}/system-settings`, payload);

export const fetchAgentLearnings = (agentName?: string, count = 50): Promise<AgentLearning[]> => {
  const params = new URLSearchParams({ count: String(count) });
  if (agentName) {
    params.set("agentName", agentName);
  }
  return client.get<AgentLearning[]>(`${BASE}/agent-learnings?${params}`);
};

export const fetchAgentLearningStats = (): Promise<AgentLearningStats> =>
  client.get<AgentLearningStats>(`${BASE}/agent-learnings/stats`);

export const captureAgentLearning = (payload: CaptureAgentLearningPayload): Promise<AgentLearning> =>
  client.post<AgentLearning>(`${BASE}/agent-learnings`, payload);

export const updateAgentLearning = (id: string, payload: { content: string; tags: string }): Promise<AgentLearning> =>
  client.put<AgentLearning>(`${BASE}/agent-learnings/${id}`, payload);

export const deleteAgentLearning = (id: string): Promise<void> => client.delete<void>(`${BASE}/agent-learnings/${id}`);

export const fetchKnowledgeBaseStats = (): Promise<KnowledgeBaseCollectionStats[]> =>
  client.get<KnowledgeBaseCollectionStats[]>(`${BASE}/knowledge-base/stats`);

export const deleteKnowledgeMapCollection = (collection: string): Promise<void> =>
  client.delete<void>(`${BASE}/knowledge-base/collections/${encodeURIComponent(collection)}`);

export const fetchBuiltInSkills = (): Promise<BuiltInSkill[]> => client.get<BuiltInSkill[]>(`${BASE}/skills/built-in`);

export const fetchSkills = (): Promise<AgentSkill[]> => client.get<AgentSkill[]>(`${BASE}/skills`);

export const createSkill = (payload: CreateSkillPayload): Promise<AgentSkill> =>
  client.post<AgentSkill>(`${BASE}/skills`, payload);

export const updateSkill = (id: string, payload: UpdateSkillPayload): Promise<void> =>
  client.put<void>(`${BASE}/skills/${id}`, payload);

export const deleteSkill = (id: string): Promise<void> => client.delete<void>(`${BASE}/skills/${id}`);

export const uploadSkillFile = async (file: File): Promise<AgentSkill> => {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${BASE_URL}${BASE}/skills/upload`, {
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

  return response.json() as Promise<AgentSkill>;
};

export const fetchKnowledgeMapNodes = (limit = 300, collection?: string): Promise<KnowledgeMapResponse> => {
  const params = new URLSearchParams({ limit: String(limit) });
  if (collection) params.set("collection", collection);
  return client.get<KnowledgeMapResponse>(`${BASE}/knowledge-map/nodes?${params}`);
};

export const searchKnowledgeMap = (query: string, topK = 20): Promise<KnowledgeMapSearchResponse> =>
  client.post<KnowledgeMapSearchResponse>(`${BASE}/knowledge-map/search`, { query, topK });
