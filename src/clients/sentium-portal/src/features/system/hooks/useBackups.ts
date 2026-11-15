import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchBackups, startBackup } from "@/features/system/api/backups.service";
import type { BackupsOverview } from "@/features/system/types/backup";

export const BACKUPS_KEY = ["system", "backups"] as const;

const IDLE_POLL_INTERVAL = 30_000;
const RUNNING_POLL_INTERVAL = 3_000;

export function useBackups() {
  const queryClient = useQueryClient();

  const { data, isLoading, error } = useQuery<BackupsOverview>({
    queryKey: BACKUPS_KEY,
    queryFn: fetchBackups,
    refetchInterval: (query) => (query.state.data?.status?.running ? RUNNING_POLL_INTERVAL : IDLE_POLL_INTERVAL),
    retry: false,
  });

  const start = useMutation({
    mutationFn: startBackup,
    onSettled: () => queryClient.invalidateQueries({ queryKey: BACKUPS_KEY }),
  });

  return { overview: data, isLoading, error, start };
}
