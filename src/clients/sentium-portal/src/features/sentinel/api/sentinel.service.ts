import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { AuditRecord, AuditStats } from "@/features/sentinel/types/sentinel";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/sentinel";
const url = pathIn(BASE);

export const fetchAuditLog = (page = 1, pageSize = 20): Promise<PagedResponse<AuditRecord>> =>
  client.get<PagedResponse<AuditRecord>>(withQuery(`${BASE}/policy/audit`, { page, pageSize }));

export const fetchAuditByAgent = (agentId: string, page = 1, pageSize = 20): Promise<PagedResponse<AuditRecord>> =>
  client.get<PagedResponse<AuditRecord>>(withQuery(url`/policy/audit/agent/${agentId}`, { page, pageSize }));

export const fetchAuditStats = (): Promise<AuditStats> => client.get<AuditStats>(`${BASE}/policy/audit/stats`);
