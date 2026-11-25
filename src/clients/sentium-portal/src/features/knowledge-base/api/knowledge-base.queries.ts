import { queryOptions } from "@tanstack/react-query";
import { fetchAgentLearningStats, fetchKnowledgeBaseStats } from "./knowledge-base.service";
import { fetchMemories } from "./memories.service";

export const learningKeys = {
  all: ["agent-learnings"] as const,
  list: (agentName?: string) => [...learningKeys.all, "list", agentName ?? "all"] as const,
  stats: () => [...learningKeys.all, "stats"] as const,
};

export const knowledgeBaseKeys = {
  stats: () => ["knowledge-base-stats"] as const,
};

export const memoryKeys = {
  all: ["user-memories"] as const,
};

export const knowledgeBaseQueries = {
  learningStats: () =>
    queryOptions({ queryKey: learningKeys.stats(), queryFn: () => fetchAgentLearningStats(), staleTime: 15_000 }),
  collectionStats: () =>
    queryOptions({ queryKey: knowledgeBaseKeys.stats(), queryFn: () => fetchKnowledgeBaseStats(), staleTime: 30_000 }),
  memories: () => queryOptions({ queryKey: memoryKeys.all, queryFn: () => fetchMemories(), staleTime: 15_000 }),
};
