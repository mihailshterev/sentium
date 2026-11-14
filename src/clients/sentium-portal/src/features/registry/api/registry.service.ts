import { client } from "@/shared/api/client";
import type { SettingsEnvelope, SettingsGroupValue, SettingsKey } from "../types/settings";

const BASE = "/registry/settings";

export const fetchSettingsGroup = <T = SettingsGroupValue>(key: SettingsKey): Promise<SettingsEnvelope<T>> =>
  client.get<SettingsEnvelope<T>>(`${BASE}/${key}`);

/** The server merges the patch into the stored group, so only changed fields need sending. */
export const updateSettingsGroup = <T = SettingsGroupValue>(
  key: SettingsKey,
  patch: Partial<T>,
): Promise<SettingsEnvelope<T>> => client.put<SettingsEnvelope<T>>(`${BASE}/${key}`, patch);
