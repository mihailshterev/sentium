import { client } from "@/shared/api/client";
import type { AuditRecord, AuditStats } from "@/features/sentinel/types/sentinel";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/sentinel";

export const fetchAuditLog = (page = 1, pageSize = 20): Promise<PagedResponse<AuditRecord>> =>
  client.get<PagedResponse<AuditRecord>>(`${BASE}/policy/audit?page=${page}&pageSize=${pageSize}`);

export const fetchAuditByAgent = (agentId: string, page = 1, pageSize = 20): Promise<PagedResponse<AuditRecord>> =>
  client.get<PagedResponse<AuditRecord>>(
    `${BASE}/policy/audit/agent/${encodeURIComponent(agentId)}?page=${page}&pageSize=${pageSize}`,
  );

export const fetchAuditStats = (): Promise<AuditStats> => client.get<AuditStats>(`${BASE}/policy/audit/stats`);
