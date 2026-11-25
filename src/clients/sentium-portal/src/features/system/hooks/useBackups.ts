import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { startBackup } from "@/features/system/api/backups.service";
import { backupKeys, backupQueries } from "@/features/system/api/backups.queries";

const IDLE_POLL_INTERVAL = 30_000;
const RUNNING_POLL_INTERVAL = 3_000;

export function useBackups() {
  const queryClient = useQueryClient();

  const { data, isLoading, error } = useQuery({
    ...backupQueries.overview(),
    refetchInterval: (query) => (query.state.data?.status?.running ? RUNNING_POLL_INTERVAL : IDLE_POLL_INTERVAL),
    retry: false,
  });

  const start = useMutation({
    mutationFn: startBackup,
    onSettled: () => queryClient.invalidateQueries({ queryKey: backupKeys.overview() }),
  });

  return { overview: data, isLoading, error, start };
}
