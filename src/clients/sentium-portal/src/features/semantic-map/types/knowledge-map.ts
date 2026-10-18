export interface KnowledgeMapNode {
  id: string;
  content: string;
  fullContent: string;
  source: string;
  sourceType: string;
  collection: string;
  createdAt: string;
  metadata: Record<string, string>;
}

export interface KnowledgeMapResponse {
  nodes: KnowledgeMapNode[];
  totalNodes: number;
  collections: string[];
}

export interface KnowledgeMapSearchResult {
  id: string;
  score: number;
  content: string;
  fullContent: string;
  source: string;
  sourceType: string;
  collection: string;
  createdAt: string;
}

export interface KnowledgeMapSearchResponse {
  query: string;
  results: KnowledgeMapSearchResult[];
  totalMatches: number;
}
