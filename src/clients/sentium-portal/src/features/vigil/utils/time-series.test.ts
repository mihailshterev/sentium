import { describe, expect, it } from "vitest";
import { contiguousRuns, nearestIndex, niceCeiling, timeTicks } from "./time-series";

describe("niceCeiling", () => {
  it.each([
    [0, 1],
    [0.7, 1],
    [1.3, 2],
    [2.2, 2.5],
    [37, 50],
    [100, 100],
    [101, 200],
    [12_400, 20_000],
  ])("rounds %d up to %d", (value, expected) => {
    expect(niceCeiling(value)).toBe(expected);
  });
});

describe("contiguousRuns", () => {
  const step = 60_000;

  it("breaks the line at a missing bucket and at an empty one", () => {
    const runs = contiguousRuns(
      [
        { at: 0, value: 1 },
        { at: step, value: 2 },
        { at: 3 * step, value: 3 },
        { at: 4 * step, value: null },
        { at: 5 * step, value: 4 },
      ],
      step,
    );

    expect(runs.map((r) => r.map((p) => p.value))).toEqual([[1, 2], [3], [4]]);
  });

  it("returns nothing for no values", () => {
    expect(contiguousRuns([{ at: 0, value: null }], step)).toEqual([]);
  });
});

describe("nearestIndex", () => {
  it("picks the closest point in time", () => {
    const points = [
      { at: 0, value: 1 },
      { at: 100, value: 1 },
      { at: 200, value: 1 },
    ];

    expect(nearestIndex(points, 140)).toBe(1);
    expect(nearestIndex(points, 999)).toBe(2);
    expect(nearestIndex([], 5)).toBe(-1);
  });
});

describe("timeTicks", () => {
  it("spreads ticks evenly including both ends", () => {
    expect(timeTicks(0, 300, 4)).toEqual([0, 100, 200, 300]);
  });
});
