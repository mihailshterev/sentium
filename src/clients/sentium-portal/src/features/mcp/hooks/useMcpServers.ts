import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toolKeys } from "@/features/agents/api/agents.queries";
import {
  createMcpServer,
  deleteMcpServer,
  syncMcpServer,
  updateMcpServer,
  updateMcpTools,
} from "../api/mcp.service";
import { mcpKeys, mcpQueries } from "../api/mcp.queries";
import type { CreateMcpServerPayload, McpServer, McpToolUpdate, UpdateMcpServerPayload } from "../types/mcp";

export function useMcpServers(enabled = true) {
  const qc = useQueryClient();

  const query = useQuery({ ...mcpQueries.servers(), enabled });

  const replace = (server: McpServer) => {
    qc.setQueryData(mcpQueries.servers().queryKey, (current) =>
      current ? current.map((s) => (s.id === server.id ? server : s)) : current,
    );
    void qc.invalidateQueries({ queryKey: toolKeys.catalog() });
  };

  const refreshAll = () => {
    void qc.invalidateQueries({ queryKey: mcpKeys.servers() });
    void qc.invalidateQueries({ queryKey: toolKeys.catalog() });
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
    onError: () => void qc.invalidateQueries({ queryKey: mcpKeys.servers() }),
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
