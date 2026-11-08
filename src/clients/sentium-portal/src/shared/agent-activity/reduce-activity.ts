import type {
  ActivityLogEntry,
  ActivityStep,
  ActivityTurn,
  ApprovalFrame,
  MessageStepDto,
  StatusTone,
  ThoughtStep,
  ToolCallFrame,
  ToolResultFrame,
  ToolStep,
} from "./types";

/*
 * Pure reducers that keep an agent's activity in the order it happened: thinking → tool → result → thinking …
 * The assistant chat folds SSE frames into an ActivityTurn; workflow runs derive steps from their ordered log.
 */

let sequence = 0;
const nextId = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${(sequence++).toString(36)}`;

const LEGACY_TOOL_LABEL = /^Calling (.+?)\.{3}$/;

/** Tool name from a legacy `"Calling x..."` label. */
export const legacyToolName = (label: string) => LEGACY_TOOL_LABEL.exec(label.trim())?.[1] ?? label.trim();

export const parseJson = <T>(text: string): T | null => {
  try {
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
};

const isOpenThought = (step: ActivityStep | undefined): step is ThoughtStep =>
  step?.kind === "thought" && step.endedAt === undefined;

/** Closes a still-streaming thought (its duration ends when something else starts). */
const closeThought = (steps: ActivityStep[], now: number): ActivityStep[] => {
  const last = steps[steps.length - 1];
  if (!isOpenThought(last) || last.startedAt === undefined) {
    return steps;
  }
  return [...steps.slice(0, -1), { ...last, endedAt: now }];
};

/** Narration written before a tool call or a new thought is not the answer: move it onto the timeline. */
const flushContent = (turn: ActivityTurn, now: number): ActivityTurn => {
  const steps = closeThought(turn.steps, now);
  const text = turn.content.trim();
  if (!text) {
    return { content: "", steps };
  }
  return { content: "", steps: [...steps, { kind: "text", id: nextId("text"), text }] };
};

const findTool = (steps: ActivityStep[], callId: string | null | undefined, name?: string): number => {
  if (callId) {
    return steps.findLastIndex((s) => s.kind === "tool" && s.callId === callId);
  }
  return steps.findLastIndex(
    (s) => s.kind === "tool" && s.status === "awaiting_approval" && (!name || s.name === name),
  );
};

const patchTool = (steps: ActivityStep[], index: number, patch: Partial<ToolStep>): ActivityStep[] => {
  const next = steps.slice();
  next[index] = { ...(steps[index] as ToolStep), ...patch };
  return next;
};

export const emptyTurn = (): ActivityTurn => ({ content: "", steps: [] });

/** A reasoning delta: extends the open thought, or starts a new one. */
export const appendThought = (turn: ActivityTurn, text: string, now = Date.now()): ActivityTurn => {
  const last = turn.steps[turn.steps.length - 1];
  if (!turn.content && isOpenThought(last)) {
    return { ...turn, steps: [...turn.steps.slice(0, -1), { ...last, text: last.text + text }] };
  }
  const flushed = flushContent(turn, now);
  return { ...flushed, steps: [...flushed.steps, { kind: "thought", id: nextId("thought"), text, startedAt: now }] };
};

/** An answer-text delta. */
export const appendContent = (turn: ActivityTurn, text: string, now = Date.now()): ActivityTurn => ({
  steps: closeThought(turn.steps, now),
  content: turn.content + text,
});

export const startTool = (turn: ActivityTurn, frame: ToolCallFrame, now = Date.now()): ActivityTurn => {
  const existing = findTool(turn.steps, frame.callId);
  if (existing >= 0) {
    const step = turn.steps[existing] as ToolStep;
    return step.status === "awaiting_approval"
      ? { ...turn, steps: patchTool(turn.steps, existing, { status: "running", startedAt: now }) }
      : turn;
  }
  const flushed = flushContent(turn, now);
  const step: ToolStep = {
    kind: "tool",
    id: frame.callId || nextId("tool"),
    callId: frame.callId,
    name: frame.name,
    arguments: frame.arguments ?? undefined,
    status: "running",
    startedAt: now,
  };
  return { ...flushed, steps: [...flushed.steps, step] };
};

export const finishTool = (turn: ActivityTurn, frame: ToolResultFrame): ActivityTurn => {
  const index = findTool(turn.steps, frame.callId);
  if (index < 0) {
    return turn;
  }
  const step = turn.steps[index] as ToolStep;
  return {
    ...turn,
    steps: patchTool(turn.steps, index, {
      status: step.status === "rejected" ? "rejected" : frame.status,
      result: frame.result ?? undefined,
      durationMs: frame.durationMs ?? undefined,
      startedAt: undefined,
    }),
  };
};

/** The run paused for a human decision on a tool call. */
export const requestApproval = (turn: ActivityTurn, frame: ApprovalFrame, now = Date.now()): ActivityTurn => {
  const existing = findTool(turn.steps, frame.callId ?? undefined);
  if (frame.callId && existing >= 0) {
    return { ...turn, steps: patchTool(turn.steps, existing, { status: "awaiting_approval", startedAt: undefined }) };
  }
  const flushed = flushContent(turn, now);
  const args = frame.arguments && Object.keys(frame.arguments).length > 0 ? JSON.stringify(frame.arguments) : undefined;
  const step: ToolStep = {
    kind: "tool",
    id: frame.callId || nextId("tool"),
    callId: frame.callId ?? undefined,
    name: frame.toolName,
    arguments: args,
    status: "awaiting_approval",
  };
  return { ...flushed, steps: [...flushed.steps, step] };
};

/** Records the user's approve/deny decision on the awaiting tool step. */
export const decideApproval = (
  turn: ActivityTurn,
  approval: { callId?: string | null; toolName: string },
  approved: boolean,
  now = Date.now(),
): ActivityTurn => {
  const index = findTool(turn.steps, approval.callId, approval.toolName);
  if (index < 0) {
    return turn;
  }
  return {
    ...turn,
    steps: patchTool(turn.steps, index, approved ? { status: "running", startedAt: now } : { status: "rejected" }),
  };
};

/**
 * The stream ended: close the open thought, and if the model's last words came before its last tool call, they are
 * the answer after all.
 */
export const finalizeTurn = (turn: ActivityTurn, now = Date.now()): ActivityTurn => {
  const steps = closeThought(turn.steps, now);
  const last = steps[steps.length - 1];
  if (!turn.content.trim() && last?.kind === "text") {
    return { content: last.text, steps: steps.slice(0, -1) };
  }
  return { ...turn, steps };
};

/** Timeline steps of a persisted message: its ordered steps, or the legacy merged thought / tool labels. */
export const stepsFromDto = (
  messageId: string,
  dto: { steps?: MessageStepDto[] | null; thought?: string | null; toolCalls?: string[] | null },
): ActivityStep[] => {
  if (dto.steps && dto.steps.length > 0) {
    return dto.steps.map((s, i): ActivityStep => {
      const id = `${messageId}-${i}`;
      if (s.kind === "tool") {
        return {
          kind: "tool",
          id: s.callId || id,
          callId: s.callId ?? undefined,
          name: s.name ?? "tool",
          arguments: s.arguments ?? undefined,
          status: s.status ?? "ok",
          result: s.result ?? undefined,
          durationMs: s.durationMs ?? undefined,
        };
      }
      return { kind: s.kind === "text" ? "text" : "thought", id, text: s.text ?? "" };
    });
  }

  const legacy: ActivityStep[] = [];
  if (dto.thought?.trim()) {
    legacy.push({ kind: "thought", id: `${messageId}-thought`, text: dto.thought });
  }
  (dto.toolCalls ?? []).forEach((label, i) =>
    legacy.push({ kind: "tool", id: `${messageId}-tool-${i}`, name: legacyToolName(label), status: "ok" }),
  );
  return legacy;
};

/** Tone of a workflow status line, from its wording. */
export const statusTone = (text: string): StatusTone => {
  const t = text.toLowerCase();
  if (t.includes("passed") || t.includes("validated")) return "pass";
  if (t.includes("failed")) return "fail";
  if (t.includes("stuck")) return "stuck";
  return "loop";
};

/**
 * Timeline steps of a workflow run's ordered log (live or persisted): consecutive reasoning or output of one agent is
 * merged, tool results update their call, and legacy `tool` labels still render.
 */
export const stepsFromWorkflowLog = (entries: ActivityLogEntry[]): ActivityStep[] => {
  const steps: ActivityStep[] = [];

  entries.forEach((entry, i) => {
    const id = `w-${i}`;
    const last = steps[steps.length - 1];
    const author = entry.author;

    switch (entry.type) {
      case "thought":
      case "message": {
        const kind = entry.type === "thought" ? "thought" : "output";
        if (last && last.kind === kind && last.author === author) {
          steps[steps.length - 1] = { ...last, text: last.text + entry.text };
        } else {
          steps.push({ kind, id, author, text: entry.text });
        }
        break;
      }
      case "tool_call": {
        const frame = parseJson<ToolCallFrame>(entry.text);
        if (frame?.callId && steps.some((s) => s.kind === "tool" && s.callId === frame.callId)) {
          break;
        }
        steps.push({
          kind: "tool",
          id: frame?.callId || id,
          author,
          callId: frame?.callId,
          name: frame?.name ?? legacyToolName(entry.text),
          arguments: frame?.arguments ?? undefined,
          status: "running",
        });
        break;
      }
      case "tool_result": {
        const frame = parseJson<ToolResultFrame>(entry.text);
        const index = frame ? steps.findLastIndex((s) => s.kind === "tool" && s.callId === frame.callId) : -1;
        if (frame && index >= 0) {
          steps[index] = {
            ...(steps[index] as ToolStep),
            status: frame.status,
            result: frame.result ?? undefined,
            durationMs: frame.durationMs ?? undefined,
          };
        }
        break;
      }
      case "tool":
        steps.push({ kind: "tool", id, author, name: legacyToolName(entry.text), status: "ok" });
        break;
      case "status":
        steps.push({ kind: "status", id, author, text: entry.text, tone: statusTone(entry.text) });
        break;
      case "error":
        steps.push({ kind: "status", id, author, text: entry.text, tone: "fail" });
        break;
      default:
        break;
    }
  });

  return steps;
};
