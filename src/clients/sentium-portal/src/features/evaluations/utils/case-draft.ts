import type { EvalCase, EvalCaseInput, EvalCheckType, EvalTurnRole } from "../types/eval-suites";

export interface CheckInfo {
  type: EvalCheckType;
  label: string;
  quality: boolean;
  needsExpectedAnswer: boolean;
  needsExpectedToolCalls: boolean;
  needsCannedToolResults: boolean;
}

export const DEFAULT_QUALITY_THRESHOLD = 4;

export const CHECKS: CheckInfo[] = [
  { type: "ContainsExpectedAnswer", label: "Contains expected answer", quality: false, needsExpectedAnswer: true, needsExpectedToolCalls: false, needsCannedToolResults: false },
  { type: "ToolsCalled", label: "Expected tools called", quality: false, needsExpectedAnswer: false, needsExpectedToolCalls: true, needsCannedToolResults: false },
  { type: "ToolArgumentsMatch", label: "Tool arguments match", quality: false, needsExpectedAnswer: false, needsExpectedToolCalls: true, needsCannedToolResults: false },
  { type: "Relevance", label: "Relevance", quality: true, needsExpectedAnswer: false, needsExpectedToolCalls: false, needsCannedToolResults: false },
  { type: "Coherence", label: "Coherence", quality: true, needsExpectedAnswer: false, needsExpectedToolCalls: false, needsCannedToolResults: false },
  { type: "Groundedness", label: "Groundedness", quality: true, needsExpectedAnswer: false, needsExpectedToolCalls: false, needsCannedToolResults: true },
  { type: "Completeness", label: "Completeness", quality: true, needsExpectedAnswer: true, needsExpectedToolCalls: false, needsCannedToolResults: false },
];

export const checkInfo = (type: EvalCheckType): CheckInfo => CHECKS.find((c) => c.type === type)!;

export interface ToolCallDraft {
  toolName: string;
  /** JSON object text; blank means arguments are not checked. */
  argumentsJson: string;
}

export interface CaseDraft {
  /** Stable React key; not sent to the API. */
  key: string;
  id: string | null;
  name: string;
  prompt: string;
  priorTurns: { role: EvalTurnRole; text: string }[];
  expectedAnswer: string;
  expectedToolCalls: ToolCallDraft[];
  cannedToolResults: { toolName: string; result: string }[];
  /** Selected Checks with their threshold text (quality Checks only; blank uses the default). */
  checks: { type: EvalCheckType; threshold: string }[];
}

let draftCounter = 0;
const nextKey = () => `case-${++draftCounter}`;

export function emptyCaseDraft(): CaseDraft {
  return {
    key: nextKey(),
    id: null,
    name: "",
    prompt: "",
    priorTurns: [],
    expectedAnswer: "",
    expectedToolCalls: [],
    cannedToolResults: [],
    checks: [{ type: "Relevance", threshold: "" }],
  };
}

export function toCaseDraft(evalCase: EvalCase): CaseDraft {
  return {
    key: nextKey(),
    id: evalCase.id,
    name: evalCase.name ?? "",
    prompt: evalCase.prompt,
    priorTurns: evalCase.priorTurns.map((t) => ({ ...t })),
    expectedAnswer: evalCase.expectedAnswer ?? "",
    expectedToolCalls: evalCase.expectedToolCalls.map((c) => ({
      toolName: c.toolName,
      argumentsJson: c.arguments ? JSON.stringify(c.arguments, null, 2) : "",
    })),
    cannedToolResults: evalCase.cannedToolResults.map((r) => ({ ...r })),
    checks: evalCase.checks.map((c) => ({ type: c.type, threshold: c.threshold === null ? "" : String(c.threshold) })),
  };
}

function parseArguments(json: string): Record<string, unknown> | null {
  if (!json.trim()) {
    return null;
  }
  const parsed: unknown = JSON.parse(json);
  if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
    throw new Error("not an object");
  }
  return parsed as Record<string, unknown>;
}

/**
 * Problems the form can see before saving. The API validates everything again (including tool names).
 */
export function caseDraftErrors(draft: CaseDraft): string[] {
  const errors: string[] = [];

  if (!draft.prompt.trim()) {
    errors.push("A case needs a prompt.");
  }
  if (draft.checks.length === 0) {
    errors.push("A case needs at least one Check.");
  }
  draft.expectedToolCalls.forEach((call, i) => {
    if (!call.toolName) {
      errors.push(`Expected tool call ${i + 1} needs a tool.`);
    }
    try {
      parseArguments(call.argumentsJson);
    } catch {
      errors.push(`The arguments of expected tool call ${i + 1} must be a JSON object.`);
    }
  });
  draft.cannedToolResults.forEach((result, i) => {
    if (!result.toolName) {
      errors.push(`Canned result ${i + 1} needs a tool.`);
    }
  });
  for (const check of draft.checks) {
    const info = checkInfo(check.type);
    if (info.needsExpectedAnswer && !draft.expectedAnswer.trim()) {
      errors.push(`"${info.label}" needs an expected answer.`);
    }
    if (info.needsExpectedToolCalls && draft.expectedToolCalls.length === 0) {
      errors.push(`"${info.label}" needs at least one expected tool call.`);
    }
    if (info.needsCannedToolResults && draft.cannedToolResults.length === 0) {
      errors.push(`"${info.label}" needs at least one canned tool result to judge against.`);
    }
    if (info.quality && check.threshold.trim()) {
      const value = Number(check.threshold);
      if (!Number.isInteger(value) || value < 1 || value > 5) {
        errors.push(`The "${info.label}" threshold must be a whole number from 1 to 5.`);
      }
    }
  }

  return errors;
}

/** Converts a draft without errors (see {@link caseDraftErrors}) to the API shape. */
export function toCaseInput(draft: CaseDraft): EvalCaseInput {
  return {
    id: draft.id,
    name: draft.name.trim() || null,
    prompt: draft.prompt.trim(),
    priorTurns: draft.priorTurns.filter((t) => t.text.trim()).map((t) => ({ role: t.role, text: t.text.trim() })),
    expectedAnswer: draft.expectedAnswer.trim() || null,
    expectedToolCalls: draft.expectedToolCalls.map((c) => ({
      toolName: c.toolName,
      arguments: parseArguments(c.argumentsJson),
    })),
    cannedToolResults: draft.cannedToolResults.map((r) => ({ toolName: r.toolName, result: r.result })),
    checks: draft.checks.map((c) => ({
      type: c.type,
      threshold: checkInfo(c.type).quality && c.threshold.trim() ? Number(c.threshold) : null,
    })),
  };
}

/** A short label for a case in lists: its name, else the start of its prompt. */
export function caseLabel(draft: Pick<CaseDraft, "name" | "prompt">, index: number): string {
  const name = draft.name.trim();
  if (name) {
    return name;
  }
  const prompt = draft.prompt.trim();
  if (!prompt) {
    return `Case ${index + 1}`;
  }
  return prompt.length > 60 ? `${prompt.slice(0, 60)}…` : prompt;
}
