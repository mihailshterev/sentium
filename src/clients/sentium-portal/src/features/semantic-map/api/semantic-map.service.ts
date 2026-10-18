import { client } from "@/shared/api/client";
import type { KnowledgeMapResponse, KnowledgeMapSearchResponse } from "../types/knowledge-map";

const BASE = "/agent-runtime";

export const fetchKnowledgeMapNodes = (limit = 300, collection?: string): Promise<KnowledgeMapResponse> => {
  const params = new URLSearchParams({ limit: String(limit) });
  if (collection) params.set("collection", collection);
  return client.get<KnowledgeMapResponse>(`${BASE}/knowledge-map/nodes?${params}`);
};

export const searchKnowledgeMap = (query: string, topK = 20): Promise<KnowledgeMapSearchResponse> =>
  client.post<KnowledgeMapSearchResponse>(`${BASE}/knowledge-map/search`, { query, topK });
