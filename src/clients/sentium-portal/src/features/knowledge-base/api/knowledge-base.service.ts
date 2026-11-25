import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { AgentLearning, AgentLearningStats, KnowledgeBaseCollectionStats } from "../types/agent-learning";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/praxis";
const CODEX_BASE = "/codex";
const url = pathIn(BASE);
const codexUrl = pathIn(CODEX_BASE);

export const fetchAgentLearnings = (
  agentName?: string,
  page = 1,
  pageSize = 20,
): Promise<PagedResponse<AgentLearning>> =>
  client.get<PagedResponse<AgentLearning>>(withQuery(`${BASE}/agent-learnings`, { page, pageSize, agentName }));

export const fetchAgentLearningStats = (): Promise<AgentLearningStats> =>
  client.get<AgentLearningStats>(`${BASE}/agent-learnings/stats`);

export const updateAgentLearning = (id: string, payload: { content: string; tags: string }): Promise<AgentLearning> =>
  client.put<AgentLearning>(url`/agent-learnings/${id}`, payload);

export const deleteAgentLearning = (id: string): Promise<void> => client.delete<void>(url`/agent-learnings/${id}`);

export const fetchKnowledgeBaseStats = (): Promise<KnowledgeBaseCollectionStats[]> =>
  client.get<KnowledgeBaseCollectionStats[]>(`${CODEX_BASE}/knowledge-base/stats`);

export const deleteKnowledgeMapCollection = (collection: string): Promise<void> =>
  client.delete<void>(codexUrl`/knowledge-base/collections/${collection}`);
