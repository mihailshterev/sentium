import { z } from "zod";
import type {
  EntryKind,
  RepeatFrequency,
  SaveTaskPayload,
  TaskPriority,
  TaskStatus,
  TimelineTask,
} from "../types/timeline";
import { isoMonth } from "../utils/calendar";

export const TASK_LIMITS = {
  title: 200,
  description: 4000,
  note: 2000,
  agentName: 100,
  tag: 32,
  tags: 10,
  repeatInterval: 99,
} as const;

export const PRIORITIES: TaskPriority[] = ["Low", "Normal", "High", "Urgent"];
export const STATUSES: TaskStatus[] = ["Todo", "InProgress", "Done"];
export const FREQUENCIES: RepeatFrequency[] = ["Daily", "Weekly", "Monthly", "Yearly"];

export const STATUS_LABELS: Record<TaskStatus, string> = { Todo: "To do", InProgress: "In progress", Done: "Done" };
export const KIND_LABELS: Record<EntryKind, string> = { Task: "Task", CalendarEvent: "Event" };
export const FREQUENCY_LABELS: Record<RepeatFrequency, string> = {
  Daily: "Daily",
  Weekly: "Weekly",
  Monthly: "Monthly",
  Yearly: "Yearly",
};

/** Monday first; values are .NET `DayOfWeek` numbers (0 = Sunday). */
export const WEEKDAYS = [
  { value: 1, label: "Mo" },
  { value: 2, label: "Tu" },
  { value: 3, label: "We" },
  { value: 4, label: "Th" },
  { value: 5, label: "Fr" },
  { value: 6, label: "Sa" },
  { value: 0, label: "Su" },
] as const;

/** Reminder offsets in minutes; an Entry without a time is reminded at day-start, so only whole days apply. */
export const DAY_REMINDERS = [
  { value: "0", label: "On the day" },
  { value: "1440", label: "The day before" },
  { value: "10080", label: "A week before" },
] as const;

export const TIME_REMINDERS = [
  { value: "0", label: "At the time" },
  { value: "15", label: "15 minutes before" },
  { value: "60", label: "1 hour before" },
  { value: "180", label: "3 hours before" },
  { value: "1440", label: "1 day before" },
] as const;

const isoDatePattern = /^\d{4}-\d{2}-\d{2}$/;
const isoMonthPattern = /^\d{4}-\d{2}$/;
const timePattern = /^\d{2}:\d{2}$/;

const optionalDate = z.string().refine((v) => v === "" || isoDatePattern.test(v), "Use the date picker (YYYY-MM-DD)");

export const taskFormSchema = z
  .object({
    kind: z.enum(["Task", "CalendarEvent"]),
    title: z
      .string()
      .trim()
      .min(1, "A title is required")
      .max(TASK_LIMITS.title, `At most ${TASK_LIMITS.title} characters`),
    description: z.string().max(TASK_LIMITS.description, `At most ${TASK_LIMITS.description} characters`),
    placement: z.enum(["day", "month"]),
    date: z.string(),
    month: z.string().regex(isoMonthPattern, "Choose a month"),
    startTime: z.string().refine((v) => v === "" || timePattern.test(v), "Use the time picker (HH:mm)"),
    endDate: optionalDate,
    dueDate: optionalDate,
    priority: z.enum(["Low", "Normal", "High", "Urgent"]),
    status: z.enum(["Todo", "InProgress", "Done"]),
    tags: z
      .array(z.string().max(TASK_LIMITS.tag, `Tags can be at most ${TASK_LIMITS.tag} characters`))
      .max(TASK_LIMITS.tags, `At most ${TASK_LIMITS.tags} tags`),
    assignedAgent: z.string().max(TASK_LIMITS.agentName),
    repeat: z.enum(["", "Daily", "Weekly", "Monthly", "Yearly"]),
    repeatInterval: z
      .string()
      .refine(
        (v) => /^\d+$/.test(v) && Number(v) >= 1 && Number(v) <= TASK_LIMITS.repeatInterval,
        `Between 1 and ${TASK_LIMITS.repeatInterval}`,
      ),
    repeatWeekdays: z.array(z.number().int().min(0).max(6)),
    repeatUntil: optionalDate,
    reminder: z.string(),
  })
  .refine((d) => d.placement === "month" || isoDatePattern.test(d.date), {
    message: "Choose a day, or pin it to the month",
    path: ["date"],
  })
  .refine((d) => d.placement === "month" || d.kind === "Task" || d.endDate === "" || d.endDate >= d.date, {
    message: "The end day cannot be before the day",
    path: ["endDate"],
  })
  .refine((d) => d.placement === "day" || d.repeat === "" || d.repeat === "Monthly" || d.repeat === "Yearly", {
    message: "An entry pinned to a month can only repeat monthly or yearly",
    path: ["repeat"],
  })
  .refine(
    (d) =>
      d.placement === "month" ||
      d.startTime !== "" ||
      DAY_REMINDERS.some((r) => r.value === d.reminder) ||
      d.reminder === "",
    { message: "Without a time, remind on the day, the day before or a week before", path: ["reminder"] },
  );

