import type { SystemMetrics } from "@/features/system/types/system";
import type {
  Incident,
  ServiceHealthDetail,
  ServiceHealthStatus,
  SystemHealthOverview,
  VigilConfig,
} from "@/features/vigil/types/serviceHealth";
import { client } from "@/shared/api/client";

export const fetchSystemMetrics = async (): Promise<SystemMetrics> => {
  return client.get<SystemMetrics>("/vigil/system/metrics");
};

export const fetchServiceHealth = async (): Promise<ServiceHealthStatus[]> => {
  return client.get<ServiceHealthStatus[]>("/vigil/status");
};

export const fetchServiceHealthDetail = async (name: string): Promise<ServiceHealthDetail> => {
  return client.get<ServiceHealthDetail>(`/vigil/status/${encodeURIComponent(name)}`);
};

export const fetchSystemOverview = async (): Promise<SystemHealthOverview> => {
  return client.get<SystemHealthOverview>("/vigil/status/overview");
};

export const fetchIncidents = async (): Promise<Incident[]> => {
  return client.get<Incident[]>("/vigil/incidents");
};

interface SettingsEnvelope<T> {
  key: string;
  value: T;
  updatedAt: string;
  updatedBy: string | null;
}

export const fetchVigilConfig = async (): Promise<VigilConfig> =>
  client.get<SettingsEnvelope<VigilConfig>>("/registry/settings/vigil").then((e) => e.value);

export const updateVigilConfig = async (payload: VigilConfig): Promise<VigilConfig> =>
  client.put<SettingsEnvelope<VigilConfig>>("/registry/settings/vigil", payload).then((e) => e.value);
