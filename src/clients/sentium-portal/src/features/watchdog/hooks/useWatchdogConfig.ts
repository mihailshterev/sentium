import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchWatchdogConfig, updateWatchdogConfig } from "@/features/watchdog/api/watchdog.service";
import type { WatchdogConfig } from "@/features/watchdog/types/serviceHealth";

const CONFIG_KEY = ["watchdog-config"] as const;

export function useWatchdogConfig(enabled: boolean) {
  const queryClient = useQueryClient();

  const {
    data: config,
    isLoading,
    error,
  } = useQuery<WatchdogConfig>({
    queryKey: CONFIG_KEY,
    queryFn: fetchWatchdogConfig,
    enabled,
    retry: false,
    staleTime: 10_000,
  });

  const {
    mutate: saveConfig,
    isPending: isSaving,
    isSuccess: isSaveSuccess,
    isError: isSaveError,
    error: saveError,
    reset: resetSave,
  } = useMutation({
    mutationFn: (payload: WatchdogConfig) => updateWatchdogConfig(payload),
    onSuccess: (updated) => {
      queryClient.setQueryData(CONFIG_KEY, updated);
    },
  });

  return { config, isLoading, error, saveConfig, isSaving, isSaveSuccess, isSaveError, saveError, resetSave };
}


