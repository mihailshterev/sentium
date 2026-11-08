import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { monthDetail, note, proposal, task, yearOverview } from "../test-fixtures";
import { MonthCalendar } from "./month-calendar";
import { TaskFormDialog } from "./task-form-dialog";
import { YearTimeline } from "./year-timeline";

const TODAY = new Date(2026, 10, 8, 12, 0);

describe("YearTimeline", () => {
  const overview2026 = {
    ...yearOverview(2026, {
      11: {
        taskCount: 5,
        openCount: 3,
        doneCount: 2,
        overdueCount: 1,
        pendingProposalCount: 2,
        agentNoteCount: 1,
        noteCount: 1,
      },
    }),
    days: [
      { date: "2026-11-12", open: 1, done: 0, urgent: false },
      { date: "2026-11-20", open: 2, done: 0, urgent: true },
      { date: "2026-11-03", open: 0, done: 2, urgent: false },
    ],
  };

  const renderTimeline = (props: Partial<Parameters<typeof YearTimeline>[0]> = {}) => {
    const onSelectMonth = vi.fn();
    const onYearsChange = vi.fn();
    const view = render(
      <YearTimeline
        overviews={new Map([[2026, overview2026]])}
        today={TODAY}
        selected={null}
        home={2026 * 12 + 10}
        overdueCount={1}
        narrow={false}
        onSelectMonth={onSelectMonth}
        onYearsChange={onYearsChange}
        {...props}
      />,
    );
    return { onSelectMonth, onYearsChange, container: view.container };
  };

  it("runs across years around today and marks the current month", () => {
    const { onYearsChange } = renderTimeline();

    const november = screen.getByRole("button", { name: "November 2026, 3 open tasks, 1 overdue, 2 suggestions" });
    expect(november).toHaveAttribute("aria-current", "date");
    expect(november).toHaveAttribute("data-state", "current");
    expect(screen.getByRole("button", { name: /^October 2026/ })).toHaveAttribute("data-state", "past");
    expect(screen.getByRole("button", { name: /^December 2026/ })).toHaveAttribute("data-state", "future");
    // A year back to scroll to, two ahead.
    expect(screen.getByTestId("timeline-month-2025-11")).toBeInTheDocument();
    expect(screen.getByTestId("timeline-month-2028-11")).toBeInTheDocument();
    expect(onYearsChange).toHaveBeenCalledWith([2025, 2026, 2027, 2028]);
  });

  it("reports the year's progress up to today and what is overdue", () => {
    renderTimeline();
    expect(screen.getByText("day 312 / 365")).toBeInTheDocument();
    expect(screen.getByLabelText("85 percent of 2026 has passed")).toBeInTheDocument();
    expect(screen.getByText("1 overdue")).toBeInTheDocument();
  });

  it("hangs callouts only on months with something in them", () => {
    renderTimeline();
    expect(screen.getByText("November")).toBeInTheDocument();
    expect(screen.getByText("3 open")).toBeInTheDocument();
    expect(screen.getByText("· 2 done")).toBeInTheDocument();
    expect(screen.queryByText("October")).not.toBeInTheDocument();
    expect(screen.queryByText("nothing planned")).not.toBeInTheDocument();
  });

  it("raises months by how many tasks they hold", () => {
    renderTimeline();
    const top = (id: string) => parseFloat(screen.getByTestId(id).style.top);

    // November holds five tasks; October and December none, so they sit on the baseline.
    expect(top("timeline-month-2026-11")).toBeLessThan(top("timeline-month-2026-10"));
    expect(top("timeline-month-2026-10")).toBe(top("timeline-month-2026-12"));
  });

  it("ticks the days with tasks on the axis", () => {
    const { container } = renderTimeline();
    const dots = container.querySelectorAll("circle[data-tone]");
    expect(dots).toHaveLength(3);
    expect([...dots].map((dot) => dot.getAttribute("data-tone")).sort()).toEqual(["done", "open", "urgent"]);
  });

  it("opens a month from its node or its callout", () => {
    const { onSelectMonth } = renderTimeline();

    fireEvent.click(screen.getByRole("button", { name: /^March 2027/ }));
    fireEvent.click(screen.getByText("November"));

    expect(onSelectMonth).toHaveBeenNthCalledWith(1, 2027, 3);
    expect(onSelectMonth).toHaveBeenNthCalledWith(2, 2026, 11);
  });

  it("collapses to a strip without callouts while a month is open", () => {
    renderTimeline({ selected: { year: 2026, month: 11 } });

    expect(screen.queryByText("November")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^November 2026/ })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("figure")).toHaveAttribute("data-layout", "strip");
  });

  it("rebuilds the rail around a month far away", () => {
    renderTimeline({ selected: { year: 2040, month: 6 } });
    expect(screen.getByRole("button", { name: /^June 2040/ })).toHaveAttribute("aria-expanded", "true");
  });
});

