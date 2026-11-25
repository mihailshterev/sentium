import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
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
const url = pathIn(BASE);

export const fetchAgentsPaged = (page = 1, pageSize = 100): Promise<PagedResponse<AgentRecord>> =>
  client.get<PagedResponse<AgentRecord>>(withQuery(`${BASE}/agents`, { page, pageSize }));

export const createAgent = (payload: CreateAgentPayload) => client.post<AgentRecord>(`${BASE}/agents`, payload);

export const updateAgent = ({ id, ...payload }: UpdateAgentPayload) =>
  client.put<void>(url`/agents/${id}`, { id, ...payload });

export const deleteAgent = (id: string) => client.delete<void>(url`/agents/${id}`);

export const fetchAgentRoster = () => client.get<AgentRosterEntry[]>(`${BASE}/agents/roster`);

export const fetchToolCatalog = () => client.get<ToolDescriptor[]>(`${BASE}/tools`);

export const fetchBuiltInAgents = () => client.get<BuiltInAgent[]>(`${BASE}/agents/built-in`);

export const updateBuiltInAgentTools = (name: string, tools: string[] | null) =>
  client.put<BuiltInAgent>(url`/agents/built-in/${name}/tools`, { tools });

export const resetBuiltInAgentTools = (name: string) =>
  client.delete<BuiltInAgent>(url`/agents/built-in/${name}/tools`);
