import type { TaskPlacement, TimelineProposal } from "../types/timeline";

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
  p.kind === "Reschedule" ? `Move "${p.title}"` : `Add "${p.title}"`;

/** Where a dragged task was dropped: a day of this month, the month's pinned list, or nowhere useful. */
export function dropTarget(overId: string | null, year: number, month: number): TaskPlacement | null {
  if (overId === "pinned") return { year, month, date: null };
  if (overId?.startsWith("day:")) return { year, month, date: overId.slice(4) };
  return null;
}