describe("MonthCalendar", () => {
  const renderMonth = () => {
    const handlers = {
      onSelectDay: vi.fn(),
      onClose: vi.fn(),
      onStepMonth: vi.fn(),
      onOpenDay: vi.fn(),
      onAddTask: vi.fn(),
      onOpenTask: vi.fn(),
      onMoveTask: vi.fn(),
      onToggleDone: vi.fn(),
      onAskAgent: vi.fn(),
      onAddNote: vi.fn(),
      onDeleteNote: vi.fn(),
      onAccept: vi.fn(),
      onReject: vi.fn(),
    };
    const dentist = task({ assignedAgent: "ResearchAnalyst" });
    render(
      <MonthCalendar
        year={2026}
        month={11}
        detail={monthDetail({
          tasks: [dentist, task({ id: "pinned", title: "Renew passport", date: null })],
          notes: [note()],
          proposals: [proposal()],
        })}
        isLoading={false}
        today={TODAY}
        selectedDay="2026-11-12"
        isSavingNote={false}
        {...handlers}
      />,
    );
    return { handlers, dentist };
  };

  it("lays out the month with today highlighted and past days dimmed", () => {
    renderMonth();

    expect(screen.getByRole("heading", { level: 2, name: "November 2026" })).toBeInTheDocument();
    expect(screen.getByTestId("day-2026-11-08")).toHaveAttribute("data-today");
    expect(screen.getByTestId("day-2026-11-07")).toHaveAttribute("data-past");
    expect(screen.getByTestId("day-2026-11-30")).not.toHaveAttribute("data-past");
    expect(screen.getByLabelText("8 of 30 days passed")).toBeInTheDocument();
  });

  it("steps between months and opens the neighbouring months' days", () => {
    const { handlers } = renderMonth();

    fireEvent.click(screen.getByRole("button", { name: "Previous month" }));
    fireEvent.click(screen.getByRole("button", { name: "Next month" }));
    fireEvent.click(screen.getByRole("button", { name: "Open 26 October" }));
    fireEvent.click(screen.getByRole("button", { name: "Open 6 December" }));

    expect(handlers.onStepMonth.mock.calls).toEqual([[-1], [1]]);
    expect(handlers.onOpenDay.mock.calls).toEqual([["2026-10-26"], ["2026-12-06"]]);
    expect(screen.getByTestId("day-2026-11-08")).toHaveAttribute("data-weekend");
    expect(screen.getByTestId("day-2026-11-09")).not.toHaveAttribute("data-weekend");
    // The selected day (12th) says how far away it is.
    expect(screen.getByText("in 4 days")).toBeInTheDocument();
  });

  it("adds tasks on a day and opens tasks for editing", () => {
    const { handlers, dentist } = renderMonth();

    fireEvent.click(screen.getByRole("button", { name: "Add task on 20 November" }));
    fireEvent.click(
      within(screen.getByTestId("day-2026-11-12")).getByRole("button", { name: "Edit task: Book dentist" }),
    );

    expect(handlers.onAddTask).toHaveBeenCalledWith("2026-11-20");
    expect(handlers.onOpenTask).toHaveBeenCalledWith(dentist);
  });

  it("shows the selected day's agenda, pinned tasks, suggestions and notes", () => {
    const { handlers, dentist } = renderMonth();

    fireEvent.click(screen.getByRole("checkbox", { name: "Complete Book dentist" }));
    fireEvent.click(screen.getByRole("button", { name: "Ask ResearchAnalyst about Book dentist" }));
    expect(handlers.onToggleDone).toHaveBeenCalledWith(dentist);
    expect(handlers.onAskAgent).toHaveBeenCalledWith({ kind: "task", task: dentist });

    expect(within(screen.getByTestId("pinned-zone")).getByText("Renew passport")).toBeInTheDocument();
    expect(screen.getByText(/Reviewed/)).toBeInTheDocument();

    fireEvent.click(
      within(screen.getByTestId("proposal-proposal-1")).getByRole("button", {
        name: 'Accept suggestion: Add "Plan winter trip"',
      }),
    );
    expect(handlers.onAccept).toHaveBeenCalledWith(expect.objectContaining({ id: "proposal-1" }));
  });

  it("asks the planner about the month and writes notes", () => {
    const { handlers } = renderMonth();

    fireEvent.click(screen.getByRole("button", { name: "Plan with agent" }));
    fireEvent.change(screen.getByRole("textbox", { name: "New note" }), { target: { value: "Busy week" } });
    fireEvent.click(screen.getByRole("button", { name: "Add note" }));
    fireEvent.click(screen.getByRole("button", { name: "Collapse November 2026" }));

    expect(handlers.onAskAgent).toHaveBeenCalledWith({ kind: "month", year: 2026, month: 11 });
    expect(handlers.onAddNote).toHaveBeenCalledWith({
      year: 2026,
      month: 11,
      date: "2026-11-12",
      taskId: null,
      content: "Busy week",
    });
    expect(handlers.onClose).toHaveBeenCalled();
  });
});

