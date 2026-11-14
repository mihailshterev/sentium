import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchSettingsGroup, settingsQueryKey, updateSettingsGroup } from "@/features/registry";
import type { PdpSettings, UpdatePdpSettingsPayload } from "@/features/sentinel/types/sentinel";

/** The Registry `pdp` group, through the same cache entry the Settings page edits. */
export const useSentinelSettings = () => {
  const queryClient = useQueryClient();
  const queryKey = settingsQueryKey("pdp");

  const {
    data: settings,
    isLoading,
    error,
  } = useQuery({
    queryKey,
    queryFn: () => fetchSettingsGroup<PdpSettings>("pdp"),
    select: (envelope) => envelope.value,
    retry: false,
    staleTime: 10_000,
  });

  const { mutate: updateSettings, isPending: isUpdating } = useMutation({
    mutationFn: (payload: UpdatePdpSettingsPayload) => updateSettingsGroup<PdpSettings>("pdp", payload),
    onSuccess: (updated) => {
      queryClient.setQueryData(queryKey, updated);
    },
  });

  return { settings, isLoading, isUpdating, error, updateSettings };
};
