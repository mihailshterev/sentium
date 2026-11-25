import { client } from "@/shared/api/client";
import { pathIn } from "@/shared/api/url";
import type {
  CreateMcpServerPayload,
  McpServer,
  McpServerStatus,
  McpToolUpdate,
  UpdateMcpServerPayload,
} from "../types/mcp";

const BASE = "/praxis/mcp/servers";
const url = pathIn(BASE);

export const fetchMcpServers = () => client.get<McpServer[]>(BASE);

export const createMcpServer = (payload: CreateMcpServerPayload) => client.post<McpServer>(BASE, payload);

export const updateMcpServer = (id: string, payload: UpdateMcpServerPayload) =>
  client.put<McpServer>(url`/${id}`, payload);

export const deleteMcpServer = (id: string) => client.delete<void>(url`/${id}`);

export const syncMcpServer = (id: string) => client.post<McpServer>(url`/${id}/sync`, {});

export const updateMcpTools = (id: string, tools: McpToolUpdate[]) => client.put<McpServer>(url`/${id}/tools`, { tools });

export const fetchMcpServerStatus = (id: string) => client.get<McpServerStatus>(url`/${id}/status`);
