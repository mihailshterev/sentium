import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { AnimatePresence, MotionConfig, motion } from "framer-motion";
import { CalendarRange, Sparkles } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { Page } from "@/shared/ui/page";
import { StatusMessage } from "@/shared/ui/status-message";
import { ApiError } from "@/shared/api/client";
import { useMediaQuery } from "@/shared/hooks/useMediaQuery";
import { useAgentRoster } from "@/features/agents/hooks/useAgentRoster";
import { AgentChatDrawer } from "../components/agent-chat-drawer";
import { MonthCalendar } from "../components/month-calendar";
import { ProposalsDialog } from "../components/proposals-dialog";
import { TaskFormDialog } from "../components/task-form-dialog";
import { UpNext } from "../components/up-next";
import { YearTimeline } from "../components/year-timeline";
import {
  useMonthDetail,
  useNoteMutations,
  usePendingProposals,
  useProposalMutations,
  useTaskMutations,
  useUpcoming,
  useYearOverviews,
} from "../hooks/useTimeline";
import { useToday } from "../hooks/useToday";
import type { TaskFormDefaults } from "../schemas/timeline-task.form";
import type {
  AgentChatContext,
  SaveTaskPayload,
  TaskPlacement,
  TimelineProposal,
  TimelineTask,
} from "../types/timeline";
import { isoMonth, toIsoDate } from "../utils/calendar";
import { MAX_MONTH_INDEX, MIN_MONTH_INDEX, monthAt, monthIndex } from "../utils/timeline-geometry";

const MIN_YEAR = 2000;
const MAX_YEAR = 2100;
const NOTICE_MS = 6000;

const clampYear = (value: number) => Math.min(MAX_YEAR, Math.max(MIN_YEAR, value));

const errorText = (error: unknown, fallback: string) =>
  error instanceof ApiError && error.message ? error.message : fallback;

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

const isEditable = (target: EventTarget | null) =>
  target instanceof HTMLElement &&
  (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));

interface EditorState {
  task: TimelineTask | null;
  defaults: TaskFormDefaults;
}

