import type { HarnessSettings, Settings, UpdateSettingsPayload } from "../types/agentConfig";
import { client } from "../api/client";

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

export interface OllamaSettings {
  defaultModel: string;
  agentTemperature: number;
  agentContextWindow: number;
}

export const fetchOllamaSettings = async (): Promise<SettingsEnvelope<OllamaSettings>> =>
  client.get<SettingsEnvelope<OllamaSettings>>(`${BASE}/settings/ollama`);

export const updateOllamaSettings = async (value: OllamaSettings): Promise<SettingsEnvelope<OllamaSettings>> =>
  client.put<SettingsEnvelope<OllamaSettings>>(`${BASE}/settings/ollama`, value);
