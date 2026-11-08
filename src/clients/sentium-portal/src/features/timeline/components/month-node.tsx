import { motion } from "framer-motion";
import styles from "../timeline.module.css";
import type { MonthSummary } from "../types/timeline";
import { MONTH_NAMES, MONTH_SHORT, type MonthState } from "../utils/calendar";

interface MonthNodeProps {
  year: number;
  month: number;
  summary: MonthSummary | undefined;
  state: MonthState;
  selected: boolean;
  position: { left: string; top: string };
  onSelect: (month: number) => void;
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
 * A month on the year path: a glowing node whose ring shows how much of the month's work is done. Past months are
 * lit, the current month pulses, future months are hollow.
 */
export function MonthNode({ year, month, summary, state, selected, position, onSelect }: MonthNodeProps) {
  const total = summary?.taskCount ?? 0;
  const done = summary?.doneCount ?? 0;
  const open = summary?.openCount ?? 0;

  return (
    <button
      type="button"
      className={styles.node}
      style={position}
      data-state={state}
      data-selected={selected || undefined}
      data-overdue={(summary?.overdueCount ?? 0) > 0 || undefined}
      aria-label={describe(year, month, summary)}
      aria-expanded={selected}
      aria-current={state === "current" ? "date" : undefined}
      data-testid={`timeline-month-${month}`}
      onClick={() => onSelect(month)}
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
      <span className={styles.nodeLabel} aria-hidden="true">
        {MONTH_SHORT[month - 1]}
      </span>
      {open > 0 && (
        <span className={styles.nodeCount} aria-hidden="true">
          {open}
        </span>
      )}
    </button>
  );
}
