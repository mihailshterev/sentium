export type ServiceStatus = "Healthy" | "Degraded" | "Unhealthy" | "Unknown";

export type ComponentKind = "Service" | "Infrastructure";

export interface HealthCheckEntry {
  name: string;
  status: string;
  description?: string | null;
  durationMs: number;
}

export interface ServiceHealthStatus {
  serviceName: string;
  kind: ComponentKind;
  status: ServiceStatus;
  latencyMs: number;
  checkedAt: string;
  details?: string | null;
  description?: string | null;
  checks: HealthCheckEntry[];
  uptimePercent: number;
  lastStateChange: string;
  consecutiveFailures: number;
}

export interface HealthSample {
  at: string;
  status: ServiceStatus;
  latencyMs: number;
}

export interface ServiceHealthDetail {
  status: ServiceHealthStatus;
  samples: HealthSample[];
}

export interface SystemHealthOverview {
  total: number;
  healthy: number;
  degraded: number;
  unhealthy: number;
  unknown: number;
  overallStatus: ServiceStatus;
  openIncidents: number;
  generatedAt: string;
}

export type IncidentStatus = "Open" | "Resolved";
export type IncidentSeverity = "Warning" | "Critical";

export interface Incident {
  id: string;
  target: string;
  kind: ComponentKind;
  severity: IncidentSeverity;
  status: IncidentStatus;
  openedAt: string;
  resolvedAt?: string | null;
  durationMs?: number | null;
  description?: string | null;
  lastObservedStatus: ServiceStatus;
}

export interface WatchdogConfig {
  pollIntervalSeconds: number;
  probeTimeoutSeconds: number;
  degradedLatencyMs: number;
  consecutiveFailuresToOpenIncident: number;
  sampleHistorySize: number;
}
