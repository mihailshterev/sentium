import { motion } from "framer-motion";
import styles from "../timeline.module.css";
import type { MonthSummary } from "../types/timeline";
import { MONTH_NAMES, type MonthState } from "../utils/calendar";

interface MonthNodeProps {
  year: number;
  month: number;
  summary: MonthSummary | undefined;
  state: MonthState;
  selected: boolean;
  position: { left: string; top: string };
  onSelect: (year: number, month: number) => void;
}

function describe(year: number, month: number, summary: MonthSummary | undefined): string {
  const parts = [`${MONTH_NAMES[month - 1]} ${year}`];
  if (!summary) return parts[0];
  parts.push(`${summary.openCount} open ${summary.openCount === 1 ? "task" : "tasks"}`);
  if (summary.overdueCount > 0) parts.push(`${summary.overdueCount} overdue`);
  if (summary.pendingProposalCount > 0) {
    parts.push(`${summary.pendingProposalCount} ${summary.pendingProposalCount === 1 ? "suggestion" : "suggestions"}`);
  }
  return parts.join(", ");
}

/**
 * A month on the rail: a glowing node where the month starts, raised by how many tasks it holds, whose ring shows how
 * much of that work is done. Past months are lit, the current month pulses, future months are hollow. Its label sits
 * on the rail's month axis.
 */
export function MonthNode({ year, month, summary, state, selected, position, onSelect }: MonthNodeProps) {
  const total = summary?.taskCount ?? 0;
  const done = summary?.doneCount ?? 0;

  return (
    <button
      type="button"
      className={styles.node}
      style={position}
      data-state={state}
      data-selected={selected || undefined}
      aria-label={describe(year, month, summary)}
      aria-expanded={selected}
      aria-current={state === "current" ? "date" : undefined}
      data-testid={`timeline-month-${year}-${month}`}
      onClick={() => onSelect(year, month)}
    >
      {selected && <motion.span layoutId="timeline-selected-node" className={styles.nodeHalo} aria-hidden="true" />}
      <svg className={styles.nodeRing} viewBox="0 0 40 40" aria-hidden="true">
        <circle className={styles.ringTrack} cx="20" cy="20" r="17" />
        {total > 0 && (
          <circle
            className={styles.ringDone}
            cx="20"
            cy="20"
            r="17"
            pathLength={1}
            strokeDasharray="1 1"
            strokeDashoffset={1 - done / total}
            transform="rotate(-90 20 20)"
          />
        )}
      </svg>
      <span className={styles.nodeCore} aria-hidden="true" />
    </button>
  );
}
