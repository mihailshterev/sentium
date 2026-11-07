import type { WorkflowRun } from "@/features/workflows/types/workflows";

export type RunOutcome = "completed" | "failed" | "cancelled" | "timeout";

export interface RunDescription {
  title: string;
  source: string;
  outcome: RunOutcome;
}

const DYNAMIC_TRIGGER = "workflow.dynamic";
const MAX_FALLBACK_TITLE = 60;
const CONTEXT_TAG = /\[(?:Workspace|File):[^\]]*\]/gi;

const OUTCOME_PREFIXES: [string, RunOutcome][] = [
  ["[FAILED]", "failed"],
  ["[CANCELLED]", "cancelled"],
  ["[TIMEOUT]", "timeout"],
];

export const RUN_OUTCOME_LABEL: Record<Exclude<RunOutcome, "completed">, string> = {
  failed: "Failed",
  cancelled: "Cancelled",
  timeout: "Timed out",
};

const readPayload = (raw: string | undefined): Record<string, unknown> => {
  if (!raw) {
    return {};
  }
  try {
    const parsed: unknown = JSON.parse(raw);
    return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
};

const readString = (payload: Record<string, unknown>, key: string): string | undefined => {
  const value = payload[key];
  return typeof value === "string" && value.trim() ? value : undefined;
};

const summarize = (text: string): string => {
  const cleaned = text.replace(CONTEXT_TAG, " ").replace(/\s+/g, " ").trim();
  if (cleaned.length <= MAX_FALLBACK_TITLE) {
    return cleaned;
  }
  const cut = cleaned.slice(0, MAX_FALLBACK_TITLE);
  const lastSpace = cut.lastIndexOf(" ");
  return `${(lastSpace > MAX_FALLBACK_TITLE / 2 ? cut.slice(0, lastSpace) : cut).trimEnd()}…`;
};

const formatTrigger = (type: string | undefined) => type?.split(".").filter(Boolean).pop() ?? "Workflow";

export function describeRun(
  run: Pick<WorkflowRun, "title" | "triggerType" | "triggerPayload" | "explanation">,
): RunDescription {
  const payload = readPayload(run.triggerPayload);
  const activity = readString(payload, "activity");

  const title = run.title?.trim() || (activity ? summarize(activity) : "") || "Untitled run";
  const source =
    readString(payload, "workflowName") ??
    (run.triggerType === DYNAMIC_TRIGGER ? "Dynamic" : formatTrigger(run.triggerType));
  const outcome = OUTCOME_PREFIXES.find(([prefix]) => run.explanation?.startsWith(prefix))?.[1] ?? "completed";

  return { title, source, outcome };
}
