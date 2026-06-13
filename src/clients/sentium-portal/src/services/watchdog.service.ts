import type { SystemMetrics } from "../types/system";
import type {
  Incident,
  ServiceHealthDetail,
  ServiceHealthStatus,
  SystemHealthOverview,
  WatchdogConfig,
} from "../types/serviceHealth";
import { client } from "../api/client";

export const fetchSystemMetrics = async (): Promise<SystemMetrics> => {
  return client.get<SystemMetrics>("/watchdog/system/metrics");
};

export const fetchServiceHealth = async (): Promise<ServiceHealthStatus[]> => {
  return client.get<ServiceHealthStatus[]>("/watchdog/status");
};

export const fetchServiceHealthDetail = async (name: string): Promise<ServiceHealthDetail> => {
  return client.get<ServiceHealthDetail>(`/watchdog/status/${encodeURIComponent(name)}`);
};

export const fetchSystemOverview = async (): Promise<SystemHealthOverview> => {
  return client.get<SystemHealthOverview>("/watchdog/status/overview");
};

export const fetchIncidents = async (): Promise<Incident[]> => {
  return client.get<Incident[]>("/watchdog/incidents");
};

interface SettingsEnvelope<T> {
  key: string;
  value: T;
  updatedAt: string;
  updatedBy: string | null;
}

export const fetchWatchdogConfig = async (): Promise<WatchdogConfig> =>
  client.get<SettingsEnvelope<WatchdogConfig>>("/registry/settings/watchdog").then((e) => e.value);

export const updateWatchdogConfig = async (payload: WatchdogConfig): Promise<WatchdogConfig> =>
  client.put<SettingsEnvelope<WatchdogConfig>>("/registry/settings/watchdog", payload).then((e) => e.value);
