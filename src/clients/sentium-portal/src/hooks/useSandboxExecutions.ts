import { useQuery } from "@tanstack/react-query";
import { fetchExecutionLogs } from "../services/sandbox.service";
import type { SandboxExecutionLog } from "../types/sandbox";

const POLL_INTERVAL = 5_000;

export const useSandboxExecutions = (count = 100) => {
  const {
    data: executions = [],
    isLoading,
    error,
    refetch,
  } = useQuery<SandboxExecutionLog[]>({
    queryKey: ["sandbox-executions", count],
    queryFn: () => fetchExecutionLogs(count),
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { executions, isLoading, error, refetch };
};
