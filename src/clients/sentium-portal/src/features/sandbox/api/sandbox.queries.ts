import { queryOptions } from "@tanstack/react-query";
import { fetchExecution, fetchSandboxStats } from "./sandbox.service";
import type { SandboxLanguage, SandboxStatusFilter } from "../types/sandbox";

export const sandboxKeys = {
  executions: () => ["sandbox-executions"] as const,
  executionList: (
    pageSize: number,
    status: SandboxStatusFilter | null,
    language: SandboxLanguage | null,
    search: string,
  ) => [...sandboxKeys.executions(), pageSize, status, language, search] as const,
  /** Outside `executions()`: the realtime refresh of the list leaves an open execution alone. */
  execution: (jobId: string) => ["sandbox-execution", jobId] as const,
  stats: () => ["sandbox-stats"] as const,
};

export const sandboxQueries = {
  execution: (jobId: string) =>
    queryOptions({ queryKey: sandboxKeys.execution(jobId), queryFn: () => fetchExecution(jobId) }),
  stats: () => queryOptions({ queryKey: sandboxKeys.stats(), queryFn: () => fetchSandboxStats() }),
};
