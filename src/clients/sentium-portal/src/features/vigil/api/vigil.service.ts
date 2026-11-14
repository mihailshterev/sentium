import type { SystemMetrics } from "@/features/system/types/system";
import type {
  Incident,
  ServiceHealthDetail,
  ServiceHealthStatus,
  SystemHealthOverview,
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
