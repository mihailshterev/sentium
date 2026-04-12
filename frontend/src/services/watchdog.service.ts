import type { SystemMetrics } from "../types/system";
import { client } from "../api/client";

export const fetchSystemMetrics = async (): Promise<SystemMetrics> => {
  return client.get<SystemMetrics>("/watchdog/system/metrics");
};