export type TaskFormData = z.infer<typeof taskFormSchema>;

export interface TaskFormDefaults {
  year: number;
  month: number;
  date: string | null;
}

/** `HH:mm:ss` from the server to the `HH:mm` of a time input. */
const toTimeInput = (time: string | null) => (time ? time.slice(0, 5) : "");

export function toTaskForm(task: TimelineTask | null | undefined, defaults: TaskFormDefaults): TaskFormData {
  if (task) {
    return {
      kind: task.kind,
      title: task.title,
      description: task.description ?? "",
      placement: task.date ? "day" : "month",
      date: task.date ?? "",
      month: isoMonth(task.year, task.month),
      startTime: toTimeInput(task.startTime),
      endDate: task.endDate ?? "",
      dueDate: task.dueDate ?? "",
      priority: task.priority,
      status: task.status,
      tags: task.tags,
      assignedAgent: task.assignedAgent ?? "",
      repeat: task.repeat?.frequency ?? "",
      repeatInterval: String(task.repeat?.interval ?? 1),
      repeatWeekdays: task.repeat?.weekdays ?? [],
      repeatUntil: task.repeat?.until ?? "",
      reminder: task.reminderOffsetMinutes === null ? "" : String(task.reminderOffsetMinutes),
    };
  }
  return {
    kind: "Task",
    title: "",
    description: "",
    placement: defaults.date ? "day" : "month",
    date: defaults.date ?? "",
    month: isoMonth(defaults.year, defaults.month),
    startTime: "",
    endDate: "",
    dueDate: "",
    priority: "Normal",
    status: "Todo",
    tags: [],
    assignedAgent: "",
    repeat: "",
    repeatInterval: "1",
    repeatWeekdays: [],
    repeatUntil: "",
    reminder: "",
  };
}

/**
 * A day places the entry in that day's month; otherwise it is pinned to the chosen month and has no time or end
 * day. A Calendar Event never sends the Task-only fields.
 */
export function toTaskPayload(form: TaskFormData): SaveTaskPayload {
  const day = form.placement === "day" ? form.date : null;
  const [year, month] = (day ?? form.month).split("-").map(Number);
  const isTask = form.kind === "Task";
  return {
    kind: form.kind,
    title: form.title.trim(),
    description: form.description.trim() || null,
    year,
    month,
    date: day,
    startTime: day && form.startTime ? `${form.startTime}:00` : null,
    endDate: day && !isTask && form.endDate ? form.endDate : null,
    dueDate: isTask ? form.dueDate || null : null,
    priority: isTask ? form.priority : null,
    status: isTask ? form.status : null,
    tags: form.tags,
    assignedAgent: isTask ? form.assignedAgent.trim() || null : null,
    repeat: form.repeat
      ? {
          frequency: form.repeat,
          interval: Number(form.repeatInterval),
          weekdays: form.repeat === "Weekly" && form.repeatWeekdays.length > 0 ? form.repeatWeekdays : null,
          until: form.repeatUntil || null,
        }
      : null,
    reminderOffsetMinutes: form.reminder === "" ? null : Number(form.reminder),
  };
}
