import { client } from "@/shared/api/client";
import type { AgentLearning, AgentLearningStats, KnowledgeBaseCollectionStats } from "../types/agent-learning";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/agent-runtime";

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
