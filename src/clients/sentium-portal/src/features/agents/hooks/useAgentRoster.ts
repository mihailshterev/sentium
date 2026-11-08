import { useQuery } from "@tanstack/react-query";
import { fetchAgentRoster } from "../api/agents.service";

export const AGENT_ROSTER_QUERY_KEY = ["agents", "roster"] as const;

export function useAgentRoster() {
  const { data, isLoading, isError } = useQuery({
    queryKey: AGENT_ROSTER_QUERY_KEY,
    queryFn: fetchAgentRoster,
    staleTime: 5 * 60_000,
  });

  return { roster: data ?? [], isLoading, isError };
}
