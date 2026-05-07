import type { SystemMetrics } from "../types/system";
import type { ServiceHealthStatus } from "../types/serviceHealth";
import { client } from "../api/client";

export const fetchSystemMetrics = async (): Promise<SystemMetrics> => {
  return client.get<SystemMetrics>("/watchdog/system/metrics");
};

export const fetchServiceHealth = async (): Promise<ServiceHealthStatus[]> => {
  return client.get<ServiceHealthStatus[]>("/watchdog/status");
};
