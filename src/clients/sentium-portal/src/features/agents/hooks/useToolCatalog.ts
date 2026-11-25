import { useQuery } from "@tanstack/react-query";
import { agentQueries } from "../api/agents.queries";

export function useToolCatalog() {
  const { data, isLoading, isError } = useQuery(agentQueries.toolCatalog());

  return { tools: data ?? [], isLoading, isError };
}
