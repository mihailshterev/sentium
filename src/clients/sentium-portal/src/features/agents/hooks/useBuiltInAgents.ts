import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { resetBuiltInAgentTools, updateBuiltInAgentTools } from "../api/agents.service";
import { agentKeys, agentQueries } from "../api/agents.queries";

export function useBuiltInAgents() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: agentKeys.all });

  const query = useQuery(agentQueries.builtIn());

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
