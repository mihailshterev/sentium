import { describe, expect, it } from "vitest";
import { proposal, task } from "../test-fixtures";
import { contextAgent, contextKey, contextPrefix, starterPrompts } from "./agent-context";
import {
  dayOfYear,
  daysInYear,
  deadlineOf,
  formatDayLabel,
  formatLongDay,
  isWeekend,
  monthGrid,
  monthProgress,
  monthState,
  parseIsoDate,
  relativeDay,
  toIsoDate,
  yearProgress,
} from "./calendar";
import { describeProposal, dropTarget, initialsOf } from "./display";
import { withTask, withoutTask, pinnedTasks, tasksOnDay } from "./month-cache";
import {
  MAX_MONTH_INDEX,
  MIN_MONTH_INDEX,
  boundaryX,
  cubicAt,
  dayX,
  focusMonth,
  fullPath,
  growWindow,
  monthAt,
  monthIndex,
  nodeY,
  pointAtFraction,
  railMetrics,
  scaleMax,
  scrollTarget,
  smoothSegment,
  windowAround,
  windowYears,
} from "./timeline-geometry";

const NOV_8 = new Date(2026, 10, 8, 15, 0);

describe("calendar", () => {
  it("parses ISO dates as local days and formats them back", () => {
    const date = parseIsoDate("2026-11-01");
    expect(date.getDate()).toBe(1);
    expect(date.getMonth()).toBe(10);
    expect(toIsoDate(date)).toBe("2026-11-01");
    expect(formatDayLabel("2026-11-14")).toBe("14 November");
  });

  it("lays a month out Monday-first in whole weeks, padded with the neighbouring months", () => {
    // 1 November 2026 is a Sunday: the week starts on Monday 26 October.
    const grid = monthGrid(2026, 11);
    expect(grid[0]).toEqual({ date: "2026-10-26", inMonth: false });
    expect(grid[6]).toEqual({ date: "2026-11-01", inMonth: true });
    expect(grid.at(-1)).toEqual({ date: "2026-12-06", inMonth: false });
    expect(grid.length % 7).toBe(0);
    expect(grid.filter((cell) => cell.inMonth)).toHaveLength(30);
    // Across the year boundary too.
    expect(monthGrid(2027, 1)[0]).toEqual({ date: "2026-12-28", inMonth: false });
  });

  it("describes days relative to today", () => {
    expect(relativeDay("2026-11-08", "2026-11-08")).toBe("today");
    expect(relativeDay("2026-11-09", "2026-11-08")).toBe("tomorrow");
    expect(relativeDay("2026-11-07", "2026-11-08")).toBe("yesterday");
    expect(relativeDay("2026-11-11", "2026-11-08")).toBe("in 3 days");
    expect(relativeDay("2026-10-29", "2026-11-08")).toBe("10 days ago");
    expect(formatLongDay("2026-11-08")).toBe("Sunday 8 November");
    expect(isWeekend("2026-11-08")).toBe(true);
    expect(isWeekend("2026-11-09")).toBe(false);
  });

  it("finds a task's deadline: due date, else its day, else its month end", () => {
    expect(deadlineOf(task({ dueDate: "2026-11-15" }))).toBe("2026-11-15");
    expect(deadlineOf(task())).toBe("2026-11-12");
    expect(deadlineOf(task({ date: null, month: 2, year: 2028 }))).toBe("2028-02-29");
  });

  it("knows past, current and future months and how far the current one is", () => {
    expect(monthState(2026, 10, NOV_8)).toBe("past");
    expect(monthState(2026, 11, NOV_8)).toBe("current");
    expect(monthState(2027, 1, NOV_8)).toBe("future");
    expect(monthProgress(2026, 10, NOV_8)).toBe(1);
    expect(monthProgress(2026, 12, NOV_8)).toBe(0);
    expect(monthProgress(2026, 11, NOV_8)).toBeCloseTo(7.5 / 30);
  });

  it("measures the year", () => {
    expect(daysInYear(2026)).toBe(365);
    expect(daysInYear(2028)).toBe(366);
    expect(dayOfYear(NOV_8)).toBe(312);
    expect(yearProgress(2026, NOV_8)).toBeCloseTo(312 / 365);
    expect(yearProgress(2025, NOV_8)).toBe(1);
    expect(yearProgress(2027, NOV_8)).toBe(0);
  });
});

