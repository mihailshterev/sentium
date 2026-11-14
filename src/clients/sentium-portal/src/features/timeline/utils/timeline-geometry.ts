/**
 * Geometry of the timeline rail: a line chart of workload, laid out left to right in CSS pixels so the rail can
 * scroll across years. Months sit on a straight baseline and rise with how many tasks and events they hold; each node marks
 * where its month starts and the month runs along the line to the next node, so on the 8th the lived path has just
 * passed its month's node. Day ticks run in a lane under the baseline, month labels under that. Pure functions (no
 * DOM measuring), so jsdom tests cover them.
 */

export interface Point {
  x: number;
  y: number;
}

export interface Segment {
  from: Point;
  c1: Point;
  c2: Point;
  to: Point;
}

/** `full`: the overview with month cards. `strip`: the low band the rail collapses to while a month is open. */
export type RailMode = "full" | "strip";

export interface RailMetrics {
  /** Width of one month. */
  step: number;
  height: number;
  /** y of the straight line an empty month sits on. */
  baseline: number;
  /** How far the busiest month rises above the baseline. */
  rise: number;
  /** The day ticks' lane, this far below the baseline. */
  dayLane: number;
  /** The month labels, this far below the baseline. */
  labelGap: number;
  /** Space before the first month and after the last one. */
  pad: number;
}

const METRICS: Record<RailMode, { wide: RailMetrics; narrow: RailMetrics }> = {
  full: {
    wide: { step: 150, height: 300, baseline: 236, rise: 116, dayLane: 24, labelGap: 40, pad: 24 },
    narrow: { step: 116, height: 300, baseline: 236, rise: 116, dayLane: 24, labelGap: 40, pad: 16 },
  },
  strip: {
    wide: { step: 88, height: 92, baseline: 46, rise: 24, dayLane: 18, labelGap: 30, pad: 16 },
    narrow: { step: 60, height: 92, baseline: 46, rise: 24, dayLane: 18, labelGap: 30, pad: 12 },
  },
};

export const railMetrics = (mode: RailMode, narrow: boolean): RailMetrics => METRICS[mode][narrow ? "narrow" : "wide"];

/** Months since year 0, so months across years compare and step as integers. */
export const monthIndex = (year: number, month: number) => year * 12 + month - 1;

export const monthAt = (index: number) => ({ year: Math.floor(index / 12), month: (index % 12) + 1 });

export const MIN_MONTH_INDEX = monthIndex(2000, 1);
export const MAX_MONTH_INDEX = monthIndex(2100, 12);

export const railWidth = (count: number, m: RailMetrics) => m.pad * 2 + count * m.step;

/** The x where month `index` starts (its node), on a rail whose first month is `start`. */
export const boundaryX = (index: number, start: number, m: RailMetrics) => m.pad + (index - start) * m.step;

/** The chart's top is at least this load, so a single item is a small step rather than a peak. */
export const SCALE_FLOOR = 4;

/** The load the full rise stands for: the busiest month given, at least {@link SCALE_FLOOR}. */
export const scaleMax = (counts: number[]) => counts.reduce((max, count) => Math.max(max, count), SCALE_FLOOR);

/** The y of a month's node: on the baseline when empty, up to the full rise for the busiest month. */
export const nodeY = (count: number, max: number, m: RailMetrics) =>
  m.baseline - m.rise * Math.min(1, Math.max(0, count) / Math.max(1, max));

/**
 * The line from one node to the next. Both ends leave horizontally, so the line stays smooth through every node and
 * is perfectly straight between months that hold the same number of tasks.
 */
export function smoothSegment(from: Point, to: Point): Segment {
  const reach = (to.x - from.x) / 2;
  return { from, c1: { x: from.x + reach, y: from.y }, c2: { x: to.x - reach, y: to.y }, to };
}

/** Where day `day` (1-based) of a `days`-long month sits on the axis. */
export const dayX = (index: number, start: number, day: number, days: number, m: RailMetrics) =>
  boundaryX(index, start, m) + ((day - 0.5) / days) * m.step;

const fmt = (value: number) => Number(value.toFixed(2));

export const segmentPath = (s: Segment) =>
  `M ${fmt(s.from.x)} ${fmt(s.from.y)} C ${fmt(s.c1.x)} ${fmt(s.c1.y)}, ${fmt(s.c2.x)} ${fmt(s.c2.y)}, ${fmt(s.to.x)} ${fmt(s.to.y)}`;

