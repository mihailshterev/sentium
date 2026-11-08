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
      title: "Book dentist",
      description: "Call first",
      year: 2026,
      month: 11,
      date: "2026-11-12",
      dueDate: "2026-11-20",
      priority: "Normal",
      status: "Todo",
      tags: ["health"],
      assignedAgent: "TimelinePlanner",
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
});
