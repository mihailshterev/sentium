import { client } from "@/shared/api/client";
import type { AgentRecord, CreateAgentPayload, UpdateAgentPayload } from "../types/agents";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/agent-runtime";

export const fetchAgentsPaged = (page = 1, pageSize = 100): Promise<PagedResponse<AgentRecord>> =>
  client.get<PagedResponse<AgentRecord>>(`${BASE}/agents?page=${page}&pageSize=${pageSize}`);

export const createAgent = (payload: CreateAgentPayload) => client.post<AgentRecord>(`${BASE}/agents`, payload);

export const updateAgent = ({ id, ...payload }: UpdateAgentPayload) =>
  client.put<AgentRecord>(`${BASE}/agents/${id}`, { id, ...payload });

export const deleteAgent = (id: string) => client.delete<void>(`${BASE}/agents/${id}`);
