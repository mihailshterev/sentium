import type { EvaluationRepetition } from "../types/evaluations";

export interface Metrics {
  latencyMs: number | null;
  inputTokens: number | null;
  outputTokens: number | null;
  tokensPerSecond: number | null;
}

const average = (values: (number | null)[]): number | null => {
  const known = values.filter((v): v is number => v !== null);
  return known.length === 0 ? null : known.reduce((sum, v) => sum + v, 0) / known.length;
};

/** Averages over repetitions, ignoring those where a value was not reported (null when none reported it). */
export function caseMetrics(repetitions: EvaluationRepetition[]): Metrics {
  return {
    latencyMs: average(repetitions.map((r) => r.latencyMs)),
    inputTokens: average(repetitions.map((r) => r.inputTokens)),
    outputTokens: average(repetitions.map((r) => r.outputTokens)),
    tokensPerSecond: average(repetitions.map((r) => r.tokensPerSecond)),
  };
}

export const formatMs = (ms: number | null): string => {
  if (ms === null) {
    return "–";
  }
  return ms >= 1000 ? `${(ms / 1000).toFixed(1)} s` : `${Math.round(ms)} ms`;
};

export const formatNumber = (value: number | null): string =>
  value === null ? "–" : value.toLocaleString("en-GB", { maximumFractionDigits: 1 });
