import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchPdpSettings, updatePdpSettings } from "../services/sentinel.service";
import type { PdpSettings, UpdatePdpSettingsPayload } from "../types/sentinel";

const SETTINGS_KEY = ["sentinel-settings"] as const;

export const useSentinelSettings = () => {
  const queryClient = useQueryClient();

  const {
    data: settings,
    isLoading,
    error,
  } = useQuery<PdpSettings>({
    queryKey: SETTINGS_KEY,
    queryFn: fetchPdpSettings,
    retry: false,
    staleTime: 10_000,
  });

  const { mutate: updateSettings, isPending: isUpdating } = useMutation({
    mutationFn: (payload: UpdatePdpSettingsPayload) => updatePdpSettings(payload),
    onSuccess: (updated) => {
      queryClient.setQueryData(SETTINGS_KEY, updated);
    },
  });

  return { settings, isLoading, isUpdating, error, updateSettings };
};
