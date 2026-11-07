import { client } from "@/shared/api/client";
import type { HarnessSettings, Settings, UpdateSettingsPayload } from "../types/settings";

const BASE = "/registry";

interface SettingsEnvelope<T> {
  key: string;
  value: T;
  updatedAt: string;
  updatedBy: string | null;
}

const toSettings = (env: SettingsEnvelope<HarnessSettings>): Settings => ({
  harness: env.value,
  updatedAt: env.updatedAt,
  updatedBy: env.updatedBy,
});

export const fetchSettings = async (): Promise<Settings> =>
  toSettings(await client.get<SettingsEnvelope<HarnessSettings>>(`${BASE}/settings/harness`));

export const updateSettings = async (payload: UpdateSettingsPayload): Promise<Settings> =>
  toSettings(await client.put<SettingsEnvelope<HarnessSettings>>(`${BASE}/settings/harness`, payload.harness));

export interface InferenceSettings {
  defaultModel: string;
  agentTemperature: number;
}

export const fetchInferenceSettings = async (): Promise<SettingsEnvelope<InferenceSettings>> =>
  client.get<SettingsEnvelope<InferenceSettings>>(`${BASE}/settings/inference`);

export const updateInferenceSettings = async (value: InferenceSettings): Promise<SettingsEnvelope<InferenceSettings>> =>
  client.put<SettingsEnvelope<InferenceSettings>>(`${BASE}/settings/inference`, value);
