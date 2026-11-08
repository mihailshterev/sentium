import { AlertTriangle, Bot, CircleDot, Sparkles } from "lucide-react";
import styles from "../timeline.module.css";
import type { MonthSummary } from "../types/timeline";
import { MONTH_NAMES } from "../utils/calendar";

/** Agent activity this recent makes the callout's agent dot pulse. */
const ACTIVE_WINDOW_MS = 24 * 60 * 60 * 1000;

interface MonthCalloutProps {
  month: number;
  summary: MonthSummary | undefined;
  side: "above" | "below" | "left" | "right";
  position: { left: string; top: string };
  now: number;
  onSelect: (month: number) => void;
}

export function MonthCallout({ month, summary, side, position, now, onSelect }: MonthCalloutProps) {
  const open = summary?.openCount ?? 0;
  const total = summary?.taskCount ?? 0;
  const overdue = summary?.overdueCount ?? 0;
  const suggested = summary?.pendingProposalCount ?? 0;
  const agentNotes = summary?.agentNoteCount ?? 0;
  const lastAgent = summary?.lastAgentActivityAt ? new Date(summary.lastAgentActivityAt).getTime() : null;
  const agentActive = lastAgent !== null && now - lastAgent < ACTIVE_WINDOW_MS;

  return (
    <div
      className={styles.callout}
      data-side={side}
      style={position}
      aria-hidden="true"
      onClick={() => onSelect(month)}
    >
      <span className={styles.calloutTitle}>
        {MONTH_NAMES[month - 1]}
        {agentActive && <span className={styles.agentPulse} />}
      </span>
      <span className={styles.calloutMeta}>{total === 0 ? "nothing planned" : `${total - open}/${total} done`}</span>
      {(open > 0 || overdue > 0 || suggested > 0 || agentNotes > 0) && (
        <span className={styles.calloutStats}>
          {open > 0 && (
            <span className={styles.stat} data-tone="cyan">
              <CircleDot size={10} />
              {open}
            </span>
          )}
          {overdue > 0 && (
            <span className={styles.stat} data-tone="danger">
              <AlertTriangle size={10} />
              {overdue}
            </span>
          )}
          {suggested > 0 && (
            <span className={styles.stat} data-tone="gold">
              <Sparkles size={10} />
              {suggested}
            </span>
          )}
          {agentNotes > 0 && (
            <span className={styles.stat} data-tone="purple">
              <Bot size={10} />
              {agentNotes}
            </span>
          )}
        </span>
      )}
    </div>
  );
}
