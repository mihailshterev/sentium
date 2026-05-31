import type { Settings, UpdateSettingsPayload } from "../types/agentConfig";
import { client } from "../api/client";

const BASE = "/registry";

export const fetchSettings = (): Promise<Settings> => client.get<Settings>(`${BASE}/settings`);

export const updateSettings = (payload: UpdateSettingsPayload): Promise<Settings> =>
  client.put<Settings>(`${BASE}/settings`, payload);
