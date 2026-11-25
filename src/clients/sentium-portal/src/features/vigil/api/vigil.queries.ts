import { queryOptions } from "@tanstack/react-query";
import {
  fetchIncidents,
  fetchInferenceOverview,
  fetchServiceHealth,
  fetchSystemMetrics,
  fetchSystemOverview,
} from "./vigil.service";
import type { InferenceRange } from "../types/inference";

export const vigilKeys = {
  systemMetrics: () => ["system-metrics"] as const,
  serviceHealth: () => ["service-health"] as const,
  overview: () => ["vigil-overview"] as const,
  incidents: () => ["vigil-incidents"] as const,
  inference: (range: InferenceRange) => ["vigil", "inference", range] as const,
};

/** The Vigil stream patches or refetches health, overview and incidents as they change. */
export const vigilQueries = {
  systemMetrics: () => queryOptions({ queryKey: vigilKeys.systemMetrics(), queryFn: () => fetchSystemMetrics() }),
  serviceHealth: () => queryOptions({ queryKey: vigilKeys.serviceHealth(), queryFn: () => fetchServiceHealth() }),
  overview: () => queryOptions({ queryKey: vigilKeys.overview(), queryFn: () => fetchSystemOverview() }),
  incidents: () => queryOptions({ queryKey: vigilKeys.incidents(), queryFn: () => fetchIncidents() }),
  inference: (range: InferenceRange) =>
    queryOptions({ queryKey: vigilKeys.inference(range), queryFn: () => fetchInferenceOverview(range) }),
};
