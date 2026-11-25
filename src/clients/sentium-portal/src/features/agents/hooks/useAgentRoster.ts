import { useQuery } from "@tanstack/react-query";
import { agentQueries } from "../api/agents.queries";

export function useAgentRoster() {
  const { data, isLoading, isError } = useQuery(agentQueries.roster());

  return { roster: data ?? [], isLoading, isError };
}
