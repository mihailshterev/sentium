import type { EvalCheckType } from "../types/eval-suites";
import type { Evaluation, EvaluationCase, EvaluationStatus } from "../types/evaluations";
import { caseMetrics } from "./evaluation-metrics";

/** One model's column: its Evaluation and the averages over every graded repetition. */
export interface ModelColumn {
  evaluationId: string;
  model: string;
  status: EvaluationStatus;
  modelDeleted: boolean | null;
  passedCases: number;
  gradedCases: number;
  caseCount: number;
  /** Passed cases over graded cases, 0-1; null until a case is graded. */
  passRate: number | null;
  latencyMs: number | null;
  tokensPerSecond: number | null;
  inputTokens: number | null;
  outputTokens: number | null;
}

export interface CheckCell {
  /** Repetitions in which this Check passed, out of `graded`. */
  passed: number;
  graded: number;
  /** Mean Judge score of a quality Check; null for deterministic Checks or when no score came back. */
  averageScore: number | null;
}

export interface CaseCell {
  passed: boolean | null;
  passedRepetitions: number;
  repetitions: number;
  checks: Partial<Record<EvalCheckType, CheckCell>>;
}

export interface CaseRow {
  caseId: string;
  name: string | null;
  prompt: string;
  checks: EvalCheckType[];
  /** One cell per model, in column order. */
  cells: CaseCell[];
}

export interface Comparison {
  columns: ModelColumn[];
  cases: CaseRow[];
  /** Column index that is best on each measure, when one is clearly best (no ties, at least two values). */
  best: { passRate: number | null; latencyMs: number | null; tokensPerSecond: number | null };
}

const mean = (values: number[]) => (values.length === 0 ? null : values.reduce((a, b) => a + b, 0) / values.length);

function column(evaluation: Evaluation): ModelColumn {
  const graded = evaluation.cases.filter((c) => c.passed !== null);
  const passed = graded.filter((c) => c.passed).length;
  const metrics = caseMetrics(evaluation.cases.flatMap((c) => c.repetitions));

  return {
    evaluationId: evaluation.summary.id,
    model: evaluation.summary.model,
    status: evaluation.summary.status,
    modelDeleted: evaluation.modelDeleted,
    passedCases: passed,
    gradedCases: graded.length,
    caseCount: evaluation.summary.caseCount,
    passRate: graded.length === 0 ? null : passed / graded.length,
    ...metrics,
  };
}

function cell(evalCase: EvaluationCase | undefined): CaseCell {
  if (!evalCase) {
    return { passed: null, passedRepetitions: 0, repetitions: 0, checks: {} };
  }

  const checks: Partial<Record<EvalCheckType, CheckCell>> = {};
  for (const type of evalCase.checks) {
    const outcomes = evalCase.repetitions.flatMap((r) => r.checks.filter((c) => c.type === type && c.status !== "Skipped"));
    checks[type] = {
      passed: outcomes.filter((o) => o.status === "Passed").length,
      graded: outcomes.length,
      averageScore: mean(outcomes.flatMap((o) => (o.score === null ? [] : [o.score]))),
    };
  }

  return {
    passed: evalCase.passed,
    passedRepetitions: evalCase.passedRepetitions,
    repetitions: evalCase.repetitions.length,
    checks,
  };
}

/** The index of the single best value, or null when fewer than two columns have one or the best is tied. */
function bestIndex(values: (number | null)[], higherIsBetter: boolean): number | null {
  const known = values.flatMap((v, i) => (v === null ? [] : [{ v, i }]));
  if (known.length < 2) {
    return null;
  }
  const sorted = [...known].sort((a, b) => (higherIsBetter ? b.v - a.v : a.v - b.v));
  return sorted[0].v === sorted[1].v ? null : sorted[0].i;
}

/**
 * Lines the Evaluations of one Benchmark up side by side. They share one suite version, so the cases of the
 * first Evaluation are the rows and every other Evaluation is matched to them by case id.
 */
export function buildComparison(evaluations: Evaluation[]): Comparison {
  const columns = evaluations.map(column);
  const rows = evaluations[0]?.cases ?? [];

  const cases = rows.map((row) => ({
    caseId: row.caseId,
    name: row.name,
    prompt: row.prompt,
    checks: row.checks,
    cells: evaluations.map((e) => cell(e.cases.find((c) => c.caseId === row.caseId))),
  }));

  return {
    columns,
    cases,
    best: {
      passRate: bestIndex(columns.map((c) => c.passRate), true),
      latencyMs: bestIndex(columns.map((c) => c.latencyMs), false),
      tokensPerSecond: bestIndex(columns.map((c) => c.tokensPerSecond), true),
    },
  };
}
