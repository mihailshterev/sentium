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

/** Tasks planned on `date`, open ones first. */
export const tasksOnDay = (tasks: TimelineTask[], date: string) =>
  tasks.filter((t) => t.date === date).sort((a, b) => Number(a.status === "Done") - Number(b.status === "Done"));

export const pinnedTasks = (tasks: TimelineTask[]) =>
  tasks.filter((t) => t.date === null).sort((a, b) => Number(a.status === "Done") - Number(b.status === "Done"));
