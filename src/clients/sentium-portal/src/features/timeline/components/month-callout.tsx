import { AlertTriangle, Bot, CalendarDays, Sparkles, StickyNote } from "lucide-react";
import styles from "../timeline.module.css";
import type { MonthSummary } from "../types/timeline";
import { MONTH_NAMES } from "../utils/calendar";

/** Agent activity this recent makes the callout's agent dot pulse. */
const ACTIVE_WINDOW_MS = 24 * 60 * 60 * 1000;

interface MonthCalloutProps {
  year: number;
  month: number;
  summary: MonthSummary;
  position: { left: string; top: string };
  now: number;
  onSelect: (year: number, month: number) => void;
}

function Headline({ summary }: { summary: MonthSummary }) {
  const { taskCount: total, openCount: open } = summary;
  if (total === 0) return <>{summary.eventCount > 0 ? "no tasks" : "no tasks yet"}</>;
  if (open === 0) return <>all {total} done</>;
  return (
    <>
      {open} open
      {total > open && <span className={styles.calloutDone}> · {total - open} done</span>}
    </>
  );
}

/**
 * A hairline card in the rail's top lane for a month that has something in it (empty months show only their node),
 * with a leader down to the node. The node is the accessible control; the card is a larger pointer target for the
 * same action.
 */
export function MonthCallout({ year, month, summary, position, now, onSelect }: MonthCalloutProps) {
  const total = summary.taskCount;
  const overdue = summary.overdueCount;
  const suggested = summary.pendingProposalCount;
  const agentNotes = summary.agentNoteCount;
  const userNotes = summary.noteCount - agentNotes;
  const events = summary.eventCount;
  const lastAgent = summary.lastAgentActivityAt ? new Date(summary.lastAgentActivityAt).getTime() : null;
  const agentActive = lastAgent !== null && now - lastAgent < ACTIVE_WINDOW_MS;

  return (
    <div className={styles.callout} style={position} aria-hidden="true" onClick={() => onSelect(year, month)}>
      <span className={styles.calloutTitle}>
        {MONTH_NAMES[month - 1]}
        {agentActive && <span className={styles.agentPulse} />}
      </span>
      <span className={styles.calloutMeta}>
        <Headline summary={summary} />
      </span>
      {total > 0 && (
        <span className={styles.calloutBar}>
          <span style={{ width: `${(summary.doneCount / total) * 100}%` }} />
        </span>
      )}
      {(overdue > 0 || suggested > 0 || agentNotes > 0 || userNotes > 0 || events > 0) && (
        <span className={styles.calloutStats}>
          {events > 0 && (
            <span className={styles.stat} data-tone="purple" title={`${events} ${events === 1 ? "event" : "events"}`}>
              <CalendarDays size={10} />
              {events}
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
          {userNotes > 0 && (
            <span className={styles.stat}>
              <StickyNote size={10} />
              {userNotes}
            </span>
          )}
        </span>
      )}
    </div>
  );
}
