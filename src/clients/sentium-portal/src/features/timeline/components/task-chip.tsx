import { useDraggable } from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import styles from "../timeline.module.css";
import type { TimelineTask } from "../types/timeline";
import { initialsOf } from "../utils/display";

interface TaskChipProps {
  task: TimelineTask;
  onOpen: (task: TimelineTask) => void;
  overlay?: boolean;
}

/**
 * A task in a day cell. Drag it to another day or to "Pinned to month"; click it to edit (a short pointer move
 * threshold keeps clicks from starting a drag).
 */
export function TaskChip({ task, onOpen, overlay }: TaskChipProps) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `task:${task.id}`,
    data: { task },
    disabled: overlay,
  });

  return (
    <button
      type="button"
      ref={overlay ? undefined : setNodeRef}
      className={styles.chip}
      data-priority={task.priority.toLowerCase()}
      data-done={task.status === "Done" || undefined}
      data-dragging={isDragging || undefined}
      data-overlay={overlay || undefined}
      style={overlay ? undefined : { transform: CSS.Translate.toString(transform) }}
      title={task.title}
      aria-label={`Edit task: ${task.title}`}
      onClick={() => onOpen(task)}
      {...(overlay ? {} : attributes)}
      {...(overlay ? {} : listeners)}
    >
      <span className={styles.chipDot} aria-hidden="true" />
      <span className={styles.chipTitle}>{task.title}</span>
      {task.assignedAgent && (
        <span className={styles.chipAgent} aria-hidden="true">
          {initialsOf(task.assignedAgent)}
        </span>
      )}
    </button>
  );
}
