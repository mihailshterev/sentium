import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  captureAgentLearning,
  deleteAgentLearning,
  fetchAgentLearnings,
  fetchAgentLearningStats,
  updateAgentLearning,
} from "../services/agentRuntime.service";
import type { CaptureAgentLearningPayload } from "../types/agentConfig";

const LEARNINGS_KEY = (agentName?: string, count?: number) =>
  ["agent-learnings", agentName ?? "all", count ?? 50] as const;

const STATS_KEY = ["agent-learnings", "stats"] as const;

export const useAgentLearnings = (agentName?: string, count = 50) => {
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: LEARNINGS_KEY(agentName, count),
    queryFn: () => fetchAgentLearnings(agentName, count),
    staleTime: 15_000,
    retry: 1,
  });

  const statsQuery = useQuery({
    queryKey: STATS_KEY,
    queryFn: fetchAgentLearningStats,
    staleTime: 15_000,
    retry: 1,
  });

  const captureMutation = useMutation({
    mutationFn: (payload: CaptureAgentLearningPayload) => captureAgentLearning(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["agent-learnings"] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, content, tags }: { id: string; content: string; tags: string }) =>
      updateAgentLearning(id, { content, tags }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["agent-learnings"] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAgentLearning(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["agent-learnings"] });
    },
  });

  return {
    learnings: query.data ?? [],
    isLoading: query.isLoading,
    error: query.error,
    stats: statsQuery.data,
    isStatsLoading: statsQuery.isLoading,
    capture: captureMutation.mutate,
    isCapturing: captureMutation.isPending,
    updateLearning: updateMutation.mutate,
    isUpdating: updateMutation.isPending,
    updatingId: updateMutation.isPending ? (updateMutation.variables as { id: string } | undefined)?.id : null,
    deleteLearning: deleteMutation.mutate,
    isDeleting: deleteMutation.isPending,
  };
};
