import { client } from "@/shared/api/client";
import type {
  CreateMcpServerPayload,
  McpServer,
  McpServerStatus,
  McpToolUpdate,
  UpdateMcpServerPayload,
} from "../types/mcp";

const BASE = "/praxis/mcp/servers";

export const fetchMcpServers = () => client.get<McpServer[]>(BASE);

export const createMcpServer = (payload: CreateMcpServerPayload) => client.post<McpServer>(BASE, payload);

export const updateMcpServer = (id: string, payload: UpdateMcpServerPayload) =>
  client.put<McpServer>(`${BASE}/${id}`, payload);

export const deleteMcpServer = (id: string) => client.delete<void>(`${BASE}/${id}`);

export const syncMcpServer = (id: string) => client.post<McpServer>(`${BASE}/${id}/sync`, {});

export const updateMcpTools = (id: string, tools: McpToolUpdate[]) =>
  client.put<McpServer>(`${BASE}/${id}/tools`, { tools });

export const fetchMcpServerStatus = (id: string) => client.get<McpServerStatus>(`${BASE}/${id}/status`);
