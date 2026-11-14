import { describe, expect, it } from "vitest";
import { task } from "../test-fixtures";
import { taskFormSchema, toTaskForm, toTaskPayload } from "./timeline-task.form";

describe("timeline task form", () => {
  it("starts a new task on the chosen day, or pinned to the month without one", () => {
    expect(toTaskForm(null, { year: 2026, month: 11, date: "2026-11-14" })).toMatchObject({
      placement: "day",
      date: "2026-11-14",
      month: "2026-11",
    });
    expect(toTaskForm(null, { year: 2026, month: 3, date: null })).toMatchObject({
      placement: "month",
      date: "",
      month: "2026-03",
    });
  });

  it("round-trips an existing task", () => {
    const existing = task({
      tags: ["health"],
      dueDate: "2026-11-20",
      assignedAgent: "TimelinePlanner",
      description: "Call first",
    });
    expect(toTaskPayload(toTaskForm(existing, { year: 2026, month: 11, date: null }))).toEqual({
      kind: "Task",
      title: "Book dentist",
      description: "Call first",
      year: 2026,
      month: 11,
      date: "2026-11-12",
      startTime: null,
      endDate: null,
      dueDate: "2026-11-20",
      priority: "Normal",
      status: "Todo",
      tags: ["health"],
      assignedAgent: "TimelinePlanner",
      repeat: null,
      reminderOffsetMinutes: null,
    });
  });

  it("takes the month from the day, and pins without one", () => {
    const form = toTaskForm(null, { year: 2026, month: 11, date: null });
    expect(toTaskPayload({ ...form, title: " Plan ", placement: "day", date: "2027-01-03" })).toMatchObject({
      year: 2027,
      month: 1,
      date: "2027-01-03",
      title: "Plan",
    });
    expect(toTaskPayload({ ...form, title: "Plan", month: "2026-12" })).toMatchObject({
      year: 2026,
      month: 12,
      date: null,
      assignedAgent: null,
      description: null,
    });
  });

  it("requires a title, and a day unless pinned", () => {
    const form = toTaskForm(null, { year: 2026, month: 11, date: null });
    expect(taskFormSchema.safeParse({ ...form, title: "  " }).success).toBe(false);
    expect(taskFormSchema.safeParse({ ...form, title: "Plan", placement: "day", date: "" }).success).toBe(false);
    expect(taskFormSchema.safeParse({ ...form, title: "Plan" }).success).toBe(true);
    expect(
      taskFormSchema.safeParse({ ...form, title: "Plan", tags: Array.from({ length: 11 }, (_, i) => `t${i}`) }).success,
    ).toBe(false);
  });

  it("sends an event without the task-only fields, with its time and end day", () => {
    const form = toTaskForm(null, { year: 2026, month: 11, date: "2026-11-21" });
    const payload = toTaskPayload({
      ...form,
      kind: "CalendarEvent",
      title: "Concert",
      startTime: "18:30",
      endDate: "2026-11-22",
      dueDate: "2026-11-30",
      priority: "High",
      assignedAgent: "TimelinePlanner",
    });

    expect(payload).toMatchObject({
      kind: "CalendarEvent",
      date: "2026-11-21",
      startTime: "18:30:00",
      endDate: "2026-11-22",
      dueDate: null,
      priority: null,
      status: null,
      assignedAgent: null,
    });
  });

  it("round-trips an event's time without seconds", () => {
    const concert = task({ kind: "CalendarEvent", startTime: "18:30:00", endDate: "2026-11-13" });
    expect(toTaskForm(concert, { year: 2026, month: 11, date: null })).toMatchObject({
      kind: "CalendarEvent",
      startTime: "18:30",
      endDate: "2026-11-13",
    });
  });

  it("drops the time and end day of an entry pinned to a month", () => {
    const form = toTaskForm(null, { year: 2026, month: 11, date: null });
    expect(toTaskPayload({ ...form, kind: "CalendarEvent", title: "Trip", startTime: "09:00", endDate: "2026-11-30" })).toMatchObject({
      date: null,
      startTime: null,
      endDate: null,
    });
  });

  it("sends a repeat rule and a reminder", () => {
    const form = toTaskForm(null, { year: 2026, month: 11, date: "2026-11-02" });
    const payload = toTaskPayload({
      ...form,
      title: "Gym",
      repeat: "Weekly",
      repeatInterval: "2",
      repeatWeekdays: [1, 3],
      repeatUntil: "2027-03-01",
      reminder: "1440",
    });

    expect(payload.repeat).toEqual({ frequency: "Weekly", interval: 2, weekdays: [1, 3], until: "2027-03-01" });
    expect(payload.reminderOffsetMinutes).toBe(1440);
  });

  it("keeps an existing occurrence's repeat rule in the form", () => {
    const occurrence = task({
      seriesId: "series-1",
      repeat: { frequency: "Monthly", interval: 1, weekdays: null, until: null },
      reminderOffsetMinutes: 0,
    });
    expect(toTaskForm(occurrence, { year: 2026, month: 11, date: null })).toMatchObject({
      repeat: "Monthly",
      repeatInterval: "1",
      reminder: "0",
    });
  });

  it("rejects an end day before the day, and a pinned entry repeating weekly", () => {
    const form = toTaskForm(null, { year: 2026, month: 11, date: "2026-11-21" });
    expect(
      taskFormSchema.safeParse({ ...form, title: "Trip", kind: "CalendarEvent", endDate: "2026-11-20" }).success,
    ).toBe(false);

    const pinned = toTaskForm(null, { year: 2026, month: 11, date: null });
    expect(taskFormSchema.safeParse({ ...pinned, title: "Rent", repeat: "Weekly" }).success).toBe(false);
    expect(taskFormSchema.safeParse({ ...pinned, title: "Rent", repeat: "Monthly" }).success).toBe(true);
  });
});
