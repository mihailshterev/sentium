import { useState } from "react";
import { useDroppable } from "@dnd-kit/core";
import Markdown from "react-markdown";
import { Bot, Pin, Plus, Trash2, User } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import { Textarea } from "@/shared/ui/form-controls";
import type { CreateNotePayload, TimelineNote, TimelineProposal, TimelineTask } from "../types/timeline";
import { MONTH_NAMES, formatDayLabel, formatLongDay, relativeDay } from "../utils/calendar";
import { pinnedTasks, tasksOnDay } from "../utils/month-cache";
import { TASK_LIMITS } from "../schemas/timeline-task.form";
import { ProposalCard, type ProposalActions } from "./proposal-card";
import { TaskRow } from "./task-row";

const NOTE_KIND_LABELS: Record<TimelineNote["kind"], string> = {
  Note: "note",
  StatusUpdate: "status update",
  ProgressReport: "progress report",
};

interface MonthSidePanelProps extends ProposalActions {
  year: number;
  month: number;
  selectedDay: string | null;
  tasks: TimelineTask[];
  notes: TimelineNote[];
  proposals: TimelineProposal[];
  today: string;
  onAddTask: (date: string | null) => void;
  onOpenTask: (task: TimelineTask) => void;
  onToggleDone: (task: TimelineTask) => void;
  onAskAgent: (task: TimelineTask) => void;
  onAddNote: (payload: CreateNotePayload) => void;
  onDeleteNote: (note: TimelineNote) => void;
  isSavingNote: boolean;
}

export function MonthSidePanel({
  year,
  month,
  selectedDay,
  tasks,
  notes,
  proposals,
  today,
  onAddTask,
  onOpenTask,
  onToggleDone,
  onAskAgent,
  onAddNote,
  onDeleteNote,
  isSavingNote,
  ...proposalActions
}: MonthSidePanelProps) {
  const { setNodeRef, isOver } = useDroppable({ id: "pinned" });
  const [draft, setDraft] = useState("");

  const dayTasks = selectedDay ? tasksOnDay(tasks, selectedDay) : [];
  const pinned = pinnedTasks(tasks);
  const visibleNotes = notes.filter(
    (n) => (n.date === null && n.taskId === null) || (selectedDay !== null && n.date === selectedDay),
  );
  const rowProps = { today, onOpen: onOpenTask, onToggleDone, onAskAgent };
  const monthName = MONTH_NAMES[month - 1];

  const submitNote = (e: React.FormEvent) => {
    e.preventDefault();
    const content = draft.trim();
    if (!content) return;
    onAddNote({ year, month, date: selectedDay, taskId: null, content });
    setDraft("");
  };

  return (
    <aside className={styles.side} aria-label={`${monthName} details`}>
      <section className={styles.sideSection}>
        <header className={styles.sideHead}>
          <h3>
            {selectedDay ? formatLongDay(selectedDay) : "Pick a day"}
            {selectedDay && (
              <span className={styles.relative} data-today={selectedDay === today || undefined}>
                {relativeDay(selectedDay, today)}
              </span>
            )}
          </h3>
          {selectedDay && (
            <Button size="sm" variant="ghost" icon={<Plus size={14} />} onClick={() => onAddTask(selectedDay)}>
              Add
            </Button>
          )}
        </header>
        {!selectedDay ? (
          <p className={styles.sideEmpty}>Select a day in the calendar to see its agenda.</p>
        ) : dayTasks.length === 0 ? (
          <p className={styles.sideEmpty}>Nothing planned.</p>
        ) : (
          <ul className={styles.rows}>
            {dayTasks.map((task) => (
              <TaskRow key={task.id} task={task} {...rowProps} />
            ))}
          </ul>
        )}
      </section>

      <section
        ref={setNodeRef}
        className={styles.sideSection}
        data-drop={isOver || undefined}
        data-testid="pinned-zone"
      >
        <header className={styles.sideHead}>
          <h3>
            <Pin size={13} aria-hidden="true" /> Pinned to {monthName}
          </h3>
          <Button
            size="sm"
            variant="ghost"
            icon={<Plus size={14} />}
            onClick={() => onAddTask(null)}
            aria-label={`Pin a task to ${monthName}`}
          >
            Pin
          </Button>
        </header>
        {pinned.length === 0 ? (
          <p className={styles.sideEmpty}>Drop a task here to keep it in {monthName} without a day.</p>
        ) : (
          <ul className={styles.rows}>
            {pinned.map((task) => (
              <TaskRow key={task.id} task={task} {...rowProps} />
            ))}
          </ul>
        )}
      </section>

      {proposals.length > 0 && (
        <section className={styles.sideSection}>
          <header className={styles.sideHead}>
            <h3>Agent suggestions</h3>
          </header>
          <ul className={styles.proposals}>
            {proposals.map((proposal) => (
              <ProposalCard key={proposal.id} proposal={proposal} {...proposalActions} />
            ))}
          </ul>
        </section>
      )}

      <section className={styles.sideSection}>
        <header className={styles.sideHead}>
          <h3>Notes</h3>
        </header>
        {visibleNotes.length === 0 ? (
          <p className={styles.sideEmpty}>No notes {selectedDay ? "for this day or month" : "for this month"} yet.</p>
        ) : (
          <ul className={styles.notes}>
            {visibleNotes.map((note) => (
              <li key={note.id} className={styles.note} data-author={note.author.toLowerCase()}>
                <div className={styles.noteHead}>
                  {note.author === "Agent" ? (
                    <Bot size={12} aria-hidden="true" />
                  ) : (
                    <User size={12} aria-hidden="true" />
                  )}
                  <span className={styles.noteAuthor}>
                    {note.author === "Agent" ? (note.authorName ?? "Agent") : "You"}
                  </span>
                  <span className={styles.noteKind}>
                    {NOTE_KIND_LABELS[note.kind]}
                    {note.date ? ` · ${formatDayLabel(note.date)}` : ""}
                  </span>
                  <button
                    type="button"
                    className={styles.noteDelete}
                    aria-label="Remove note"
                    onClick={() => onDeleteNote(note)}
                  >
                    <Trash2 size={12} />
                  </button>
                </div>
                <div className={styles.noteBody}>
                  <Markdown>{note.content}</Markdown>
                </div>
              </li>
            ))}
          </ul>
        )}
        <form className={styles.noteForm} onSubmit={submitNote}>
          <Textarea
            rows={2}
            value={draft}
            maxLength={TASK_LIMITS.note}
            placeholder={selectedDay ? `Note for ${formatDayLabel(selectedDay)}…` : `Note for ${monthName}…`}
            aria-label="New note"
            onChange={(e) => setDraft(e.target.value)}
          />
          <Button type="submit" size="sm" disabled={!draft.trim()} loading={isSavingNote}>
            Add note
          </Button>
        </form>
      </section>
    </aside>
  );
}
