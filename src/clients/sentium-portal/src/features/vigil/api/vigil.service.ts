import type { SystemMetrics } from "@/features/system/types/system";
import type {
  Incident,
  ServiceHealthDetail,
  ServiceHealthStatus,
  SystemHealthOverview,
} from "@/features/vigil/types/serviceHealth";
import type { InferenceOverview, InferenceRange } from "@/features/vigil/types/inference";
import { client } from "@/shared/api/client";
import { path, withQuery } from "@/shared/api/url";

export const fetchSystemMetrics = (): Promise<SystemMetrics> => client.get<SystemMetrics>("/vigil/system/metrics");

export const fetchServiceHealth = (): Promise<ServiceHealthStatus[]> =>
  client.get<ServiceHealthStatus[]>("/vigil/status");

export const fetchServiceHealthDetail = (name: string): Promise<ServiceHealthDetail> =>
  client.get<ServiceHealthDetail>(path`/vigil/status/${name}`);

export const fetchSystemOverview = (): Promise<SystemHealthOverview> =>
  client.get<SystemHealthOverview>("/vigil/status/overview");

export const fetchIncidents = (): Promise<Incident[]> => client.get<Incident[]>("/vigil/incidents");

export const fetchInferenceOverview = (range: InferenceRange): Promise<InferenceOverview> =>
  client.get<InferenceOverview>(withQuery("/vigil/inference", { range }));
