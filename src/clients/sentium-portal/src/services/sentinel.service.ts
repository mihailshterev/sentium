import { client } from "../api/client";
import type { AuditRecord, AuditStats, PdpSettings, UpdatePdpSettingsPayload } from "../types/sentinel";

const BASE = "/sentinel";

interface SettingsEnvelope<T> {
  key: string;
  value: T;
  updatedAt: string;
  updatedBy: string | null;
}

export const fetchAuditLog = (count = 100): Promise<AuditRecord[]> =>
  client.get<AuditRecord[]>(`${BASE}/policy/audit?count=${count}`);

export const fetchAuditByAgent = (agentId: string, count = 50): Promise<AuditRecord[]> =>
  client.get<AuditRecord[]>(`${BASE}/policy/audit/agent/${encodeURIComponent(agentId)}?count=${count}`);

export const fetchAuditStats = (): Promise<AuditStats> => client.get<AuditStats>(`${BASE}/policy/audit/stats`);

export const fetchPdpSettings = (): Promise<PdpSettings> =>
  client.get<SettingsEnvelope<PdpSettings>>("/registry/settings/pdp").then((e) => e.value);

export const updatePdpSettings = (payload: UpdatePdpSettingsPayload): Promise<PdpSettings> =>
  client.put<SettingsEnvelope<PdpSettings>>("/registry/settings/pdp", payload).then((e) => e.value);
