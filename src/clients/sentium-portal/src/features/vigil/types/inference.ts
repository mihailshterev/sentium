export const INFERENCE_RANGES = ["1h", "6h", "24h", "7d", "30d"] as const;

export type InferenceRange = (typeof INFERENCE_RANGES)[number];

export type InferenceServer = "Chat" | "Embedding";

export interface InferenceModelState {
  server: InferenceServer;
  model: string;
  status: string;
  loadFailed: boolean;
  requestsProcessing: number;
  requestsDeferred: number;
  updatedAt: string;
}

/** One bucket of a model's series; throughputs are null when the model did no such work in the bucket. */
export interface InferencePoint {
  at: string;
  promptTokensPerSecond: number | null;
  generationTokensPerSecond: number | null;
  promptTokens: number;
  generatedTokens: number;
  peakRequestsProcessing: number;
  peakRequestsDeferred: number;
  busyPercent: number;
}

/** A model's buckets in time order. Missing buckets mean no data, not zero. */
export interface InferenceSeries {
  server: InferenceServer;
  model: string;
  points: InferencePoint[];
}

export interface InferenceUsage {
  caller: string;
  model: string;
  operation: string;
  requests: number;
  errors: number;
  inputTokens: number;
  outputTokens: number;
  averageDurationSeconds: number | null;
  averageTimeToFirstTokenSeconds: number | null;
}

export interface ModelLoadEvent {
  server: InferenceServer;
  model: string;
  startedAt: string;
  completedAt: string;
  durationSeconds: number;
  succeeded: boolean;
}

export interface InferenceOverview {
  range: InferenceRange;
  stepMinutes: number;
  from: string;
  to: string;
  models: InferenceModelState[];
  series: InferenceSeries[];
  usage: InferenceUsage[];
  loads: ModelLoadEvent[];
}
