import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchVigilConfig, updateVigilConfig } from "@/features/vigil/api/vigil.service";
import type { VigilConfig } from "@/features/vigil/types/serviceHealth";

const CONFIG_KEY = ["vigil-config"] as const;

export function useVigilConfig(enabled: boolean) {
  const queryClient = useQueryClient();

  const {
    data: config,
    isLoading,
    error,
  } = useQuery<VigilConfig>({
    queryKey: CONFIG_KEY,
    queryFn: fetchVigilConfig,
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
    mutationFn: (payload: VigilConfig) => updateVigilConfig(payload),
    onSuccess: (updated) => {
      queryClient.setQueryData(CONFIG_KEY, updated);
    },
  });

  return { config, isLoading, error, saveConfig, isSaving, isSaveSuccess, isSaveError, saveError, resetSave };
}


