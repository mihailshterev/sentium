import type { KnowledgeMapNode } from "../../../types/knowledge-map";

export const DEMO_NODES: KnowledgeMapNode[] = Array.from({ length: 60 }, (_, i) => {
  const collections = ["knowledge_base", "agent_learnings", "user_memories"];
  const sources = ["sentinel-audit", "network-analysis", "agent-learning", "memory-capture", "file-ingestion"];
  const col = collections[i % 3];
  return {
    id: `demo-${i}`,
    content: `Demo concept node ${i + 1} — this node represents a chunk of embedded knowledge.`,
    fullContent: `Full content for demo node ${i + 1}. This would contain the actual document chunk content from the vector store.`,
    source: sources[i % 5],
    sourceType: "Custom",
    collection: col,
    createdAt: new Date(Date.now() - i * 86400000).toISOString(),
    metadata: {},
  };
});
