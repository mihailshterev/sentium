export type EvalCheckType =
  | "ContainsExpectedAnswer"
  | "ToolsCalled"
  | "ToolArgumentsMatch"
  | "Relevance"
  | "Coherence"
  | "Groundedness"
  | "Completeness";

export type EvalTurnRole = "User" | "Assistant";

export interface EvalTurn {
  role: EvalTurnRole;
  text: string;
}

export interface ExpectedToolCall {
  toolName: string;
  /** The subset of arguments that must match; null means arguments are not checked. */
  arguments: Record<string, unknown> | null;
}

export interface CannedToolResult {
  toolName: string;
  result: string;
}

export interface EvalCheck {
  type: EvalCheckType;
  /** Passing Judge score (1-5) of a quality Check; null uses the default of 4. */
  threshold: number | null;
}

export interface EvalCase {
  id: string;
  name: string | null;
  prompt: string;
  priorTurns: EvalTurn[];
  expectedAnswer: string | null;
  expectedToolCalls: ExpectedToolCall[];
  cannedToolResults: CannedToolResult[];
  checks: EvalCheck[];
}

/** A case as sent to the API: a case without an id is new. */
export type EvalCaseInput = Omit<EvalCase, "id"> & { id: string | null };

export interface EvalSuiteSummary {
  id: string;
  name: string;
  description: string | null;
  currentVersion: number;
  caseCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface EvalSuite {
  id: string;
  name: string;
  description: string | null;
  /** The version shown; older than `currentVersion` when an earlier version was opened. */
  version: number;
  currentVersion: number;
  versionCreatedAt: string;
  cases: EvalCase[];
  createdAt: string;
  updatedAt: string;
}

export interface EvalSuiteVersion {
  version: number;
  caseCount: number;
  createdAt: string;
}

export interface CreateEvalSuitePayload {
  name: string;
  description: string | null;
  cases: EvalCaseInput[];
}

export interface UpdateEvalSuitePayload extends CreateEvalSuitePayload {
  baseVersion: number;
}
