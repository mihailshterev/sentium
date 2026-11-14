import { useDraggable } from "@dnd-kit/core";
import styles from "../timeline.module.css";
import type { TimelineTask } from "../types/timeline";
import { formatTime, initialsOf, isEvent } from "../utils/display";

interface TaskChipProps {
  task: TimelineTask;
  onOpen: (task: TimelineTask) => void;
  onToggleDone?: (task: TimelineTask) => void;
  overlay?: boolean;
}

/**
 * An Entry in a day cell. Drag it to another day or to "Pinned to month"; click it to edit (a short pointer move
 * threshold keeps clicks from starting a drag). A Task has a tick box beside it; a Calendar Event has none and fades
 * once past. While dragging, the chip stays put as a faded placeholder and the calendar's drag overlay follows the
 * pointer.
 */
export function TaskChip({ task, onOpen, onToggleDone, overlay }: TaskChipProps) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `task:${task.id}`,
    data: { task },
    disabled: overlay,
  });

  const event = isEvent(task);
  const done = task.status === "Done";
  const time = formatTime(task.startTime);

  return (
    <div className={styles.chipWrap}>
      {!event && onToggleDone && !overlay && (
        <input
          type="checkbox"
          className={styles.chipTick}
          checked={done}
          aria-label={`${done ? "Reopen" : "Complete"} ${task.title}`}
          onChange={() => onToggleDone(task)}
        />
      )}
      <button
        type="button"
        ref={overlay ? undefined : setNodeRef}
        className={styles.chip}
        data-kind={event ? "event" : "task"}
        data-priority={event ? undefined : task.priority.toLowerCase()}
        data-done={(!event && done) || undefined}
        data-past={task.isPast || undefined}
        data-dragging={isDragging || undefined}
        data-overlay={overlay || undefined}
        title={task.title}
        aria-label={`Edit ${event ? "event" : "task"}: ${task.title}`}
        onClick={() => onOpen(task)}
        {...(overlay ? {} : attributes)}
        {...(overlay ? {} : listeners)}
      >
        <span className={styles.chipDot} aria-hidden="true" />
        {time && <span className={styles.chipTime}>{time}</span>}
        <span className={styles.chipTitle}>{task.title}</span>
        {task.assignedAgent && (
          <span className={styles.chipAgent} aria-hidden="true">
            {initialsOf(task.assignedAgent)}
          </span>
        )}
      </button>
    </div>
  );
}
