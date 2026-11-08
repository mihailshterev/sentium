import { useQuery } from "@tanstack/react-query";
import { fetchToolCatalog } from "../api/agents.service";

export const TOOL_CATALOG_QUERY_KEY = ["tools"] as const;

export function useToolCatalog() {
  const { data, isLoading, isError } = useQuery({
    queryKey: TOOL_CATALOG_QUERY_KEY,
    queryFn: fetchToolCatalog,
    staleTime: 30 * 60_000,
  });

  return { tools: data ?? [], isLoading, isError };
}
