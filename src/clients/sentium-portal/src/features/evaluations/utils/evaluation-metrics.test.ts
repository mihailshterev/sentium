import { describe, expect, it } from "vitest";
import { caseMetrics, formatMs, formatNumber } from "./evaluation-metrics";
import type { EvaluationRepetition } from "../types/evaluations";

const rep = (latencyMs: number, outputTokens: number | null, tokensPerSecond: number | null): EvaluationRepetition => ({
  number: 1,
  passed: true,
  transcript: [],
  toolCalls: [],
  checks: [],
  latencyMs,
  inputTokens: null,
  outputTokens,
  tokensPerSecond,
  error: null,
});

describe("evaluation metrics", () => {
  it("averages only the values that were reported", () => {
    const metrics = caseMetrics([rep(1000, 20, 10), rep(3000, null, null)]);

    expect(metrics.latencyMs).toBe(2000);
    expect(metrics.outputTokens).toBe(20);
    expect(metrics.tokensPerSecond).toBe(10);
    expect(metrics.inputTokens).toBeNull();
  });

  it("formats durations and missing values", () => {
    expect(formatMs(850)).toBe("850 ms");
    expect(formatMs(2400)).toBe("2.4 s");
    expect(formatMs(null)).toBe("–");
    expect(formatNumber(12.34)).toBe("12.3");
    expect(formatNumber(null)).toBe("–");
  });
});
