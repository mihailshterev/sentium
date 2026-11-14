import { AlertTriangle, Bot, CalendarClock, CalendarDays, Sparkles } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import type { TimelineTask, Upcoming } from "../types/timeline";
import { MONTH_NAMES, deadlineOf, formatShortDay, relativeDay } from "../utils/calendar";
import { TaskRow } from "./task-row";

interface UpNextProps {
  upcoming: Upcoming | undefined;
  today: string;
  onOpenTask: (task: TimelineTask) => void;
  onToggleDone: (task: TimelineTask) => void;
  onAskAgent: (task: TimelineTask) => void;
  onShowSuggestions: () => void;
  onPlanMonth: () => void;
}

const overdueWhen = (task: TimelineTask, today: string) => {
  const deadline = deadlineOf(task);
  return task.date || task.dueDate
    ? `${formatShortDay(deadline)} · ${relativeDay(deadline, today)}`
    : `pinned to ${MONTH_NAMES[task.month - 1]}`;
};

const nextWhen = (task: TimelineTask, today: string) => {
  const relative = relativeDay(task.date!, today);
  return relative === "today" || relative === "tomorrow" ? relative : formatShortDay(task.date!);
};

/**
 * What needs attention now, under the rail while no month is open: overdue tasks, the coming week and the agent
 * suggestions waiting for an answer.
 */
export function UpNext({
  upcoming,
  today,
  onOpenTask,
  onToggleDone,
  onAskAgent,
  onShowSuggestions,
  onPlanMonth,
}: UpNextProps) {
  const overdue = upcoming?.overdue ?? [];
  const overdueCount = upcoming?.overdueCount ?? 0;
  const next = upcoming?.next ?? [];
  const pending = upcoming?.pendingProposals ?? 0;
  const events = upcoming?.events ?? [];
  const rowProps = { today, onOpen: onOpenTask, onToggleDone, onAskAgent };

  return (
    <section className={styles.upNext} aria-label="Up next" data-testid="up-next">
      {overdueCount > 0 && (
        <section className={styles.upColumn} data-tone="danger">
          <header className={styles.sideHead}>
            <h3>
              <AlertTriangle size={13} aria-hidden="true" /> Overdue
              <span className={styles.upCount}>{overdueCount}</span>
            </h3>
          </header>
          <ul className={styles.rows}>
            {overdue.map((task) => (
              <TaskRow key={task.id} task={task} when={overdueWhen(task, today)} {...rowProps} />
            ))}
          </ul>
          {overdueCount > overdue.length && (
            <p className={styles.sideEmpty}>and {overdueCount - overdue.length} more on the timeline</p>
          )}
        </section>
      )}

      <section className={styles.upColumn}>
        <header className={styles.sideHead}>
          <h3>
            <CalendarClock size={13} aria-hidden="true" /> Next {upcoming?.days ?? 7} days
          </h3>
        </header>
        {next.length === 0 ? (
          <p className={styles.sideEmpty}>Nothing due this week.</p>
        ) : (
          <ul className={styles.rows}>
            {next.map((task) => (
              <TaskRow key={task.id} task={task} when={nextWhen(task, today)} {...rowProps} />
            ))}
          </ul>
        )}
      </section>

      {events.length > 0 && (
        <section className={styles.upColumn}>
          <header className={styles.sideHead}>
            <h3>
              <CalendarDays size={13} aria-hidden="true" /> Events
            </h3>
          </header>
          <ul className={styles.rows}>
            {events.map((event) => (
              <TaskRow key={event.id} task={event} when={nextWhen(event, today)} {...rowProps} />
            ))}
          </ul>
        </section>
      )}

      <section className={styles.upColumn}>
        <header className={styles.sideHead}>
          <h3>
            <Sparkles size={13} aria-hidden="true" /> Suggestions
          </h3>
        </header>
        <p className={styles.sideEmpty}>
          {pending === 0
            ? "No agent suggestions waiting."
            : `${pending} ${pending === 1 ? "suggestion is" : "suggestions are"} waiting for your answer.`}
        </p>
        <div className={styles.upActions}>
          {pending > 0 && (
            <Button size="sm" icon={<Sparkles size={14} />} onClick={onShowSuggestions}>
              Review
            </Button>
          )}
          <Button size="sm" icon={<Bot size={14} />} onClick={onPlanMonth}>
            Plan this month
          </Button>
        </div>
      </section>
    </section>
  );
}
