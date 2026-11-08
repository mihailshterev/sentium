import { client } from "@/shared/api/client";
import type {
  AgentRecord,
  AgentRosterEntry,
  BuiltInAgent,
  CreateAgentPayload,
  ToolDescriptor,
  UpdateAgentPayload,
} from "../types/agents";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/praxis";

export const fetchAgentsPaged = (page = 1, pageSize = 100): Promise<PagedResponse<AgentRecord>> =>
  client.get<PagedResponse<AgentRecord>>(`${BASE}/agents?page=${page}&pageSize=${pageSize}`);

export const createAgent = (payload: CreateAgentPayload) => client.post<AgentRecord>(`${BASE}/agents`, payload);

export const updateAgent = ({ id, ...payload }: UpdateAgentPayload) =>
  client.put<AgentRecord>(`${BASE}/agents/${id}`, { id, ...payload });

export const deleteAgent = (id: string) => client.delete<void>(`${BASE}/agents/${id}`);

export const fetchAgentRoster = () => client.get<AgentRosterEntry[]>(`${BASE}/agents/roster`);

export const fetchToolCatalog = () => client.get<ToolDescriptor[]>(`${BASE}/tools`);

export const fetchBuiltInAgents = () => client.get<BuiltInAgent[]>(`${BASE}/agents/built-in`);

export const updateBuiltInAgentTools = (name: string, tools: string[] | null) =>
  client.put<BuiltInAgent>(`${BASE}/agents/built-in/${encodeURIComponent(name)}/tools`, { tools });

export const resetBuiltInAgentTools = (name: string) =>
  client.delete<BuiltInAgent>(`${BASE}/agents/built-in/${encodeURIComponent(name)}/tools`);
