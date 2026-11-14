import { useMutation, useQueries, useQueryClient } from "@tanstack/react-query";
import { fetchSettingsGroup, updateSettingsGroup } from "../api/registry.service";
import type { SettingsEnvelope, SettingsGroupValue, SettingsKey } from "../types/settings";

/** Shared with feature pages that read a group too (e.g. Sentinel's PDP readout), so a save updates them all. */
export const settingsQueryKey = (key: SettingsKey) => ["registry-settings", key] as const;

export interface GroupPatch {
  key: SettingsKey;
  patch: SettingsGroupValue;
}

export interface GroupSaveResult {
  key: SettingsKey;
  error: Error | null;
}

export function useSettingsGroups(keys: SettingsKey[]) {
  const results = useQueries({
    queries: keys.map((key) => ({
      queryKey: settingsQueryKey(key),
      queryFn: () => fetchSettingsGroup(key),
      retry: false,
    })),
  });

  const envelopes: Partial<Record<SettingsKey, SettingsEnvelope<SettingsGroupValue>>> = {};
  const errors: Partial<Record<SettingsKey, Error>> = {};
  keys.forEach((key, i) => {
    const { data, error } = results[i];
    if (data) {
      envelopes[key] = data;
    }
    if (error) {
      errors[key] = error;
    }
  });

  return { envelopes, errors, isLoading: results.some((r) => r.isLoading) };
}

/** Saves several groups at once. Never rejects: each group reports its own outcome, so one failure keeps the rest. */
export function useSaveSettingsGroups() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (patches: GroupPatch[]) => Promise.all(patches.map((patch) => saveGroup(patch))),
  });

  async function saveGroup({ key, patch }: GroupPatch): Promise<GroupSaveResult> {
    try {
      qc.setQueryData(settingsQueryKey(key), await updateSettingsGroup(key, patch));
      return { key, error: null };
    } catch (error) {
      return { key, error: error instanceof Error ? error : new Error("Failed to save.") };
    }
  }
}
