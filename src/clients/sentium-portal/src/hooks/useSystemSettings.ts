import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchSystemSettings, updateSystemSettings } from "../services/agentRuntime.service";
import type { UpdateSystemSettingsPayload } from "../types/agentConfig";

const SETTINGS_KEY = ["system-settings"] as const;

export const useSystemSettings = () => {
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: fetchSystemSettings,
    staleTime: 30_000,
    retry: 1,
  });

  const mutation = useMutation({
    mutationFn: (payload: UpdateSystemSettingsPayload) => updateSystemSettings(payload),
    onSuccess: (updated) => {
      qc.setQueryData(SETTINGS_KEY, updated);
    },
  });

  return {
    settings: query.data,
    isLoading: query.isLoading,
    error: query.error,
    save: mutation.mutate,
    isSaving: mutation.isPending,
    isSaveSuccess: mutation.isSuccess,
    isSaveError: mutation.isError,
    saveError: mutation.error,
    resetSave: mutation.reset,
  };
};
