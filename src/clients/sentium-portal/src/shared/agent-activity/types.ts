/** Outcome of a tool step. */
export type ToolStatus = "running" | "awaiting_approval" | "ok" | "error" | "denied" | "rejected";

/** Visual tone of a workflow status milestone. */
export type StatusTone = "pass" | "fail" | "stuck" | "loop";

interface StepBase {
  /** Stable key: the tool call id for tools, otherwise generated. */
  id: string;
  /** The agent that produced the step (workflow runs). */
  author?: string;
}

/** Model reasoning. Live thoughts carry wall-clock bounds so the UI can say "thought · 4s". */
export interface ThoughtStep extends StepBase {
  kind: "thought";
  text: string;
  startedAt?: number;
  endedAt?: number;
}

/** Narration the model wrote before a later tool call (not part of the final answer). */
export interface TextStep extends StepBase {
  kind: "text";
  text: string;
}

export interface ToolStep extends StepBase {
  kind: "tool";
  callId?: string;
  name: string;
  /** Arguments as (capped) JSON text. */
  arguments?: string;
  status: ToolStatus;
  /** Result preview, or the error / denial reason. */
  result?: string;
  durationMs?: number;
  /** Set while streaming, to show a live elapsed time. */
  startedAt?: number;
}

/** An agent's answer within a workflow run. */
export interface OutputStep extends StepBase {
  kind: "output";
  text: string;
}

/** A workflow milestone (step started, review passed, ...). */
export interface StatusStep extends StepBase {
  kind: "status";
  text: string;
  tone: StatusTone;
}

export type ActivityStep = ThoughtStep | TextStep | ToolStep | OutputStep | StatusStep;

/** An assistant turn while it streams: the activity timeline plus the answer text. */
export interface ActivityTurn {
  content: string;
  steps: ActivityStep[];
}

/** Payload of a `tool_call` frame. */
export interface ToolCallFrame {
  callId: string;
  name: string;
  arguments?: string | null;
}

/** Payload of a `tool_result` frame. */
export interface ToolResultFrame {
  callId: string;
  status: ToolStatus;
  result?: string | null;
  durationMs?: number | null;
}

/** Payload of an `approval_request` frame. */
export interface ApprovalFrame {
  requestId: string;
  toolName: string;
  arguments?: Record<string, unknown> | null;
  callId?: string | null;
}

/** A persisted timeline step, as the conversation API returns it. */
export interface MessageStepDto {
  kind: "thought" | "text" | "tool";
  text?: string | null;
  callId?: string | null;
  name?: string | null;
  arguments?: string | null;
  status?: ToolStatus | null;
  result?: string | null;
  durationMs?: number | null;
}

/** A workflow run log entry (live frame or persisted). */
export interface ActivityLogEntry {
  author: string;
  text: string;
  type: string;
}
