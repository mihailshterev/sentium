import { describe, expect, it } from "vitest";
import { caseDraftErrors, caseLabel, emptyCaseDraft, toCaseDraft, toCaseInput } from "./case-draft";
import type { EvalCase } from "../types/eval-suites";

const CASE: EvalCase = {
  id: "c-1",
  name: null,
  prompt: "What do we need?",
  priorTurns: [{ role: "User", text: "Hi" }],
  expectedAnswer: "milk",
  expectedToolCalls: [{ toolName: "knowledge_base_search", arguments: { query: "milk" } }],
  cannedToolResults: [{ toolName: "knowledge_base_search", result: "milk, eggs" }],
  checks: [
    { type: "ToolArgumentsMatch", threshold: null },
    { type: "Relevance", threshold: 3 },
  ],
};

describe("case drafts", () => {
  it("round-trips a stored case", () => {
    const draft = toCaseDraft(CASE);

    expect(caseDraftErrors(draft)).toEqual([]);
    expect(toCaseInput(draft)).toEqual(CASE);
  });

  it("starts new cases without an id and with a Relevance Check", () => {
    const draft = emptyCaseDraft();

    expect(draft.id).toBeNull();
    expect(draft.checks).toEqual([{ type: "Relevance", threshold: "" }]);
    expect(caseDraftErrors(draft)).toEqual(["A case needs a prompt."]);
  });

  it("reports invalid arguments, missing requirements and bad thresholds", () => {
    const draft = {
      ...emptyCaseDraft(),
      prompt: "Hi",
      expectedToolCalls: [{ toolName: "knowledge_base_search", argumentsJson: "[1]" }],
      checks: [
        { type: "ContainsExpectedAnswer" as const, threshold: "" },
        { type: "Coherence" as const, threshold: "9" },
      ],
    };

    expect(caseDraftErrors(draft)).toEqual([
      "The arguments of expected tool call 1 must be a JSON object.",
      '"Contains expected answer" needs an expected answer.',
      'The "Coherence" threshold must be a whole number from 1 to 5.',
    ]);
  });

  it("needs canned tool results for Groundedness", () => {
    const draft = { ...emptyCaseDraft(), prompt: "Hi", checks: [{ type: "Groundedness" as const, threshold: "" }] };

    expect(caseDraftErrors(draft)).toEqual(['"Groundedness" needs at least one canned tool result to judge against.']);
    expect(caseDraftErrors({ ...draft, cannedToolResults: [{ toolName: "knowledge_base_search", result: "milk" }] })).toEqual([]);
  });

  it("sends blank arguments as not checked and drops thresholds of deterministic Checks", () => {
    const input = toCaseInput({
      ...emptyCaseDraft(),
      prompt: " Hi ",
      expectedToolCalls: [{ toolName: "knowledge_base_search", argumentsJson: "  " }],
      checks: [{ type: "ToolsCalled", threshold: "3" }],
    });

    expect(input.prompt).toBe("Hi");
    expect(input.expectedToolCalls).toEqual([{ toolName: "knowledge_base_search", arguments: null }]);
    expect(input.checks).toEqual([{ type: "ToolsCalled", threshold: null }]);
  });

  it("labels cases by name, then prompt, then position", () => {
    expect(caseLabel({ name: "Groceries", prompt: "x" }, 0)).toBe("Groceries");
    expect(caseLabel({ name: "", prompt: "What do we need?" }, 0)).toBe("What do we need?");
    expect(caseLabel({ name: "", prompt: "" }, 2)).toBe("Case 3");
  });
});