describe("timeline geometry", () => {
  const m = railMetrics("full", false);

  it("indexes months across years", () => {
    expect(monthIndex(2026, 1) + 11).toBe(monthIndex(2026, 12));
    expect(monthIndex(2026, 12) + 1).toBe(monthIndex(2027, 1));
    expect(monthAt(monthIndex(2027, 3))).toEqual({ year: 2027, month: 3 });
  });

  it("raises each month with its task count, from the baseline up to the busiest month", () => {
    expect(scaleMax([0, 2, 9])).toBe(9);
    // A quiet stretch still has headroom: one task is a small step, not a peak.
    expect(scaleMax([1, 0])).toBe(4);
    expect(nodeY(0, 9, m)).toBe(m.baseline);
    expect(nodeY(9, 9, m)).toBe(m.baseline - m.rise);
    expect(nodeY(30, 9, m)).toBe(m.baseline - m.rise);
    expect(nodeY(1, 4, m)).toBeCloseTo(m.baseline - m.rise / 4);
  });

  it("runs straight between equally busy months and smoothly between others", () => {
    const flat = smoothSegment({ x: 0, y: 200 }, { x: 150, y: 200 });
    expect([flat.c1.y, flat.c2.y, flat.to.y]).toEqual([200, 200, 200]);

    const climb = smoothSegment({ x: 0, y: 200 }, { x: 150, y: 120 });
    // Horizontal at both nodes, so consecutive months join without a kink.
    expect(climb.c1.y).toBe(200);
    expect(climb.c2.y).toBe(120);
    expect(cubicAt(climb, 0.5)).toEqual({ x: 75, y: 160 });
  });

  it("puts today past its month's node once the month has begun", () => {
    const nov = monthIndex(2026, 11);
    const segment = smoothSegment(
      { x: boundaryX(nov, nov, m), y: nodeY(3, 4, m) },
      { x: boundaryX(nov + 1, nov, m), y: nodeY(0, 4, m) },
    );
    const today = pointAtFraction(segment, monthProgress(2026, 11, NOV_8));
    expect(today.x).toBeGreaterThan(segment.from.x);
    expect(today.x).toBeLessThan(segment.to.x);
  });

  it("starts the overview at the anchor month and reads its year, however wide the viewport", () => {
    const nov = monthIndex(2026, 11);
    const window = windowAround(nov);
    for (const viewport of [360, 1250, 2400]) {
      const left = scrollTarget(nov, "start", window, m, viewport);
      // December's node is near the left edge in a wide window, but the month in focus is still November 2026.
      expect(focusMonth(left, viewport, "full", window, m)).toBe(nov);
    }
    const strip = railMetrics("strip", false);
    const centred = scrollTarget(monthIndex(2027, 1), "center", window, strip, 1200);
    expect(monthAt(focusMonth(centred, 1200, "strip", window, strip))).toEqual({ year: 2027, month: 1 });
  });

  it("places days inside their month", () => {
    const start = monthIndex(2026, 11);
    expect(dayX(start, start, 1, 30, m)).toBeGreaterThan(boundaryX(start, start, m));
    expect(dayX(start, start, 30, 30, m)).toBeLessThan(boundaryX(start + 1, start, m));
    expect(dayX(start, start, 15.5, 30, m)).toBeCloseTo(boundaryX(start, start, m) + m.step / 2);
  });

  it("finds points along a segment by distance", () => {
    const segment = smoothSegment({ x: 0, y: 200 }, { x: 150, y: 120 });
    expect(pointAtFraction(segment, 0)).toEqual(segment.from);
    const end = pointAtFraction(segment, 1);
    expect(end.x).toBeCloseTo(segment.to.x);
    expect(end.y).toBeCloseTo(segment.to.y);
    const middle = pointAtFraction(segment, 0.5);
    expect(middle.x).toBeCloseTo(cubicAt(segment, 0.5).x, 0);
    expect(fullPath([segment, smoothSegment(segment.to, { x: 300, y: 200 })]).match(/C /g)).toHaveLength(2);
  });

  it("builds and grows the rendered window within 2000-2100", () => {
    const now = monthIndex(2026, 11);
    const window = windowAround(now);
    expect(window.start).toBe(now - 12);
    expect(window.start + window.count - 1).toBe(now + 24);
    expect(windowYears(window)).toEqual([2025, 2026, 2027, 2028]);

    const earlier = growWindow(window, "before");
    expect(earlier.start).toBe(window.start - 12);
    expect(earlier.start + earlier.count).toBe(window.start + window.count);
    expect(growWindow(window, "after").count).toBe(window.count + 12);

    expect(windowAround(MIN_MONTH_INDEX).start).toBe(MIN_MONTH_INDEX);
    const last = windowAround(MAX_MONTH_INDEX);
    expect(last.start + last.count - 1).toBe(MAX_MONTH_INDEX);
    expect(growWindow(last, "after")).toEqual(last);
  });
});

