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

/** The household's calendar defaults (global, Sovereign only). Times are `HH:mm:ss`. */
export interface HouseholdCalendar {
  timeZone: string;
  dayStart: string;
}

/** A user's own calendar settings; null falls back to the household's. */
export interface UserCalendar {
  timeZone: string | null;
  dayStart: string | null;
}

export const fetchHouseholdCalendar = async (): Promise<SettingsEnvelope<HouseholdCalendar>> =>
  client.get<SettingsEnvelope<HouseholdCalendar>>(`${BASE}/settings/calendar`);

export const updateHouseholdCalendar = async (value: HouseholdCalendar): Promise<SettingsEnvelope<HouseholdCalendar>> =>
  client.put<SettingsEnvelope<HouseholdCalendar>>(`${BASE}/settings/calendar`, value);

export const fetchUserCalendar = async (): Promise<SettingsEnvelope<UserCalendar>> =>
  client.get<SettingsEnvelope<UserCalendar>>(`${BASE}/settings/calendar-user`);

export const updateUserCalendar = async (value: UserCalendar): Promise<SettingsEnvelope<UserCalendar>> =>
  client.put<SettingsEnvelope<UserCalendar>>(`${BASE}/settings/calendar-user`, value);
