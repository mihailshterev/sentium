import { queryOptions } from "@tanstack/react-query";
import { fetchSettingsGroup, fetchSettingsSchema } from "./registry.service";
import type { SettingsGroupValue, SettingsKey } from "../types/settings";

/** Shared with feature pages that read a group too (e.g. Sentinel's PDP readout), so a save updates them all. */
export const settingsKeys = {
  group: (key: SettingsKey) => ["registry-settings", key] as const,
  schema: (isSovereign: boolean) => ["registry-settings-schema", isSovereign] as const,
};

export const settingsQueries = {
  group: <T = SettingsGroupValue>(key: SettingsKey) =>
    queryOptions({ queryKey: settingsKeys.group(key), queryFn: () => fetchSettingsGroup<T>(key) }),
  /** Only changes with a server release; keyed by role because household sections are listed to a Sovereign only. */
  schema: (isSovereign: boolean) =>
    queryOptions({ queryKey: settingsKeys.schema(isSovereign), queryFn: () => fetchSettingsSchema(), staleTime: Infinity }),
};
