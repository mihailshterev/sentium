import { queryOptions } from "@tanstack/react-query";
import { fetchAgentRoster, fetchBuiltInAgents, fetchToolCatalog } from "./agents.service";

/** Invalidating `all` refreshes the list, the roster and the built-in agents together. */
export const agentKeys = {
  all: ["agents"] as const,
  list: () => [...agentKeys.all, "list"] as const,
  roster: () => [...agentKeys.all, "roster"] as const,
  builtIn: () => [...agentKeys.all, "built-in"] as const,
};

/** Outside `agentKeys`: the catalog changes with MCP servers, not with agents. */
export const toolKeys = {
  catalog: () => ["tools"] as const,
};

export const agentQueries = {
  roster: () =>
    queryOptions({ queryKey: agentKeys.roster(), queryFn: () => fetchAgentRoster(), staleTime: 5 * 60_000 }),
  builtIn: () => queryOptions({ queryKey: agentKeys.builtIn(), queryFn: () => fetchBuiltInAgents() }),
  toolCatalog: () =>
    queryOptions({ queryKey: toolKeys.catalog(), queryFn: () => fetchToolCatalog(), staleTime: 30 * 60_000 }),
};
