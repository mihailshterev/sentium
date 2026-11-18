import type { Evaluation, EvaluationSummary } from "./evaluations";

export interface BenchmarkSummary {
  id: string;
  suiteId: string;
  suiteName: string;
  suiteVersion: number;
  agentName: string;
  repetitions: number;
  /** The Judge shared by every Evaluation; null when the suite version has no quality Checks. */
  judgeModel: string | null;
  createdAt: string;
  evaluations: EvaluationSummary[];
}

export interface Benchmark extends Omit<BenchmarkSummary, "evaluations"> {
  evaluations: Evaluation[];
}

export interface StartBenchmarkPayload {
  suiteId: string;
  suiteVersion: number | null;
  agentName: string;
  models: string[];
  repetitions: number;
}

export const MIN_BENCHMARK_MODELS = 2;
export const MAX_BENCHMARK_MODELS = 6;
