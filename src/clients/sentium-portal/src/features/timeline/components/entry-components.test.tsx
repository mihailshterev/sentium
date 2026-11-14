import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { DndContext } from "@dnd-kit/core";
import { task } from "../test-fixtures";
import { SeriesScopeDialog } from "./series-scope-dialog";
import { TaskChip } from "./task-chip";
import { TaskFormDialog } from "./task-form-dialog";
import { TaskRow } from "./task-row";

const concert = task({ id: "event-1", kind: "CalendarEvent", title: "Concert", startTime: "18:30:00" });

const renderChip = (entry = task()) => {
  const onOpen = vi.fn();
  const onToggleDone = vi.fn();
  render(
    <DndContext>
      <TaskChip task={entry} onOpen={onOpen} onToggleDone={onToggleDone} />
    </DndContext>,
  );
  return { onOpen, onToggleDone };
};

describe("TaskChip", () => {
  it("ticks a task done from its chip", () => {
    const dentist = task();
    const { onToggleDone, onOpen } = renderChip(dentist);

    fireEvent.click(screen.getByRole("checkbox", { name: "Complete Book dentist" }));

    expect(onToggleDone).toHaveBeenCalledWith(dentist);
    expect(onOpen).not.toHaveBeenCalled();
  });

  it("shows a done task ticked, to reopen it", () => {
    renderChip(task({ status: "Done" }));
    expect(screen.getByRole("checkbox", { name: "Reopen Book dentist" })).toBeChecked();
  });

  it("gives an event no tick box, its time, and a past look once over", () => {
    renderChip({ ...concert, isPast: true });

    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
    const chip = screen.getByRole("button", { name: "Edit event: Concert" });
    expect(chip).toHaveAttribute("data-kind", "event");
    expect(chip).toHaveAttribute("data-past");
    expect(chip).toHaveTextContent("18:30");
  });
});

describe("TaskRow", () => {
  it("lists an event without a tick box", () => {
    render(
      <ul>
        <TaskRow task={concert} today="2026-11-08" onOpen={vi.fn()} onToggleDone={vi.fn()} onAskAgent={vi.fn()} />
      </ul>,
    );

    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
    expect(screen.getByText("event")).toBeInTheDocument();
    expect(screen.getByText("18:30")).toBeInTheDocument();
  });
});

describe("TaskFormDialog kinds", () => {
  const renderForm = (props: Partial<Parameters<typeof TaskFormDialog>[0]> = {}) => {
    const onSubmit = vi.fn();
    render(
      <TaskFormDialog
        defaults={{ year: 2026, month: 11, date: "2026-11-21" }}
        roster={[]}
        tagSuggestions={[]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        isPending={false}
        {...props}
      />,
    );
    return { onSubmit };
  };

  it("switches to an event, hiding the task-only fields and offering an end day", async () => {
    const { onSubmit } = renderForm();

    fireEvent.click(screen.getByLabelText("Event"));

    expect(screen.queryByLabelText("Priority")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Status")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Assigned agent")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Due by")).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Concert" } });
    fireEvent.change(screen.getByLabelText("Time"), { target: { value: "18:30" } });
    fireEvent.change(screen.getByLabelText("Ends"), { target: { value: "2026-11-22" } });
    fireEvent.click(screen.getByRole("button", { name: "Add event" }));

    await vi.waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          kind: "CalendarEvent",
          startTime: "18:30:00",
          endDate: "2026-11-22",
          priority: null,
          status: null,
        }),
      ),
    );
  });

  it("never lets an existing entry change its kind", () => {
    renderForm({ task: concert });

    expect(screen.queryByLabelText("Task")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Edit event" })).toBeInTheDocument();
  });

  it("sends a weekly repeat and a reminder", async () => {
    const { onSubmit } = renderForm();

    fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Gym" } });
    fireEvent.change(screen.getByLabelText("Repeat"), { target: { value: "Weekly" } });
    fireEvent.click(screen.getByRole("checkbox", { name: "Mo" }));
    fireEvent.change(screen.getByLabelText("Reminder"), { target: { value: "1440" } });
    fireEvent.click(screen.getByRole("button", { name: "Add task" }));

    await vi.waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          repeat: { frequency: "Weekly", interval: 1, weekdays: [1], until: null },
          reminderOffsetMinutes: 1440,
        }),
      ),
    );
  });
});

describe("SeriesScopeDialog", () => {
  it("asks whether a change applies to this occurrence or to every following one", () => {
    const onChoose = vi.fn();
    render(<SeriesScopeDialog action="delete" title="Gym" onChoose={onChoose} onCancel={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "This and all following" }));
    fireEvent.click(screen.getByRole("button", { name: "Only this one" }));

    expect(onChoose.mock.calls).toEqual([["Following"], ["One"]]);
  });
});
