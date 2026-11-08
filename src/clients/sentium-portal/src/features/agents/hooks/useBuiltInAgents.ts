import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchBuiltInAgents, resetBuiltInAgentTools, updateBuiltInAgentTools } from "../api/agents.service";

export const BUILT_IN_AGENTS_QUERY_KEY = ["agents", "built-in"] as const;

export function useBuiltInAgents() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["agents"] });

  const query = useQuery({ queryKey: BUILT_IN_AGENTS_QUERY_KEY, queryFn: fetchBuiltInAgents });

  const save = useMutation({
    mutationFn: ({ name, tools }: { name: string; tools: string[] | null }) => updateBuiltInAgentTools(name, tools),
    onSuccess: invalidate,
  });

  const reset = useMutation({
    mutationFn: (name: string) => resetBuiltInAgentTools(name),
    onSuccess: invalidate,
  });

  return {
    builtInAgents: query.data ?? [],
    isLoading: query.isLoading,
    isError: query.isError,
    saveTools: save.mutate,
    isSaving: save.isPending,
    saveError: save.error,
    resetTools: reset.mutate,
    isResetting: reset.isPending,
    resetError: reset.error,
  };
}
