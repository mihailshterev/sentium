import { useId, useState } from "react";
import styles from "./inference.module.css";
import { useElementWidth } from "@/features/vigil/hooks/useElementWidth";
import { LOCALE } from "@/features/vigil/utils/inference-format";
import { contiguousRuns, nearestIndex, niceCeiling, timeTicks, type ChartPoint } from "@/features/vigil/utils/time-series";

const HEIGHT = 132;
const MARGIN = { top: 10, right: 12, bottom: 22, left: 44 };
const FALLBACK_WIDTH = 560;
const Y_TICKS = 3;
const MAX_X_TICKS = 4;
const MIN_X_TICK_SPACING = 90;
const DOT_RADIUS = 4;
// Below this width there is no room beside the crosshair, so the tooltip docks under the plot.
const NARROW_WIDTH = 360;
const DAY_MS = 24 * 60 * 60 * 1000;

export interface TooltipRow {
  label: string;
  value: string;
}

interface TimeSeriesChartProps {
  /** Shown above the plot and used as its accessible name. */
  title: string;
  points: readonly ChartPoint[];
  from: number;
  to: number;
  stepMs: number;
  formatValue: (value: number) => string;
  /** Fixed top of the y axis (e.g. 100 for percentages); otherwise rounded up from the data. */
  max?: number;
  /** Extra tooltip rows for the bucket at `index`. */
  describe?: (index: number) => TooltipRow[];
}

/**
 * A single-series time chart: 2px line over a 10% wash, recessive hairline grid, and a crosshair that snaps to the
 * nearest bucket on hover or with the arrow keys. One series, so no legend: the title names it.
 */
