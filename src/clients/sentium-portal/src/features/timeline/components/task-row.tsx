import { Bot, CalendarDays, Repeat } from "lucide-react";
import styles from "../timeline.module.css";
import type { TimelineTask } from "../types/timeline";
import { formatShortDay } from "../utils/calendar";
import { contextAgent } from "../utils/agent-context";
import { formatTime, isEvent } from "../utils/display";

interface TaskRowProps {
  task: TimelineTask;
  today: string;
  when?: string;
  onOpen: (task: TimelineTask) => void;
  onToggleDone: (task: TimelineTask) => void;
  onAskAgent: (task: TimelineTask) => void;
}

/**
 * An Entry in an agenda list: tick a Task done, open it, or ask its agent about it. A Calendar Event has no tick
 * box; it simply fades once past.
 */
export function TaskRow({ task, today, when, onOpen, onToggleDone, onAskAgent }: TaskRowProps) {
  const event = isEvent(task);
  const done = !event && task.status === "Done";
  const overdue = !event && !done && task.dueDate !== null && task.dueDate < today;
  const agent = contextAgent({ kind: "task", task });
  const time = formatTime(task.startTime);

  return (
    <li
      className={styles.row}
      data-kind={event ? "event" : "task"}
      data-done={done || undefined}
      data-past={task.isPast || undefined}
      data-priority={event ? undefined : task.priority.toLowerCase()}
    >
      {event ? (
        <CalendarDays size={13} className={styles.rowEventIcon} aria-hidden="true" />
      ) : (
        <input
          type="checkbox"
          className={styles.rowCheck}
          checked={done}
          aria-label={`${done ? "Reopen" : "Complete"} ${task.title}`}
          onChange={() => onToggleDone(task)}
        />
      )}
      <button type="button" className={styles.rowTitle} onClick={() => onOpen(task)}>
        <span>{task.title}</span>
        <span className={styles.rowMeta}>
          {when && <span className={styles.rowWhen}>{when}</span>}
          {time && <span>{time}</span>}
          {event ? <span className={styles.rowPriority}>event</span> : <span className={styles.rowPriority}>{task.priority.toLowerCase()}</span>}
          {event && task.endDate && <span>until {formatShortDay(task.endDate)}</span>}
          {event && task.isPast && <span>past</span>}
          {task.status === "InProgress" && !event && <span>in progress</span>}
          {task.dueDate && <span data-overdue={overdue || undefined}>due {formatShortDay(task.dueDate)}</span>}
          {task.seriesId && (
            <span title="Repeats">
              <Repeat size={10} aria-label="repeats" />
            </span>
          )}
          {task.tags.map((tag) => (
            <span key={tag}>#{tag}</span>
          ))}
          {task.origin === "Agent" && task.createdByAgent && <span>via {task.createdByAgent}</span>}
        </span>
      </button>
      <button
        type="button"
        className={styles.agentButton}
        aria-label={`Ask ${agent} about ${task.title}`}
        title={`Ask ${agent}`}
        onClick={() => onAskAgent(task)}
      >
        <Bot size={14} />
      </button>
    </li>
  );
}
