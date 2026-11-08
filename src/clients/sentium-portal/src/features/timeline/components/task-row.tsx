import { Bot } from "lucide-react";
import styles from "../timeline.module.css";
import type { TimelineTask } from "../types/timeline";
import { formatShortDay } from "../utils/calendar";
import { contextAgent } from "../utils/agent-context";

interface TaskRowProps {
  task: TimelineTask;
  today: string;
  onOpen: (task: TimelineTask) => void;
  onToggleDone: (task: TimelineTask) => void;
  onAskAgent: (task: TimelineTask) => void;
}

/** A task in the side panel's agenda: complete it, open it, or ask its agent about it. */
export function TaskRow({ task, today, onOpen, onToggleDone, onAskAgent }: TaskRowProps) {
  const done = task.status === "Done";
  const overdue = !done && task.dueDate !== null && task.dueDate < today;
  const agent = contextAgent({ kind: "task", task });

  return (
    <li className={styles.row} data-done={done || undefined} data-priority={task.priority.toLowerCase()}>
      <input
        type="checkbox"
        className={styles.rowCheck}
        checked={done}
        aria-label={`${done ? "Reopen" : "Complete"} ${task.title}`}
        onChange={() => onToggleDone(task)}
      />
      <button type="button" className={styles.rowTitle} onClick={() => onOpen(task)}>
        <span>{task.title}</span>
        <span className={styles.rowMeta}>
          <span className={styles.rowPriority}>{task.priority.toLowerCase()}</span>
          {task.status === "InProgress" && <span>in progress</span>}
          {task.dueDate && <span data-overdue={overdue || undefined}>due {formatShortDay(task.dueDate)}</span>}
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
