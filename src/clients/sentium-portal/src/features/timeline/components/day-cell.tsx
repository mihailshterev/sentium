import { useDroppable } from "@dnd-kit/core";
import { Plus, StickyNote } from "lucide-react";
import styles from "../timeline.module.css";
import type { TimelineProposal, TimelineTask } from "../types/timeline";
import { formatDayLabel } from "../utils/calendar";
import { ProposalGhost, type ProposalActions } from "./proposal-card";
import { TaskChip } from "./task-chip";

const VISIBLE_TASKS = 3;

interface DayCellProps extends ProposalActions {
  date: string;
  tasks: TimelineTask[];
  proposals: TimelineProposal[];
  noteCount: number;
  isPast: boolean;
  isToday: boolean;
  weekend: boolean;
  selected: boolean;
  onSelect: (date: string) => void;
  onAddTask: (date: string) => void;
  onOpenTask: (task: TimelineTask) => void;
}

export function DayCell({
  date,
  tasks,
  proposals,
  noteCount,
  isPast,
  isToday,
  weekend,
  selected,
  onSelect,
  onAddTask,
  onOpenTask,
  ...proposalActions
}: DayCellProps) {
  const { setNodeRef, isOver } = useDroppable({ id: `day:${date}` });
  const label = formatDayLabel(date);
  const day = Number(date.slice(8));
  const hidden = tasks.length - VISIBLE_TASKS;

  return (
    <li
      ref={setNodeRef}
      className={styles.day}
      data-past={isPast || undefined}
      data-today={isToday || undefined}
      data-weekend={weekend || undefined}
      data-over={isOver || undefined}
      data-selected={selected || undefined}
      data-testid={`day-${date}`}
    >
      <div className={styles.dayHead}>
        <button
          type="button"
          className={styles.dayNumber}
          aria-label={`${label}${isToday ? " (today)" : ""}, ${tasks.length} ${tasks.length === 1 ? "task" : "tasks"}`}
          aria-pressed={selected}
          onClick={() => onSelect(date)}
        >
          {day}
        </button>
        {noteCount > 0 && (
          <span className={styles.dayNote} title={`${noteCount} note(s)`}>
            <StickyNote size={11} aria-hidden="true" />
          </span>
        )}
        <button
          type="button"
          className={styles.dayAdd}
          aria-label={`Add task on ${label}`}
          onClick={() => onAddTask(date)}
        >
          <Plus size={12} />
        </button>
      </div>

      <div className={styles.dayItems}>
        {tasks.slice(0, VISIBLE_TASKS).map((task) => (
          <TaskChip key={task.id} task={task} onOpen={onOpenTask} />
        ))}
        {hidden > 0 && (
          <button type="button" className={styles.dayMore} onClick={() => onSelect(date)}>
            +{hidden} more
          </button>
        )}
        {proposals.map((proposal) => (
          <ProposalGhost key={proposal.id} proposal={proposal} {...proposalActions} />
        ))}
      </div>
    </li>
  );
}

interface OutsideDayCellProps {
  date: string;
  weekend: boolean;
  onOpen: (date: string) => void;
}

export function OutsideDayCell({ date, weekend, onOpen }: OutsideDayCellProps) {
  return (
    <li className={styles.outside} data-weekend={weekend || undefined}>
      <button
        type="button"
        className={styles.dayNumber}
        aria-label={`Open ${formatDayLabel(date)}`}
        onClick={() => onOpen(date)}
      >
        {Number(date.slice(8))}
      </button>
    </li>
  );
}