describe("TaskFormDialog", () => {
  const roster = [
    { name: "GeneralAssistant", description: "General", kind: "BuiltIn" as const },
    { name: "TimelinePlanner", description: "Plans", kind: "BuiltIn" as const },
  ];

  it("creates a task on the chosen day", async () => {
    const onSubmit = vi.fn();
    render(
      <TaskFormDialog
        defaults={{ year: 2026, month: 11, date: "2026-11-14" }}
        roster={roster}
        tagSuggestions={[]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Dentist" } });
    fireEvent.change(screen.getByLabelText("Priority"), { target: { value: "High" } });
    fireEvent.change(screen.getByLabelText("Assigned agent"), { target: { value: "TimelinePlanner" } });
    fireEvent.click(screen.getByRole("button", { name: "Add task" }));

    await vi.waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Dentist",
          year: 2026,
          month: 11,
          date: "2026-11-14",
          priority: "High",
          assignedAgent: "TimelinePlanner",
        }),
      ),
    );
  });

  it("keeps an assignee that no longer exists visible", () => {
    render(
      <TaskFormDialog
        task={task({ assignedAgent: "Deleted Bot" })}
        defaults={{ year: 2026, month: 11, date: null }}
        roster={roster}
        tagSuggestions={[]}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
        onDiscuss={vi.fn()}
        isPending={false}
      />,
    );

    expect(screen.getByRole("option", { name: "Deleted Bot (unavailable)" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Discuss with Deleted Bot" })).toBeInTheDocument();
  });

  it("asks for a day unless the task is pinned", async () => {
    const onSubmit = vi.fn();
    render(
      <TaskFormDialog
        defaults={{ year: 2026, month: 11, date: null }}
        roster={roster}
        tagSuggestions={[]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Plan" } });
    fireEvent.click(screen.getByLabelText("On a day"));
    fireEvent.click(screen.getByRole("button", { name: "Add task" }));

    expect(await screen.findByText("Choose a day, or pin the task to the month")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
