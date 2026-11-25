import { useQuery } from "@tanstack/react-query";
import { sandboxQueries } from "@/features/sandbox/api/sandbox.queries";

export const useSandboxExecution = (jobId?: string) => {
  const { data, isLoading, isFetching, error, refetch } = useQuery({
    ...sandboxQueries.execution(jobId ?? ""),
    enabled: !!jobId,
    retry: false,
  });

  return { execution: data ?? null, isLoading, isFetching, error, refetch };
};
