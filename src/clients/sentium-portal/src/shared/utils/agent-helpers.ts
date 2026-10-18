export interface AgentAssignment {
  agent: string;
  task: string;
}

export const parseAssignments = (text: string): AgentAssignment[] | null => {
  if (!text) {
    return null;
  }

  const start = text.indexOf("[");
  const end = text.lastIndexOf("]");
  if (start < 0 || end <= start) {
    return null;
  }

  let raw: unknown;
  try {
    raw = JSON.parse(text.slice(start, end + 1));
  } catch {
    return null;
  }

  if (!Array.isArray(raw)) {
    return null;
  }

  const assignments = raw
    .filter(
      (e): e is { agent: string; task: string } =>
        !!e &&
        typeof e === "object" &&
        typeof (e as Record<string, unknown>).agent === "string" &&
        typeof (e as Record<string, unknown>).task === "string" &&
        (e as { agent: string }).agent.trim().length > 0 &&
        (e as { task: string }).task.trim().length > 0,
    )
    .map((e) => ({ agent: e.agent.trim(), task: e.task.trim() }));

  return assignments.length > 0 ? assignments : null;
};
