import { client } from "../api/client";
import type { AuditRecord, AuditStats, PdpSettings, UpdatePdpSettingsPayload } from "../types/sentinel";
import type { PagedResponse } from "../types/pagination";

const BASE = "/sentinel";

interface SettingsEnvelope<T> {
  key: string;
  value: T;
  updatedAt: string;
  updatedBy: string | null;
}

export const fetchAuditLog = (page = 1, pageSize = 20): Promise<PagedResponse<AuditRecord>> =>
  client.get<PagedResponse<AuditRecord>>(`${BASE}/policy/audit?page=${page}&pageSize=${pageSize}`);

export const fetchAuditByAgent = (agentId: string, page = 1, pageSize = 20): Promise<PagedResponse<AuditRecord>> =>
  client.get<PagedResponse<AuditRecord>>(
    `${BASE}/policy/audit/agent/${encodeURIComponent(agentId)}?page=${page}&pageSize=${pageSize}`,
  );

export const fetchAuditStats = (): Promise<AuditStats> => client.get<AuditStats>(`${BASE}/policy/audit/stats`);

export const fetchPdpSettings = (): Promise<PdpSettings> =>
  client.get<SettingsEnvelope<PdpSettings>>("/registry/settings/pdp").then((e) => e.value);

export const updatePdpSettings = (payload: UpdatePdpSettingsPayload): Promise<PdpSettings> =>
  client.put<SettingsEnvelope<PdpSettings>>("/registry/settings/pdp", payload).then((e) => e.value);