export function TimeSeriesChart({ title, points, from, to, stepMs, formatValue, max, describe }: TimeSeriesChartProps) {
  const { ref, width } = useElementWidth<HTMLDivElement>(FALLBACK_WIDTH);
  const [active, setActive] = useState<number | null>(null);
  const titleId = useId();

  const plotWidth = Math.max(1, width - MARGIN.left - MARGIN.right);
  const plotHeight = HEIGHT - MARGIN.top - MARGIN.bottom;
  const values = points.flatMap((p) => (p.value === null ? [] : [p.value]));
  const top = max ?? niceCeiling(Math.max(0, ...values));

  const x = (at: number) => MARGIN.left + (to > from ? ((at - from) / (to - from)) * plotWidth : 0);
  const y = (value: number) => MARGIN.top + plotHeight - (Math.min(value, top) / top) * plotHeight;
  const baseline = y(0);

  const runs = contiguousRuns(points, stepMs);
  const yTicks = Array.from({ length: Y_TICKS }, (_, i) => (top * i) / (Y_TICKS - 1));
  const xTicks = timeTicks(from, to, Math.max(2, Math.min(MAX_X_TICKS, Math.floor(plotWidth / MIN_X_TICK_SPACING) + 1)));
  const formatTick = tickFormatter(to - from);

  const activePoint = active === null ? undefined : points[active];
  const tooltipSide = !activePoint ? undefined : width < NARROW_WIDTH ? "below" : x(activePoint.at) > width / 2 ? "left" : "right";

  const onPointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    const bounds = event.currentTarget.getBoundingClientRect();
    const ratio = (event.clientX - bounds.left - MARGIN.left) / plotWidth;
    const index = nearestIndex(points, from + Math.min(1, Math.max(0, ratio)) * (to - from));
    setActive(index < 0 ? null : index);
  };

  const onKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (points.length === 0) {
      return;
    }

    const last = points.length - 1;
    const next =
      event.key === "ArrowRight"
        ? Math.min(last, (active ?? -1) + 1)
        : event.key === "ArrowLeft"
          ? Math.max(0, (active ?? points.length) - 1)
          : event.key === "Home"
            ? 0
            : event.key === "End"
              ? last
              : undefined;

    if (event.key === "Escape") {
      setActive(null);
      return;
    }

    if (next !== undefined) {
      event.preventDefault();
      setActive(next);
    }
  };

  return (
    <figure className={styles.chart}>
      <figcaption id={titleId} className={styles.chartTitle}>
        {title}
      </figcaption>

      <div
        ref={ref}
        className={styles.plot}
        tabIndex={0}
        role="group"
        aria-labelledby={titleId}
        aria-describedby={`${titleId}-hint`}
        onPointerMove={onPointerMove}
        onPointerLeave={() => setActive(null)}
        onBlur={() => setActive(null)}
        onKeyDown={onKeyDown}
      >
        <span id={`${titleId}-hint`} className="visually-hidden">
          Arrow keys step through the buckets; the table view lists every value.
        </span>

        <svg width={width} height={HEIGHT} aria-hidden="true" className={styles.svg}>
          {yTicks.map((tick) => (
            <g key={tick}>
              <line x1={MARGIN.left} x2={MARGIN.left + plotWidth} y1={y(tick)} y2={y(tick)} className={styles.grid} />
              <text x={MARGIN.left - 8} y={y(tick)} className={styles.axisY}>
                {formatValue(tick)}
              </text>
            </g>
          ))}

          {xTicks.map((tick, i) => (
            <text
              key={tick}
              x={x(tick)}
              y={HEIGHT - 6}
              className={styles.axisX}
              data-edge={i === 0 ? "start" : i === xTicks.length - 1 ? "end" : undefined}
            >
              {formatTick(tick)}
            </text>
          ))}

          {runs.map((run) => (
            <Run key={run[0].at} run={run} x={x} y={y} baseline={baseline} />
          ))}

          {activePoint && (
            <g>
              <line x1={x(activePoint.at)} x2={x(activePoint.at)} y1={MARGIN.top} y2={baseline} className={styles.crosshair} />
              {activePoint.value !== null && (
                <circle cx={x(activePoint.at)} cy={y(activePoint.value)} r={DOT_RADIUS} className={styles.dot} />
              )}
            </g>
          )}
        </svg>

        <div
          className={styles.tooltip}
          aria-live="polite"
          hidden={!activePoint}
          data-side={tooltipSide}
          style={activePoint && tooltipSide !== "below" ? { left: x(activePoint.at) } : undefined}
        >
          {activePoint && active !== null && (
            <>
              <strong className={styles.tooltipValue}>
                {activePoint.value === null ? "no work" : formatValue(activePoint.value)}
              </strong>
              <span className={styles.tooltipTime}>{formatTooltipTime(activePoint.at)}</span>
              {describe?.(active).map((row) => (
                <span key={row.label} className={styles.tooltipRow}>
                  <span>{row.value}</span> {row.label}
                </span>
              ))}
            </>
          )}
        </div>
      </div>
    </figure>
  );
}

function Run({
  run,
  x,
  y,
  baseline,
}: {
  run: ChartPoint[];
  x: (at: number) => number;
  y: (value: number) => number;
  baseline: number;
}) {
  if (run.length === 1) {
    const only = run[0];
    return <circle cx={x(only.at)} cy={y(only.value ?? 0)} r={DOT_RADIUS} className={styles.dot} />;
  }

  const line = run.map((p, i) => `${i === 0 ? "M" : "L"}${x(p.at)},${y(p.value ?? 0)}`).join(" ");
  const area = `${line} L${x(run[run.length - 1].at)},${baseline} L${x(run[0].at)},${baseline} Z`;

  return (
    <g>
      <path d={area} className={styles.area} />
      <path d={line} className={styles.line} />
    </g>
  );
}

function tickFormatter(spanMs: number): (at: number) => string {
  const format =
    spanMs <= DAY_MS
      ? new Intl.DateTimeFormat(LOCALE, { hour: "2-digit", minute: "2-digit" })
      : new Intl.DateTimeFormat(LOCALE, { day: "numeric", month: "short" });

  return (at) => format.format(at);
}

const tooltipTimeFormat = new Intl.DateTimeFormat(LOCALE, {
  day: "numeric",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

function formatTooltipTime(at: number): string {
  return tooltipTimeFormat.format(at);
}
