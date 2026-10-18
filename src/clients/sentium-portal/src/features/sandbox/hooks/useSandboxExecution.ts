import { useQuery } from "@tanstack/react-query";
import { fetchExecution } from "@/features/sandbox/api/sandbox.service";
import type { SandboxExecutionLog } from "@/features/sandbox/types/sandbox";

const POLL_INTERVAL = 5_000;

export const useSandboxExecution = (jobId?: string) => {
  const { data, isLoading, isFetching, error, refetch } = useQuery<SandboxExecutionLog>({
    queryKey: ["sandbox-execution", jobId],
    queryFn: () => fetchExecution(jobId!),
    enabled: !!jobId,
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { execution: data ?? null, isLoading, isFetching, error, refetch };
};