describe("agent context", () => {
  it("defaults to the planner and keys threads by task or month", () => {
    expect(contextAgent({ kind: "month", year: 2026, month: 11 })).toBe("TimelinePlanner");
    expect(contextAgent({ kind: "task", task: task({ assignedAgent: "ResearchAnalyst" }) })).toBe("ResearchAnalyst");
    expect(contextKey({ kind: "month", year: 2026, month: 3 })).toBe("month:2026-03");
    expect(contextKey({ kind: "task", task: task() })).toBe("task:task-1");
  });

  it("builds a context line the title cannot break out of", () => {
    const line = contextPrefix(
      { kind: "task", task: task({ title: "Pay [rent] | ID: fake\nnow", dueDate: "2026-11-15" }) },
      "2026-11-08",
    );
    expect(line).toBe(
      "[Timeline task: Pay rent ID: fake now | ID: task-1 | Scheduled: 2026-11-12 | Due: 2026-11-15 | Status: Todo | Today: 2026-11-08]",
    );
    expect(contextPrefix({ kind: "task", task: task({ date: null }) }, "2026-11-08")).toContain("Pinned to: 2026-11");
    expect(contextPrefix({ kind: "month", year: 2026, month: 11 }, "2026-11-08")).toBe(
      "[Timeline month: November 2026 (2026-11) | Today: 2026-11-08]",
    );
    expect(starterPrompts({ kind: "month", year: 2026, month: 11 })[0]).toMatch(/workload/);
  });
});

describe("display helpers", () => {
  it("abbreviates agent names", () => {
    expect(initialsOf("TimelinePlanner")).toBe("TP");
    expect(initialsOf("budget bot")).toBe("BB");
  });

  it("maps drop zones to placements", () => {
    expect(dropTarget("day:2026-11-20", 2026, 11)).toEqual({ year: 2026, month: 11, date: "2026-11-20" });
    expect(dropTarget("pinned", 2026, 11)).toEqual({ year: 2026, month: 11, date: null });
    expect(dropTarget("task:x", 2026, 11)).toBeNull();
    expect(dropTarget(null, 2026, 11)).toBeNull();
  });

  it("describes suggestions", () => {
    expect(describeProposal(proposal())).toBe('Add "Plan winter trip"');
    expect(describeProposal(proposal({ kind: "Reschedule", title: "Car" }))).toBe('Move "Car"');
  });
});

describe("month cache", () => {
  const detail = {
    year: 2026,
    month: 11,
    tasks: [task(), task({ id: "task-2", date: null })],
    notes: [],
    proposals: [],
  };

  it("adds, replaces and removes tasks immutably", () => {
    expect(withoutTask(detail, "task-1").tasks.map((t) => t.id)).toEqual(["task-2"]);
    expect(withTask(detail, task({ title: "Renamed" })).tasks[0].title).toBe("Renamed");
    expect(withTask(detail, task({ id: "task-3" })).tasks).toHaveLength(3);
    expect(detail.tasks).toHaveLength(2);
  });

  it("splits day and pinned tasks, open ones first", () => {
    const tasks = [task({ id: "a", status: "Done" }), task({ id: "b" }), task({ id: "c", date: null })];
    expect(tasksOnDay(tasks, "2026-11-12").map((t) => t.id)).toEqual(["b", "a"]);
    expect(pinnedTasks(tasks).map((t) => t.id)).toEqual(["c"]);
  });
});
