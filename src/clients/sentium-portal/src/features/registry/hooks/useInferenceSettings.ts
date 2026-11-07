import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  fetchInferenceSettings,
  updateInferenceSettings,
  type InferenceSettings,
} from "@/features/registry/api/registry.service";

const SETTINGS_KEY = ["settings-inference"] as const;

export const useInferenceSettings = (enabled = true) => {
  const qc = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: fetchInferenceSettings,
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
    mutationFn: (payload: InferenceSettings) => updateInferenceSettings(payload),
    onSuccess: (updated) => qc.setQueryData(SETTINGS_KEY, updated),
  });

  return { settings: data, isLoading, error, save, isSaving, isSaveSuccess, isSaveError, saveError, resetSave };
};
