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
import type { AgentLearning, AgentLearningStats, KnowledgeBaseCollectionStats } from "../types/agentConfig";
import type { DeleteModelResult, OllamaModel } from "../types/models";
import type { AgentSkill, AgentSkillType, BuiltInSkill, CreateSkillPayload, UpdateSkillPayload } from "../types/skills";
import { BASE_URL, client, handleUnauthorized } from "../api/client";
import type { KnowledgeMapResponse, KnowledgeMapSearchResponse } from "../types/knowledge-map";
import type { PagedResponse } from "../types/pagination";

const BASE = "/agent-runtime";

export const fetchAgentsPaged = (page = 1, pageSize = 100): Promise<PagedResponse<AgentRecord>> =>
  client.get<PagedResponse<AgentRecord>>(`${BASE}/agents?page=${page}&pageSize=${pageSize}`);

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

export const fetchWorkflowsPaged = (page = 1, pageSize = 100): Promise<PagedResponse<WorkflowRecord>> =>
  client.get<PagedResponse<WorkflowRecord>>(`${BASE}/workflows?page=${page}&pageSize=${pageSize}`);

export const createWorkflow = (payload: WorkflowPayload) => client.post<WorkflowRecord>(`${BASE}/workflows`, payload);

export const updateWorkflow = ({ id, ...payload }: UpdateWorkflowPayload) =>
  client.put<WorkflowRecord>(`${BASE}/workflows/${id}`, payload);

export const deleteWorkflow = (id: string) => client.delete<void>(`${BASE}/workflows/${id}`);

export const fetchConversations = (page = 1, pageSize = 20): Promise<PagedResponse<ConversationSummary>> =>
  client.get<PagedResponse<ConversationSummary>>(`${BASE}/conversations?page=${page}&pageSize=${pageSize}`);

export const fetchConversation = (id: string) => client.get<ConversationDetail>(`${BASE}/conversations/${id}`);

export const createConversation = (payload: CreateConversationPayload) =>
  client.post<CreateConversationResult>(`${BASE}/conversations`, payload);

export const deleteConversation = (id: string) => client.delete<void>(`${BASE}/conversations/${id}`);

export const runDynamicWorkflow = (payload: Record<string, string>) =>
  client.post<{ eventId: string }>(`${BASE}/orchestration/run-dynamic-workflow`, payload);

export const runWorkflowPipeline = (payload: RunWorkflowPayload) =>
  client.post<{ eventId: string }>(`${BASE}/orchestration/run-workflow`, payload);

export const fetchWorkflowRunsPaged = (page = 1, pageSize = 20): Promise<PagedResponse<WorkflowRun>> =>
  client.get<PagedResponse<WorkflowRun>>(`${BASE}/workflows/runs?page=${page}&pageSize=${pageSize}`);

export const fetchWorkflowRuns = async (pageSize = 15): Promise<WorkflowRun[]> =>
  (await fetchWorkflowRunsPaged(1, pageSize)).items;

export const fetchWorkflowRun = (runId: string): Promise<WorkflowRun> =>
  client.get<WorkflowRun>(`${BASE}/workflows/runs/${runId}`);

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
    `${BASE}/workspaces/files${workspaceId ? `?workspaceId=${encodeURIComponent(workspaceId)}` : ""}`,
  );

export const deleteWorkspaceFile = (fileId: string): Promise<void> =>
  client.delete<void>(`${BASE}/workspaces/files/${fileId}`);

export const fetchWorkspacesPaged = (page = 1, pageSize = 100): Promise<PagedResponse<Workspace>> =>
  client.get<PagedResponse<Workspace>>(`${BASE}/workspaces?page=${page}&pageSize=${pageSize}`);

export const fetchWorkspaces = async (): Promise<Workspace[]> => (await fetchWorkspacesPaged(1, 100)).items;

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

  const response = await fetch(`${BASE_URL}${BASE}/workspaces/files`, {
    method: "POST",
    body: formData,
    credentials: "include",
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error((errorData as { error?: string }).error || `Upload failed: ${response.status}`);
  }

  return response.json() as Promise<WorkspaceFile>;
};

export const fetchAgentLearnings = (
  agentName?: string,
  page = 1,
  pageSize = 20,
): Promise<PagedResponse<AgentLearning>> => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (agentName) {
    params.set("agentName", agentName);
  }
  return client.get<PagedResponse<AgentLearning>>(`${BASE}/agent-learnings?${params}`);
};

export const fetchAgentLearningStats = (): Promise<AgentLearningStats> =>
  client.get<AgentLearningStats>(`${BASE}/agent-learnings/stats`);

export const updateAgentLearning = (id: string, payload: { content: string; tags: string }): Promise<AgentLearning> =>
  client.put<AgentLearning>(`${BASE}/agent-learnings/${id}`, payload);

export const deleteAgentLearning = (id: string): Promise<void> => client.delete<void>(`${BASE}/agent-learnings/${id}`);

export const fetchKnowledgeBaseStats = (): Promise<KnowledgeBaseCollectionStats[]> =>
  client.get<KnowledgeBaseCollectionStats[]>(`${BASE}/knowledge-base/stats`);

export const deleteKnowledgeMapCollection = (collection: string): Promise<void> =>
  client.delete<void>(`${BASE}/knowledge-base/collections/${encodeURIComponent(collection)}`);

export const fetchBuiltInSkills = (): Promise<BuiltInSkill[]> => client.get<BuiltInSkill[]>(`${BASE}/skills/built-in`);

export const fetchSkillsPaged = (
  skillType?: AgentSkillType,
  page = 1,
  pageSize = 20,
): Promise<PagedResponse<AgentSkill>> => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (skillType !== undefined) {
    params.set("skillType", String(skillType));
  }
  return client.get<PagedResponse<AgentSkill>>(`${BASE}/skills?${params}`);
};

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
    handleUnauthorized();
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
