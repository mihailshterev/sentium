import { queryOptions } from "@tanstack/react-query";
import { fetchMcpServers, fetchMcpServerStatus } from "./mcp.service";

export const mcpKeys = {
  servers: () => ["mcp-servers"] as const,
  status: (id: string) => ["mcp-server-status", id] as const,
};

export const mcpQueries = {
  servers: () => queryOptions({ queryKey: mcpKeys.servers(), queryFn: () => fetchMcpServers() }),
  status: (id: string) => queryOptions({ queryKey: mcpKeys.status(id), queryFn: () => fetchMcpServerStatus(id) }),
};