export function fullPath(segs: Segment[]): string {
  if (segs.length === 0) return "";
  const [first] = segs;
  return [
    `M ${fmt(first.from.x)} ${fmt(first.from.y)}`,
    ...segs.map((s) => `C ${fmt(s.c1.x)} ${fmt(s.c1.y)}, ${fmt(s.c2.x)} ${fmt(s.c2.y)}, ${fmt(s.to.x)} ${fmt(s.to.y)}`),
  ].join(" ");
}

export function cubicAt(s: Segment, t: number): Point {
  const u = 1 - t;
  const a = u * u * u;
  const b = 3 * u * u * t;
  const c = 3 * u * t * t;
  const d = t * t * t;
  return {
    x: a * s.from.x + b * s.c1.x + c * s.c2.x + d * s.to.x,
    y: a * s.from.y + b * s.c1.y + c * s.c2.y + d * s.to.y,
  };
}

const SAMPLES = 24;

/**
 * The point `fraction` of the way along the segment by arc length (a Bézier's `t` is not proportional to distance).
 */
export function pointAtFraction(s: Segment, fraction: number): Point {
  const f = Math.min(1, Math.max(0, fraction));
  const lengths = [0];
  let previous = s.from;
  for (let i = 1; i <= SAMPLES; i++) {
    const point = cubicAt(s, i / SAMPLES);
    lengths.push(lengths[i - 1] + Math.hypot(point.x - previous.x, point.y - previous.y));
    previous = point;
  }

  const target = f * lengths[SAMPLES];
  let i = 1;
  while (i < SAMPLES && lengths[i] < target) i++;
  const span = lengths[i] - lengths[i - 1];
  const local = span === 0 ? 0 : (target - lengths[i - 1]) / span;
  return cubicAt(s, (i - 1 + local) / SAMPLES);
}

/** Months the rail renders: a window that grows as the viewer scrolls toward either end. */
export interface RailWindow {
  start: number;
  count: number;
}

/** Months kept before / after the anchor when a window is (re)built around it. */
export const WINDOW_BEFORE = 12;
export const WINDOW_AFTER = 24;
/** Months added when the viewer nears an end. */
export const WINDOW_GROWTH = 12;

export function windowAround(anchor: number): RailWindow {
  const start = Math.max(MIN_MONTH_INDEX, anchor - WINDOW_BEFORE);
  const end = Math.min(MAX_MONTH_INDEX, anchor + WINDOW_AFTER);
  return { start, count: end - start + 1 };
}

export function growWindow(w: RailWindow, side: "before" | "after"): RailWindow {
  if (side === "before") {
    const start = Math.max(MIN_MONTH_INDEX, w.start - WINDOW_GROWTH);
    return { start, count: w.count + (w.start - start) };
  }
  const end = Math.min(MAX_MONTH_INDEX, w.start + w.count - 1 + WINDOW_GROWTH);
  return { start: w.start, count: end - w.start + 1 };
}

/** Distinct years the window touches, in order. */
export function windowYears(w: RailWindow): number[] {
  const first = monthAt(w.start).year;
  const last = monthAt(w.start + w.count - 1).year;
  return Array.from({ length: last - first + 1 }, (_, i) => first + i);
}

/**
 * The overview's anchor node sits this much less than a month in from the left edge: the lived path runs in from the
 * month before (whose node is just out of view), inviting a scroll back.
 */
const PEEK = 30;

/**
 * The scrollLeft that shows month `index`: at the left edge after a peek of the month before ("start", the overview)
 * or centred ("center", the strip).
 */
export function scrollTarget(
  index: number,
  align: "start" | "center",
  w: RailWindow,
  m: RailMetrics,
  viewport: number,
) {
  const x = boundaryX(index, w.start, m);
  return Math.max(0, align === "center" ? x + m.step / 2 - viewport / 2 : x - m.step + PEEK);
}

/**
 * The month in focus at a scroll position: the first whole month after the peek on the overview (however wide the
 * viewport), the centred one on the strip. Its year heads the readout.
 */
export function focusMonth(scrollLeft: number, viewport: number, mode: RailMode, w: RailWindow, m: RailMetrics) {
  const x = mode === "strip" ? scrollLeft + viewport / 2 : scrollLeft + m.step - PEEK + m.step / 2;
  const index = w.start + Math.floor((x - m.pad) / m.step);
  return Math.min(MAX_MONTH_INDEX, Math.max(MIN_MONTH_INDEX, index));
}
