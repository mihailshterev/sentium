import { client } from "@/shared/api/client";
import { withQuery } from "@/shared/api/url";
import type { KnowledgeMapResponse, KnowledgeMapSearchResponse } from "../types/knowledge-map";

const BASE = "/codex";

export const fetchKnowledgeMapNodes = (limit = 300, collection?: string): Promise<KnowledgeMapResponse> =>
  client.get<KnowledgeMapResponse>(withQuery(`${BASE}/knowledge-map/nodes`, { limit, collection }));

export const searchKnowledgeMap = (query: string, topK = 20): Promise<KnowledgeMapSearchResponse> =>
  client.post<KnowledgeMapSearchResponse>(`${BASE}/knowledge-map/search`, { query, topK });
