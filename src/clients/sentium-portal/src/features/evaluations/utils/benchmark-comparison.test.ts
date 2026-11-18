import { describe, expect, it } from "vitest";
import { buildComparison } from "./benchmark-comparison";
import type { CheckOutcome, Evaluation, EvaluationRepetition } from "../types/evaluations";

const rep = (passed: boolean, latencyMs: number, tokensPerSecond: number, checks: CheckOutcome[]): EvaluationRepetition => ({
  number: 1,
  passed,
  transcript: [],
  toolCalls: [],
  checks,
  latencyMs,
  inputTokens: 100,
  outputTokens: 20,
  tokensPerSecond,
  error: null,
});

const relevance = (score: number | null, passed: boolean): CheckOutcome => ({
  type: "Relevance",
  status: passed ? "Passed" : "Failed",
  reason: null,
  score,
  threshold: 4,
});

const evaluation = (model: string, cases: Evaluation["cases"], modelDeleted: boolean | null = false): Evaluation => ({
  summary: {
    id: `e-${model}`,
    suiteId: "s-1",
    suiteName: "Shopping",
    suiteVersion: 1,
    agentName: "ResearchAnalyst",
    benchmarkId: "b-1",
    model,
    judgeModel: "judge",
    repetitions: 2,
    status: "Completed",
    error: null,
    caseCount: 2,
    completedCaseCount: cases.filter((c) => c.passed !== null).length,
    passedCaseCount: cases.filter((c) => c.passed).length,
    createdAt: "",
    startedAt: null,
    finishedAt: null,
  },
  cases,
  modelDeleted,
});

const evalCase = (caseId: string, passed: boolean | null, repetitions: EvaluationRepetition[]) => ({
  caseId,
  position: 0,
  name: null,
  prompt: caseId,
  checks: ["Relevance" as const],
  passed,
  passedRepetitions: repetitions.filter((r) => r.passed).length,
  repetitions,
});

describe("buildComparison", () => {
  const fast = evaluation("fast", [
    evalCase("c-1", true, [rep(true, 1000, 30, [relevance(5, true)]), rep(true, 1000, 30, [relevance(4, true)])]),
    evalCase("c-2", false, [rep(false, 3000, 30, [relevance(2, false)]), rep(true, 3000, 30, [relevance(4, true)])]),
  ]);
  const slow = evaluation(
    "slow",
    [evalCase("c-1", true, [rep(true, 5000, 10, [relevance(4, true)]), rep(true, 5000, 10, [relevance(4, true)])]), evalCase("c-2", null, [])],
    true,
  );

  it("summarises every model's pass rate, speed and deletion", () => {
    const { columns } = buildComparison([fast, slow]);

    expect(columns.map((c) => [c.model, c.passedCases, c.gradedCases, c.passRate])).toEqual([
      ["fast", 1, 2, 0.5],
      ["slow", 1, 1, 1],
    ]);
    expect(columns[0].latencyMs).toBe(2000);
    expect(columns[1].tokensPerSecond).toBe(10);
    expect(columns[1].modelDeleted).toBe(true);
  });

  it("lines cases up by id with per-Check pass counts and mean scores", () => {
    const { cases } = buildComparison([fast, slow]);

    expect(cases.map((c) => c.caseId)).toEqual(["c-1", "c-2"]);
    expect(cases[0].cells[0].checks.Relevance).toEqual({ passed: 2, graded: 2, averageScore: 4.5 });
    expect(cases[1].cells[0].checks.Relevance).toEqual({ passed: 1, graded: 2, averageScore: 3 });
    expect(cases[1].cells[1].passed).toBeNull();
  });

  it("marks the single best column per measure", () => {
    const { best } = buildComparison([fast, slow]);

    expect(best).toEqual({ passRate: 1, latencyMs: 0, tokensPerSecond: 0 });
  });
});
