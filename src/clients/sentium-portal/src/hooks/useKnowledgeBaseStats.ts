import { useQuery } from "@tanstack/react-query";
import { fetchKnowledgeBaseStats } from "../services/agentRuntime.service";

const KB_STATS_KEY = ["knowledge-base-stats"] as const;

export const useKnowledgeBaseStats = () => {
  const query = useQuery({
    queryKey: KB_STATS_KEY,
    queryFn: fetchKnowledgeBaseStats,
    staleTime: 30_000,
    retry: 1,
  });

  return {
    collections: query.data ?? [],
    isLoading: query.isLoading,
    error: query.error,
    refetch: query.refetch,
  };
};
