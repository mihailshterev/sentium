import { client, BASE_URL } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { PagedResponse } from "@/shared/types/pagination";
import type {
  SandboxExecutionLog,
  SandboxLanguage,
  SandboxStats,
  SandboxStatusFilter,
} from "@/features/sandbox/types/sandbox";

const BASE = "/sandbox";
const url = pathIn(BASE);

export interface FetchExecutionsParams {
  page: number;
  pageSize: number;
  status?: SandboxStatusFilter;
  language?: SandboxLanguage;
  search?: string;
}

export const fetchExecutions = ({
  page,
  pageSize,
  status,
  language,
  search,
}: FetchExecutionsParams): Promise<PagedResponse<SandboxExecutionLog>> =>
  client.get<PagedResponse<SandboxExecutionLog>>(
    withQuery(`${BASE}/executions`, { page, pageSize, status, language, search: search?.trim() }),
  );

export const fetchExecution = (jobId: string): Promise<SandboxExecutionLog> =>
  client.get<SandboxExecutionLog>(url`/executions/${jobId}`);

export const fetchSandboxStats = (): Promise<SandboxStats> => client.get<SandboxStats>(`${BASE}/executions/stats`);

export const getArtifactUrl = (downloadPath: string): string => `${BASE_URL}${BASE}/artifacts/${downloadPath}`;
