/** A bucket on a time axis; `value` is null when the bucket exists but has nothing to plot. */
export interface ChartPoint {
  at: number;
  value: number | null;
}

const NICE_STEPS = [1, 2, 2.5, 5, 10];

// A gap wider than this many buckets breaks the line instead of drawing across missing data.
const GAP_TOLERANCE = 1.5;

/** The smallest 1/2/2.5/5 x 10^n at or above `value`, so the top gridline lands on a round number. */
export function niceCeiling(value: number): number {
  if (!(value > 0)) {
    return 1;
  }

  const base = 10 ** Math.floor(Math.log10(value));
  const step = NICE_STEPS.find((s) => value / base <= s + 1e-9) ?? 10;

  return step * base;
}

/** Splits points into runs of consecutive buckets with a value, so missing or empty buckets leave a gap. */
export function contiguousRuns(points: readonly ChartPoint[], stepMs: number): ChartPoint[][] {
  const runs: ChartPoint[][] = [];
  let current: ChartPoint[] = [];

  for (const point of points) {
    const previous = current.at(-1);
    const isGap = point.value === null || (previous !== undefined && point.at - previous.at > stepMs * GAP_TOLERANCE);

    if (isGap && current.length > 0) {
      runs.push(current);
      current = [];
    }

    if (point.value !== null) {
      current.push(point);
    }
  }

  if (current.length > 0) {
    runs.push(current);
  }

  return runs;
}

/** Index of the point closest in time to `at`; -1 for no points. Points must be in time order. */
export function nearestIndex(points: readonly ChartPoint[], at: number): number {
  let best = -1;
  let bestDistance = Number.POSITIVE_INFINITY;

  points.forEach((point, index) => {
    const distance = Math.abs(point.at - at);
    if (distance < bestDistance) {
      best = index;
      bestDistance = distance;
    }
  });

  return best;
}

/** `count` evenly spaced times from `from` to `to`, both ends included. */
export function timeTicks(from: number, to: number, count: number): number[] {
  if (count < 2 || to <= from) {
    return [from];
  }

  return Array.from({ length: count }, (_, i) => from + ((to - from) * i) / (count - 1));
}
