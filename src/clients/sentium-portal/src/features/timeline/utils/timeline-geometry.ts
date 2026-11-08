/**
 * Geometry of the year path: 12 month nodes plus a year-end anchor joined by one cubic Bézier per month. Pure
 * functions (no DOM measuring), so jsdom tests cover them and SSR-free rendering stays deterministic.
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

/** `strip`: the flat band the year collapses to while a month is open. */
export type TimelineLayout = "horizontal" | "vertical" | "strip";

export interface ViewBox {
  width: number;
  height: number;
}

export const VIEW_BOX: Record<TimelineLayout, ViewBox> = {
  horizontal: { width: 1200, height: 520 },
  vertical: { width: 400, height: 1320 },
  strip: { width: 1200, height: 150 },
};

const NODE_COUNT = 13;

/**
 * Horizontal: a staggered zigzag that climbs from bottom-left to top-right. Vertical (narrow screens): a serpentine
 * down the page.
 */
export function layoutNodes(layout: TimelineLayout): Point[] {
  if (layout === "vertical") {
    const { height } = VIEW_BOX.vertical;
    const top = 70;
    const step = (height - top * 2) / (NODE_COUNT - 1);
    return Array.from({ length: NODE_COUNT }, (_, i) => ({ x: i % 2 === 0 ? 150 : 250, y: top + i * step }));
  }

  if (layout === "strip") {
    const { width } = VIEW_BOX.strip;
    const left = 48;
    const step = (width - left * 2) / (NODE_COUNT - 1);
    return Array.from({ length: NODE_COUNT }, (_, i) => ({ x: left + i * step, y: 70 + (i % 2 === 0 ? 18 : -18) }));
  }

  const { width } = VIEW_BOX.horizontal;
  const left = 64;
  const step = (width - left * 2) / (NODE_COUNT - 1);
  // Room for a callout above the highest node and below the lowest one.
  const low = 350;
  const high = 180;
  const stagger = 40;
  return Array.from({ length: NODE_COUNT }, (_, i) => {
    const climb = low - ((low - high) * i) / (NODE_COUNT - 1);
    return { x: left + i * step, y: climb + (i % 2 === 0 ? stagger : -stagger) };
  });
}

/** One segment per month: tangents follow the main axis so consecutive curves join smoothly. */
export function segments(points: Point[], layout: TimelineLayout): Segment[] {
  return points.slice(0, -1).map((from, i) => {
    const to = points[i + 1];
    if (layout === "vertical") {
      const dy = (to.y - from.y) / 2;
      return { from, c1: { x: from.x, y: from.y + dy }, c2: { x: to.x, y: to.y - dy }, to };
    }
    const dx = (to.x - from.x) / 2;
    return { from, c1: { x: from.x + dx, y: from.y }, c2: { x: to.x - dx, y: to.y }, to };
  });
}

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

/** A view-box point as CSS percentages, for HTML overlays positioned over the SVG. */
export const toPercent = (p: Point, view: ViewBox) => ({
  left: `${(p.x / view.width) * 100}%`,
  top: `${(p.y / view.height) * 100}%`,
});
