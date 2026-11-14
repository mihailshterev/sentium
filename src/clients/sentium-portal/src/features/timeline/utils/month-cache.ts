import type { MonthDetail, TimelineTask } from "../types/timeline";

/** Cache edits for optimistic updates of a month's detail (immutable). */

export const withoutTask = (detail: MonthDetail, taskId: string): MonthDetail => ({
  ...detail,
  tasks: detail.tasks.filter((t) => t.id !== taskId),
});

/** Replaces the task when present, else appends it. */
export function withTask(detail: MonthDetail, task: TimelineTask): MonthDetail {
  const exists = detail.tasks.some((t) => t.id === task.id);
  return {
    ...detail,
    tasks: exists ? detail.tasks.map((t) => (t.id === task.id ? task : t)) : [...detail.tasks, task],
  };
}

const covers = (task: TimelineTask, date: string) =>
  task.date === date || (task.date !== null && task.endDate !== null && task.date <= date && date <= task.endDate);

/** Calendar Events first, then open Tasks before done ones; within each, by time with untimed entries last. */
const dayOrder = (a: TimelineTask, b: TimelineTask) =>
  Number(a.kind === "Task") - Number(b.kind === "Task") ||
  Number(a.status === "Done") - Number(b.status === "Done") ||
  (a.startTime ?? "99").localeCompare(b.startTime ?? "99");

/** Entries on `date`, including every day of a multi-day Calendar Event. */
export const tasksOnDay = (tasks: TimelineTask[], date: string) => tasks.filter((t) => covers(t, date)).sort(dayOrder);

export const pinnedTasks = (tasks: TimelineTask[]) =>
  tasks.filter((t) => t.date === null).sort((a, b) => Number(a.status === "Done") - Number(b.status === "Done"));
