import type { AgentChatContext } from "../types/timeline";
import { isoMonth, monthLabel } from "./calendar";

/** The planner agent: talks about months and unassigned tasks. */
export const DEFAULT_TIMELINE_AGENT = "TimelinePlanner";

export const contextKey = (context: AgentChatContext) =>
  context.kind === "task" ? `task:${context.task.id}` : `month:${isoMonth(context.year, context.month)}`;

export const contextLabel = (context: AgentChatContext) =>
  context.kind === "task" ? context.task.title : monthLabel(context.year, context.month);

/** The task's assigned agent, else the planner. */
export const contextAgent = (context: AgentChatContext) =>
  (context.kind === "task" ? context.task.assignedAgent : null) ?? DEFAULT_TIMELINE_AGENT;

/** User text inside the bracketed prefix cannot break out of it or forge another field. */
const clean = (value: string) =>
  value
    .replace(/[[\]|\r\n]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();

/**
 * The context line sent in front of the first message, in the same `[Kind: label | ID: id]` shape the assistant uses
 * for workspace and file context. Agents read the rest through their timeline tools.
 */
export function contextPrefix(context: AgentChatContext, today: string): string {
  if (context.kind === "month") {
    return `[Timeline month: ${monthLabel(context.year, context.month)} (${isoMonth(context.year, context.month)}) | Today: ${today}]`;
  }

  const { task } = context;
  const parts = [
    `Timeline task: ${clean(task.title)}`,
    `ID: ${task.id}`,
    task.date ? `Scheduled: ${task.date}` : `Pinned to: ${isoMonth(task.year, task.month)}`,
  ];
  if (task.dueDate) parts.push(`Due: ${task.dueDate}`);
  parts.push(`Status: ${task.status}`, `Today: ${today}`);
  return `[${parts.join(" | ")}]`;
}

export function starterPrompts(context: AgentChatContext): string[] {
  if (context.kind === "month") {
    return [
      "Review my workload this month and suggest moves for overloaded days.",
      "What should I plan for this month?",
      "Summarise my progress this month and leave a progress report.",
    ];
  }
  return ["Break this task into steps.", "Find a good day for this task.", "What should I prepare before doing this?"];
}