export function TimelineView() {
  const today = useToday();
  const todayIso = toIsoDate(today);
  const [params, setParams] = useSearchParams();

  const todayIndex = monthIndex(today.getFullYear(), today.getMonth() + 1);
  const yearParam = Number(params.get("year"));
  const year = clampYear(yearParam || today.getFullYear());
  const monthParam = Number(params.get("month"));
  const selectedMonth = monthParam >= 1 && monthParam <= 12 ? monthParam : null;
  const dayParam = params.get("day");
  const linkedDay =
    selectedMonth && dayParam && ISO_DATE.test(dayParam) && dayParam.startsWith(`${isoMonth(year, selectedMonth)}-`)
      ? dayParam
      : null;

  const narrow = useMediaQuery("(max-width: 720px)");

  const [home, setHome] = useState(() =>
    selectedMonth
      ? monthIndex(year, selectedMonth)
      : yearParam && year !== today.getFullYear()
        ? monthIndex(year, 1)
        : todayIndex,
  );
  if (selectedMonth && home !== monthIndex(year, selectedMonth)) {
    setHome(monthIndex(year, selectedMonth));
  }

  const [railYears, setRailYears] = useState(() => [monthAt(home).year]);
  const onYearsChange = useCallback(
    (years: number[]) => setRailYears((previous) => (previous.join() === years.join() ? previous : years)),
    [],
  );

  const { overviews, isError } = useYearOverviews(railYears, todayIso);
  const { upcoming } = useUpcoming(todayIso);
  const { detail, isLoading: monthLoading } = useMonthDetail(year, selectedMonth);
  const { proposals: pending } = usePendingProposals();
  const { roster } = useAgentRoster();
  const taskMutations = useTaskMutations();
  const noteMutations = useNoteMutations();
  const proposalMutations = useProposalMutations();

  const defaultDay = () =>
    linkedDay ??
    (selectedMonth && year === today.getFullYear() && selectedMonth === today.getMonth() + 1 ? todayIso : null);

  const [selectedDay, setSelectedDay] = useState<string | null>(defaultDay);
  const [editor, setEditor] = useState<EditorState | null>(null);
  const [toDelete, setToDelete] = useState<TimelineTask | null>(null);
  const [chat, setChat] = useState<AgentChatContext | null>(null);
  const [showProposals, setShowProposals] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const chatTrigger = useRef<HTMLElement | null>(null);

  // The day selection belongs to the open month.
  const [dayMonth, setDayMonth] = useState(`${year}-${selectedMonth}`);
  if (dayMonth !== `${year}-${selectedMonth}`) {
    setDayMonth(`${year}-${selectedMonth}`);
    setSelectedDay(defaultDay());
  }

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), NOTICE_MS);
    return () => clearTimeout(timer);
  }, [notice]);

  const go = (nextYear: number, month: number | null, day?: string | null) => {
    const next = new URLSearchParams();
    next.set("year", String(clampYear(nextYear)));
    if (month) next.set("month", String(month));
    if (month && day) next.set("day", day);
    setParams(next);
  };

  const selectMonth = (monthYear: number, month: number) =>
    monthYear === year && month === selectedMonth ? go(year, null) : go(monthYear, month);
  const collapse = () => go(year, null);

  const shownIndex = selectedMonth ? monthIndex(year, selectedMonth) : null;
  const pendingStep = useRef<number | null>(null);
  useEffect(() => {
    pendingStep.current = null;
  }, [shownIndex]);

  const stepMonth = (delta: number) => {
    if (shownIndex === null) return;
    const index = Math.min(MAX_MONTH_INDEX, Math.max(MIN_MONTH_INDEX, (pendingStep.current ?? shownIndex) + delta));
    pendingStep.current = index;
    const target = monthAt(index);
    go(target.year, target.month);
  };

  const openDay = (date: string) => go(Number(date.slice(0, 4)), Number(date.slice(5, 7)), date);
  const openTaskMonth = (task: TimelineTask) => go(task.year, task.month, task.date);

  const openChat = (context: AgentChatContext) => {
    chatTrigger.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setChat(context);
  };

  const closeChat = () => {
    setChat(null);
    chatTrigger.current?.focus();
  };

  const dialogOpen = editor !== null || toDelete !== null || showProposals;

  const stepRef = useRef(stepMonth);
  useEffect(() => {
    stepRef.current = stepMonth;
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (dialogOpen) return;
      if ((e.key === "ArrowLeft" || e.key === "ArrowRight") && selectedMonth) {
        if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey || isEditable(e.target)) return;
        e.preventDefault();
        stepRef.current(e.key === "ArrowLeft" ? -1 : 1);
        return;
      }
      if (e.key !== "Escape") return;
      if (chat) {
        setChat(null);
        chatTrigger.current?.focus();
      } else if (selectedMonth) {
        const next = new URLSearchParams();
        next.set("year", String(year));
        setParams(next);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [dialogOpen, chat, selectedMonth, year, setParams]);

  const defaultsFor = (date: string | null): TaskFormDefaults => ({
    year,
    month: selectedMonth ?? (year === today.getFullYear() ? today.getMonth() + 1 : 1),
    date,
  });

  const saveTask = (payload: SaveTaskPayload) => {
    if (!editor) return;
    const close = { onSuccess: () => setEditor(null) };
    if (editor.task) {
      taskMutations.update.mutate({ id: editor.task.id, payload }, close);
    } else {
      taskMutations.create.mutate(payload, close);
    }
  };

  const moveTask = (task: TimelineTask, target: TaskPlacement) =>
    taskMutations.schedule.mutate(
      { task, target },
      { onError: (error) => setNotice(errorText(error, `"${task.title}" could not be moved.`)) },
    );

  const toggleDone = (task: TimelineTask) =>
    taskMutations.setStatus.mutate(
      { task, status: task.status === "Done" ? "Todo" : "Done" },
      { onError: (error) => setNotice(errorText(error, `"${task.title}" could not be updated.`)) },
    );

  const acceptProposal = (proposal: TimelineProposal) =>
    proposalMutations.accept.mutate(proposal, {
      onError: (error) =>
        setNotice(
          error instanceof ApiError && error.isConflict
            ? "That suggestion is out of date - the task changed since it was made."
            : errorText(error, "The suggestion could not be accepted."),
        ),
    });

  const rejectProposal = (proposal: TimelineProposal) =>
    proposalMutations.reject.mutate(proposal, {
      onError: (error) => setNotice(errorText(error, "The suggestion could not be rejected.")),
    });

  const busyProposalId = proposalMutations.accept.isPending
    ? proposalMutations.accept.variables?.id
    : proposalMutations.reject.isPending
      ? proposalMutations.reject.variables?.id
      : null;

  const proposalActions = { onAccept: acceptProposal, onReject: rejectProposal, busyId: busyProposalId };

  const tagSuggestions = useMemo(
    () => [...new Set((detail?.tasks ?? []).flatMap((t) => t.tags))].sort(),
    [detail?.tasks],
  );

  const mutationError = editor?.task ? taskMutations.update.error : taskMutations.create.error;

  return (
    <MotionConfig reducedMotion="user">
      <Page
        title="Timeline"
        subtitle="Your year at a glance: tasks, notes, and what your agents suggest"
        icon={<CalendarRange size={20} />}
        width="wide"
        actions={
          <div className={styles.headerActions}>
            <Button size="sm" onClick={() => go(today.getFullYear(), today.getMonth() + 1)}>
              Today
            </Button>
            <Button
              size="sm"
              icon={<Sparkles size={14} />}
              onClick={() => setShowProposals(true)}
              aria-label={`Suggestions${pending.length > 0 ? ` (${pending.length})` : ""}`}
              data-testid="timeline-suggestions"
            >
              <span className={styles.actionLabel}>Suggestions</span>
              {pending.length > 0 ? ` (${pending.length})` : ""}
            </Button>
          </div>
        }
      >
        {notice && <StatusMessage variant="warning" message={notice} className={styles.notice} />}
        {isError && <StatusMessage variant="error" message="The timeline could not be loaded." />}

        <div className={styles.layout} data-chat={chat ? "open" : undefined}>
          <div className={styles.main}>
            <YearTimeline
              overviews={overviews}
              today={today}
              selected={selectedMonth ? { year, month: selectedMonth } : null}
              home={home}
              overdueCount={upcoming?.overdueCount ?? 0}
              narrow={narrow}
              onSelectMonth={selectMonth}
              onYearsChange={onYearsChange}
            />

            <AnimatePresence mode="popLayout" initial={false}>
              {selectedMonth ? (
                <motion.div
                  key={`${year}-${selectedMonth}`}
                  initial={{ opacity: 0, y: -16, scaleY: 0.94 }}
                  animate={{ opacity: 1, y: 0, scaleY: 1 }}
                  exit={{ opacity: 0, y: -10, scaleY: 0.97 }}
                  transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
                  style={{ transformOrigin: "50% 0%" }}
                >
                  <MonthCalendar
                    year={year}
                    month={selectedMonth}
                    detail={detail}
                    isLoading={monthLoading}
                    today={today}
                    selectedDay={selectedDay}
                    onSelectDay={setSelectedDay}
                    onClose={collapse}
                    onStepMonth={stepMonth}
                    onOpenDay={openDay}
                    onAddTask={(date) => setEditor({ task: null, defaults: defaultsFor(date) })}
                    onOpenTask={(task) => setEditor({ task, defaults: defaultsFor(task.date) })}
                    onMoveTask={moveTask}
                    onToggleDone={toggleDone}
                    onAskAgent={openChat}
                    onAddNote={(payload) =>
                      noteMutations.create.mutate(payload, {
                        onError: (error) => setNotice(errorText(error, "The note could not be saved.")),
                      })
                    }
                    onDeleteNote={(note) => noteMutations.remove.mutate(note.id)}
                    isSavingNote={noteMutations.create.isPending}
                    {...proposalActions}
                  />
                </motion.div>
              ) : (
                <motion.div key="up-next" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
                  <UpNext
                    upcoming={upcoming}
                    today={todayIso}
                    onOpenTask={openTaskMonth}
                    onToggleDone={toggleDone}
                    onAskAgent={(task) => openChat({ kind: "task", task })}
                    onShowSuggestions={() => setShowProposals(true)}
                    onPlanMonth={() =>
                      openChat({ kind: "month", year: today.getFullYear(), month: today.getMonth() + 1 })
                    }
                  />
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          <AnimatePresence>
            {chat && (
              <motion.div
                className={styles.drawerWrap}
                initial={{ opacity: 0, x: 32 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 32 }}
                transition={{ duration: 0.22 }}
              >
                <AgentChatDrawer context={chat} today={todayIso} onClose={closeChat} />
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        {editor && (
          <TaskFormDialog
            task={editor.task}
            defaults={editor.defaults}
            roster={roster}
            tagSuggestions={tagSuggestions}
            onSubmit={saveTask}
            onCancel={() => {
              taskMutations.create.reset();
              taskMutations.update.reset();
              setEditor(null);
            }}
            onDelete={(task) => setToDelete(task)}
            onDiscuss={(task) => {
              setEditor(null);
              openChat({ kind: "task", task });
            }}
            isPending={taskMutations.create.isPending || taskMutations.update.isPending}
            error={mutationError ? errorText(mutationError, "The task could not be saved.") : null}
          />
        )}

        <ConfirmDialog
          open={toDelete !== null}
          variant="danger"
          title="Delete task"
          description={toDelete ? `Delete "${toDelete.title}" with its notes and pending suggestions?` : ""}
          confirmLabel="Delete"
          cancelLabel="Cancel"
          onConfirm={() =>
            toDelete &&
            taskMutations.remove.mutate(toDelete.id, {
              onSuccess: () => {
                setToDelete(null);
                setEditor(null);
              },
            })
          }
          onCancel={() => setToDelete(null)}
        />

        {showProposals && (
          <ProposalsDialog proposals={pending} onClose={() => setShowProposals(false)} {...proposalActions} />
        )}
      </Page>
    </MotionConfig>
  );
}
