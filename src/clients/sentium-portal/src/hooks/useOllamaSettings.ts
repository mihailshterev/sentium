import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchOllamaSettings, updateOllamaSettings, type OllamaSettings } from "../services/registry.service";

const SETTINGS_KEY = ["settings-ollama"] as const;

export const useOllamaSettings = (enabled = true) => {
  const qc = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: fetchOllamaSettings,
    enabled,
    retry: false,
  });

  const {
    mutate: save,
    isPending: isSaving,
    isSuccess: isSaveSuccess,
    isError: isSaveError,
    error: saveError,
    reset: resetSave,
  } = useMutation({
    mutationFn: (payload: OllamaSettings) => updateOllamaSettings(payload),
    onSuccess: (updated) => qc.setQueryData(SETTINGS_KEY, updated),
  });

  return { settings: data, isLoading, error, save, isSaving, isSaveSuccess, isSaveError, saveError, resetSave };
};
