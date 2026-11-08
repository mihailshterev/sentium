import { useState } from "react";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import { Bot, Plus, X } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import type {
  AgentChatContext,
  CreateNotePayload,
  MonthDetail,
  TaskPlacement,
  TimelineNote,
  TimelineTask,
} from "../types/timeline";
import { WEEKDAYS, daysInMonth, monthGrid, monthLabel, monthState, toIsoDate } from "../utils/calendar";
import { dropTarget } from "../utils/display";
import { tasksOnDay } from "../utils/month-cache";
import { DayCell } from "./day-cell";
import { MonthSidePanel } from "./month-side-panel";
import type { ProposalActions } from "./proposal-card";
import { TaskChip } from "./task-chip";

interface MonthCalendarProps extends ProposalActions {
  year: number;
  month: number;
  detail: MonthDetail | undefined;
  isLoading: boolean;
  today: Date;
  selectedDay: string | null;
  onSelectDay: (date: string | null) => void;
  onClose: () => void;
  onAddTask: (date: string | null) => void;
  onOpenTask: (task: TimelineTask) => void;
  onMoveTask: (task: TimelineTask, target: TaskPlacement) => void;
  onToggleDone: (task: TimelineTask) => void;
  onAskAgent: (context: AgentChatContext) => void;
  onAddNote: (payload: CreateNotePayload) => void;
  onDeleteNote: (note: TimelineNote) => void;
  isSavingNote: boolean;
}

/**
 * An expanded month: the calendar grid (drag tasks between days or onto "Pinned to month"), and the side panel with
 * the selected day's agenda, pinned tasks, agent suggestions and notes.
 */
export function MonthCalendar({
  year,
  month,
  detail,
  isLoading,
  today,
  selectedDay,
  onSelectDay,
  onClose,
  onAddTask,
  onOpenTask,
  onMoveTask,
  onToggleDone,
  onAskAgent,
  onAddNote,
  onDeleteNote,
  isSavingNote,
  ...proposalActions
}: MonthCalendarProps) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));
  const [dragging, setDragging] = useState<TimelineTask | null>(null);

  const label = monthLabel(year, month);
  const todayIso = toIsoDate(today);
  const state = monthState(year, month, today);
  const days = daysInMonth(year, month);
  const elapsed = state === "past" ? days : state === "current" ? today.getDate() : 0;
  const tasks = detail?.tasks ?? [];
  const notes = detail?.notes ?? [];
  const proposals = detail?.proposals ?? [];

  const onDragStart = (event: DragStartEvent) => setDragging((event.active.data.current?.task as TimelineTask) ?? null);

  const onDragEnd = (event: DragEndEvent) => {
    setDragging(null);
    const task = event.active.data.current?.task as TimelineTask | undefined;
    const target = dropTarget(event.over ? String(event.over.id) : null, year, month);
    if (!task || !target) return;
    if (task.year === target.year && task.month === target.month && task.date === target.date) return;
    onMoveTask(task, target);
  };

  return (
    <section className={styles.month} aria-labelledby="timeline-month-title" data-testid="month-calendar">
      <header className={styles.monthHead}>
        <div className={styles.monthTitle}>
          <h2 id="timeline-month-title">{label}</h2>
          <div className={styles.monthProgress} aria-label={`${elapsed} of ${days} days passed`}>
            <span className={styles.monthBar}>
              <span style={{ width: `${(elapsed / days) * 100}%` }} />
            </span>
            <span className={styles.monthProgressLabel}>
              {state === "current" ? `day ${elapsed} / ${days}` : state === "past" ? "past" : "ahead"}
            </span>
          </div>
        </div>
        <div className={styles.monthActions}>
          <Button size="sm" icon={<Plus size={14} />} onClick={() => onAddTask(selectedDay)}>
            Add task
          </Button>
          <Button size="sm" icon={<Bot size={14} />} onClick={() => onAskAgent({ kind: "month", year, month })}>
            Plan with agent
          </Button>
          <Button
            size="sm"
            variant="ghost"
            iconOnly
            icon={<X size={16} />}
            aria-label={`Collapse ${label}`}
            onClick={onClose}
          />
        </div>
      </header>

      <DndContext
        sensors={sensors}
        onDragStart={onDragStart}
        onDragEnd={onDragEnd}
        onDragCancel={() => setDragging(null)}
      >
        <div className={styles.monthBody} aria-busy={isLoading || undefined}>
          <div className={styles.calendar}>
            <div className={styles.weekdays} aria-hidden="true">
              {WEEKDAYS.map((day) => (
                <span key={day}>{day}</span>
              ))}
            </div>
            <ol className={styles.grid} aria-label={`Days of ${label}`}>
              {monthGrid(year, month).map((date, i) =>
                date ? (
                  <DayCell
                    key={date}
                    date={date}
                    tasks={tasksOnDay(tasks, date)}
                    proposals={proposals.filter((p) => p.date === date)}
                    noteCount={notes.filter((n) => n.date === date).length}
                    isPast={date < todayIso}
                    isToday={date === todayIso}
                    selected={date === selectedDay}
                    onSelect={onSelectDay}
                    onAddTask={onAddTask}
                    onOpenTask={onOpenTask}
                    {...proposalActions}
                  />
                ) : (
                  <li key={`blank-${i}`} className={styles.blank} aria-hidden="true" />
                ),
              )}
            </ol>
          </div>

          <MonthSidePanel
            year={year}
            month={month}
            selectedDay={selectedDay}
            tasks={tasks}
            notes={notes}
            proposals={proposals}
            today={todayIso}
            onAddTask={onAddTask}
            onOpenTask={onOpenTask}
            onToggleDone={onToggleDone}
            onAskAgent={(task) => onAskAgent({ kind: "task", task })}
            onAddNote={onAddNote}
            onDeleteNote={onDeleteNote}
            isSavingNote={isSavingNote}
            {...proposalActions}
          />
        </div>

        <DragOverlay dropAnimation={null}>
          {dragging && <TaskChip task={dragging} onOpen={() => {}} overlay />}
        </DragOverlay>
      </DndContext>
    </section>
  );
}
