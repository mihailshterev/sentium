import { useQuery } from "@tanstack/react-query";
import { fetchSandboxStats } from "../services/sandbox.service";
import type { SandboxStats } from "../types/sandbox";

const POLL_INTERVAL = 5_000;

export const useSandboxStats = () => {
  const { data, isLoading, error, refetch } = useQuery<SandboxStats>({
    queryKey: ["sandbox-stats"],
    queryFn: fetchSandboxStats,
    refetchInterval: POLL_INTERVAL,
    retry: false,
  });

  return { stats: data ?? null, isLoading, error, refetch };
};
