import type { TaskPlacement, TimelineProposal, TimelineTask } from "../types/timeline";

export const isEvent = (task: Pick<TimelineTask, "kind">) => task.kind === "CalendarEvent";

/** `HH:mm:ss` to `HH:mm`; empty for an Entry without a time. */
export const formatTime = (time: string | null) => (time ? time.slice(0, 5) : "");

/** "TimelinePlanner" -> "TP", "budget bot" -> "BB". */
export const initialsOf = (name: string) =>
  name
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join("");

export const describeProposal = (p: TimelineProposal) =>
  p.kind === "Reschedule" ? `Move "${p.title}"` : p.kind === "CreateEvent" ? `Add event "${p.title}"` : `Add "${p.title}"`;

/** Where a dragged task was dropped: a day of this month, the month's pinned list, or nowhere useful. */
export function dropTarget(overId: string | null, year: number, month: number): TaskPlacement | null {
  if (overId === "pinned") return { year, month, date: null };
  if (overId?.startsWith("day:")) return { year, month, date: overId.slice(4) };
  return null;
}
