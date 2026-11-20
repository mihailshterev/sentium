import { client } from "@/shared/api/client";
import type { SettingsEnvelope, SettingsGroupValue, SettingsKey, SettingsSectionSchema } from "../types/settings";

const BASE = "/registry/settings";

/** The sections the caller may edit: personal ones, plus household ones for a Sovereign. */
export const fetchSettingsSchema = (): Promise<SettingsSectionSchema[]> =>
  client.get<SettingsSectionSchema[]>(`${BASE}/schema`);

export const fetchSettingsGroup = <T = SettingsGroupValue>(key: SettingsKey): Promise<SettingsEnvelope<T>> =>
  client.get<SettingsEnvelope<T>>(`${BASE}/${key}`);

/** The server merges the patch into the stored group, so only changed fields need sending. */
export const updateSettingsGroup = <T = SettingsGroupValue>(
  key: SettingsKey,
  patch: Partial<T>,
): Promise<SettingsEnvelope<T>> => client.put<SettingsEnvelope<T>>(`${BASE}/${key}`, patch);
