import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { TOOL_CATALOG_QUERY_KEY } from "@/features/agents/hooks/useToolCatalog";
import {
  createMcpServer,
  deleteMcpServer,
  fetchMcpServers,
  syncMcpServer,
  updateMcpServer,
  updateMcpTools,
} from "../api/mcp.service";
import type {
  CreateMcpServerPayload,
  McpServer,
  McpToolUpdate,
  UpdateMcpServerPayload,
} from "../types/mcp";

export const MCP_SERVERS_KEY = ["mcp-servers"] as const;

export function useMcpServers(enabled = true) {
  const qc = useQueryClient();

  const query = useQuery({ queryKey: MCP_SERVERS_KEY, queryFn: fetchMcpServers, enabled });

  const replace = (server: McpServer) => {
    qc.setQueryData<McpServer[]>(MCP_SERVERS_KEY, (current) =>
      current ? current.map((s) => (s.id === server.id ? server : s)) : current,
    );
    void qc.invalidateQueries({ queryKey: TOOL_CATALOG_QUERY_KEY });
  };

  const refreshAll = () => {
    void qc.invalidateQueries({ queryKey: MCP_SERVERS_KEY });
    void qc.invalidateQueries({ queryKey: TOOL_CATALOG_QUERY_KEY });
  };

  const create = useMutation({
    mutationFn: (payload: CreateMcpServerPayload) => createMcpServer(payload),
    onSuccess: refreshAll,
  });

  const update = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateMcpServerPayload }) => updateMcpServer(id, payload),
    onSuccess: replace,
  });

  const remove = useMutation({ mutationFn: (id: string) => deleteMcpServer(id), onSuccess: refreshAll });

  const sync = useMutation({
    mutationFn: (id: string) => syncMcpServer(id),
    onSuccess: replace,
    onError: () => void qc.invalidateQueries({ queryKey: MCP_SERVERS_KEY }),
  });

  const updateTools = useMutation({
    mutationFn: ({ id, tools }: { id: string; tools: McpToolUpdate[] }) => updateMcpTools(id, tools),
    onSuccess: replace,
  });

  return {
    servers: query.data ?? [],
    isLoading: query.isLoading,
    error: query.error,
    createServer: create.mutateAsync,
    isCreating: create.isPending,
    updateServer: update.mutateAsync,
    isUpdating: update.isPending,
    deleteServer: remove.mutateAsync,
    syncServer: sync.mutateAsync,
    syncingId: sync.isPending ? sync.variables : undefined,
    updateTools: updateTools.mutateAsync,
    updatingToolsId: updateTools.isPending ? updateTools.variables?.id : undefined,
  };
}
