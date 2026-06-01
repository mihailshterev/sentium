import { client, BASE_URL } from "../api/client";
import type { PagedResponse } from "../types/pagination";
import type { SandboxExecutionLog, SandboxLanguage, SandboxStats, SandboxStatusFilter } from "../types/sandbox";

const BASE = "/sandbox";

export interface FetchExecutionsParams {
  page: number;
  pageSize: number;
  status?: SandboxStatusFilter;
  language?: SandboxLanguage;
  search?: string;
}

export const fetchExecutions = (params: FetchExecutionsParams): Promise<PagedResponse<SandboxExecutionLog>> => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  });
  if (params.status) {
    query.set("status", params.status);
  }
  if (params.language) {
    query.set("language", params.language);
  }
  if (params.search?.trim()) {
    query.set("search", params.search.trim());
  }
  return client.get<PagedResponse<SandboxExecutionLog>>(`${BASE}/executions?${query.toString()}`);
};

export const fetchExecution = (jobId: string): Promise<SandboxExecutionLog> =>
  client.get<SandboxExecutionLog>(`${BASE}/executions/${jobId}`);

export const fetchSandboxStats = (): Promise<SandboxStats> => client.get<SandboxStats>(`${BASE}/executions/stats`);

export const getArtifactUrl = (downloadPath: string): string => `${BASE_URL}${BASE}/artifacts/${downloadPath}`;
