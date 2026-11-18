import type { EvalCheckType } from "./eval-suites";

export type EvaluationStatus = "Queued" | "Running" | "Completed" | "Failed" | "Cancelled";

export type CheckOutcomeStatus = "Passed" | "Failed" | "Skipped";

export interface EvaluationSummary {
  id: string;
  suiteId: string;
  suiteName: string;
  suiteVersion: number;
  agentName: string;
  /** The Benchmark this Evaluation is part of, if any. */
  benchmarkId: string | null;
  model: string;
  /** The Judge frozen at start; null when the suite version has no quality Checks. */
  judgeModel: string | null;
  repetitions: number;
  status: EvaluationStatus;
  error: string | null;
  caseCount: number;
  completedCaseCount: number;
  passedCaseCount: number;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
}

export interface TranscriptMessage {
  role: "user" | "assistant";
  text: string;
}

export interface RecordedToolCall {
  toolName: string;
  arguments: unknown;
  result: string;
}

export interface CheckOutcome {
  type: EvalCheckType;
  status: CheckOutcomeStatus;
  /** For a quality Check, the Judge's reasoning. */
  reason: string | null;
  /** The Judge's 1-5 score; null for deterministic Checks or when the Judge gave none. */
  score: number | null;
  /** The score a quality Check needed to pass. */
  threshold: number | null;
}

export interface EvaluationRepetition {
  number: number;
  passed: boolean;
  transcript: TranscriptMessage[];
  toolCalls: RecordedToolCall[];
  checks: CheckOutcome[];
  latencyMs: number;
  inputTokens: number | null;
  outputTokens: number | null;
  tokensPerSecond: number | null;
  error: string | null;
}

export interface EvaluationCase {
  caseId: string;
  position: number;
  name: string | null;
  prompt: string;
  checks: EvalCheckType[];
  /** null until the case is graded. */
  passed: boolean | null;
  passedRepetitions: number;
  repetitions: EvaluationRepetition[];
}

export interface Evaluation {
  summary: EvaluationSummary;
  cases: EvaluationCase[];
  /** Whether the evaluated model is no longer served; null when the model catalog was unavailable. */
  modelDeleted: boolean | null;
}

export interface StartEvaluationPayload {
  suiteId: string;
  suiteVersion: number | null;
  agentName: string;
  model: string | null;
  repetitions: number;
}

export const isActive = (status: EvaluationStatus) => status === "Queued" || status === "Running";
