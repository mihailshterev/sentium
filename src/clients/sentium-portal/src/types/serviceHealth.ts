export type ServiceStatus = "Healthy" | "Unhealthy" | "Unknown";

export interface ServiceHealthStatus {
  serviceName: string;
  status: ServiceStatus;
  latencyMs: number;
  checkedAt: string;
  details?: string | null;
}
