import { z } from "zod";
import type { SaveTaskPayload, TaskPriority, TaskStatus, TimelineTask } from "../types/timeline";
import { isoMonth } from "../utils/calendar";

export const TASK_LIMITS = {
  title: 200,
  description: 4000,
  note: 2000,
  agentName: 100,
  tag: 32,
  tags: 10,
} as const;

export const PRIORITIES: TaskPriority[] = ["Low", "Normal", "High", "Urgent"];
export const STATUSES: TaskStatus[] = ["Todo", "InProgress", "Done"];

export const STATUS_LABELS: Record<TaskStatus, string> = { Todo: "To do", InProgress: "In progress", Done: "Done" };

const isoDatePattern = /^\d{4}-\d{2}-\d{2}$/;
const isoMonthPattern = /^\d{4}-\d{2}$/;

export const taskFormSchema = z
  .object({
    title: z
      .string()
      .trim()
      .min(1, "A task needs a title")
      .max(TASK_LIMITS.title, `At most ${TASK_LIMITS.title} characters`),
    description: z.string().max(TASK_LIMITS.description, `At most ${TASK_LIMITS.description} characters`),
    placement: z.enum(["day", "month"]),
    date: z.string(),
    month: z.string().regex(isoMonthPattern, "Choose a month"),
    dueDate: z.string().refine((v) => v === "" || isoDatePattern.test(v), "Use the date picker (YYYY-MM-DD)"),
    priority: z.enum(["Low", "Normal", "High", "Urgent"]),
    status: z.enum(["Todo", "InProgress", "Done"]),
    tags: z
      .array(z.string().max(TASK_LIMITS.tag, `Tags can be at most ${TASK_LIMITS.tag} characters`))
      .max(TASK_LIMITS.tags, `At most ${TASK_LIMITS.tags} tags`),
    assignedAgent: z.string().max(TASK_LIMITS.agentName),
  })
  .refine((d) => d.placement === "month" || isoDatePattern.test(d.date), {
    message: "Choose a day, or pin the task to the month",
    path: ["date"],
  });

export type TaskFormData = z.infer<typeof taskFormSchema>;

export interface TaskFormDefaults {
  year: number;
  month: number;
  date: string | null;
}

export function toTaskForm(task: TimelineTask | null | undefined, defaults: TaskFormDefaults): TaskFormData {
  if (task) {
    return {
      title: task.title,
      description: task.description ?? "",
      placement: task.date ? "day" : "month",
      date: task.date ?? "",
      month: isoMonth(task.year, task.month),
      dueDate: task.dueDate ?? "",
      priority: task.priority,
      status: task.status,
      tags: task.tags,
      assignedAgent: task.assignedAgent ?? "",
    };
  }
  return {
    title: "",
    description: "",
    placement: defaults.date ? "day" : "month",
    date: defaults.date ?? "",
    month: isoMonth(defaults.year, defaults.month),
    dueDate: "",
    priority: "Normal",
    status: "Todo",
    tags: [],
    assignedAgent: "",
  };
}

/** A day places the task in that day's month; otherwise it is pinned to the chosen month. */
export function toTaskPayload(form: TaskFormData): SaveTaskPayload {
  const day = form.placement === "day" ? form.date : null;
  const [year, month] = (day ?? form.month).split("-").map(Number);
  return {
    title: form.title.trim(),
    description: form.description.trim() || null,
    year,
    month,
    date: day,
    dueDate: form.dueDate || null,
    priority: form.priority,
    status: form.status,
    tags: form.tags,
    assignedAgent: form.assignedAgent.trim() || null,
  };
}
