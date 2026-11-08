import { useMemo } from "react";
import { motion } from "framer-motion";
import styles from "../timeline.module.css";
import type { MonthSummary } from "../types/timeline";
import { monthProgress, monthState, yearProgress, dayOfYear, daysInYear } from "../utils/calendar";
import {
  VIEW_BOX,
  fullPath,
  layoutNodes,
  pointAtFraction,
  segmentPath,
  segments,
  toPercent,
  type Point,
  type TimelineLayout,
} from "../utils/timeline-geometry";
import { MonthCallout } from "./month-callout";
import { MonthNode } from "./month-node";

interface YearTimelineProps {
  year: number;
  months: MonthSummary[] | undefined;
  today: Date;
  selectedMonth: number | null;
  /** A month is open below: shrink to a strip without callouts. */
  compact: boolean;
  layout: Exclude<TimelineLayout, "strip">;
  onSelectMonth: (month: number) => void;
}

/** Distance (view-box units) from a node to its callout. */
const LEADER = 54;

type Side = "above" | "below" | "left" | "right";

function calloutSide(index: number, layout: TimelineLayout): Side {
  if (layout === "vertical") return index % 2 === 0 ? "left" : "right";
  // Even nodes sit below the climbing trend line, odd ones above it: hang each callout on the outside.
  return index % 2 === 0 ? "below" : "above";
}

function leaderEnd(point: Point, side: Side): Point {
  switch (side) {
    case "above":
      return { x: point.x, y: point.y - LEADER };
    case "below":
      return { x: point.x, y: point.y + LEADER };
    case "left":
      return { x: point.x - LEADER * 0.6, y: point.y };
    default:
      return { x: point.x + LEADER * 0.6, y: point.y };
  }
}

/**
 * The year as a glowing path through twelve month nodes. The stretch already lived glows (to today, mid-month for the
 * current one); the rest is a faint dashed track. Paths are SVG; nodes, callouts and the today marker are HTML laid
 * over it at percentage positions, so they stay crisp, focusable and round at any aspect ratio.
 */
export function YearTimeline({
  year,
  months,
  today,
  selectedMonth,
  compact,
  layout: preferred,
  onSelectMonth,
}: YearTimelineProps) {
  const layout: TimelineLayout = compact ? "strip" : preferred;
  const view = VIEW_BOX[layout];
  const points = useMemo(() => layoutNodes(layout), [layout]);
  const segs = useMemo(() => segments(points, layout), [points, layout]);

  const states = Array.from({ length: 12 }, (_, i) => monthState(year, i + 1, today));
  const fractions = Array.from({ length: 12 }, (_, i) => monthProgress(year, i + 1, today));
  const currentIndex = states.indexOf("current");
  const todayPoint = currentIndex >= 0 ? pointAtFraction(segs[currentIndex], fractions[currentIndex]) : null;

  const progress = yearProgress(year, today);
  const summaryFor = (month: number) => months?.find((m) => m.month === month);
  const now = today.getTime();

  return (
    <motion.figure
      layout
      className={styles.timeline}
      data-layout={layout}
      data-compact={compact || undefined}
      aria-label={`Year timeline ${year}`}
    >
      <p className={styles.readout} aria-label={`${Math.round(progress * 100)} percent of ${year} has passed`}>
        <span className={styles.readoutYear}>{year}</span>
        <span>
          {currentIndex >= 0 ? `day ${dayOfYear(today)} / ${daysInYear(year)}` : progress === 1 ? "complete" : "ahead"}
        </span>
        <span className={styles.readoutBar} aria-hidden="true">
          <span style={{ width: `${progress * 100}%` }} />
        </span>
        <span>{(progress * 100).toFixed(1)}%</span>
      </p>

      <div className={styles.stage} style={{ aspectRatio: `${view.width} / ${view.height}` }}>
        <svg className={styles.paths} viewBox={`0 0 ${view.width} ${view.height}`} aria-hidden="true">
          <path className={styles.track} d={fullPath(segs)} />
          {segs.slice(0, 12).map(
            (segment, i) =>
              fractions[i] > 0 && (
                <g key={i} data-state={states[i]}>
                  <path
                    className={styles.elapsedHalo}
                    d={segmentPath(segment)}
                    pathLength={1}
                    strokeDasharray="1 1"
                    strokeDashoffset={1 - fractions[i]}
                  />
                  <path
                    className={styles.elapsed}
                    d={segmentPath(segment)}
                    pathLength={1}
                    strokeDasharray="1 1"
                    strokeDashoffset={1 - fractions[i]}
                  />
                </g>
              ),
          )}
          {!compact &&
            points.slice(0, 12).map((point, i) => {
              const end = leaderEnd(point, calloutSide(i, layout));
              return <line key={i} className={styles.leader} x1={point.x} y1={point.y} x2={end.x} y2={end.y} />;
            })}
        </svg>

        <div className={styles.overlay}>
          {points.slice(0, 12).map((point, i) => (
            <MonthNode
              key={i}
              year={year}
              month={i + 1}
              summary={summaryFor(i + 1)}
              state={states[i]}
              selected={selectedMonth === i + 1}
              position={toPercent(point, view)}
              onSelect={onSelectMonth}
            />
          ))}

          {!compact &&
            points.slice(0, 12).map((point, i) => {
              const side = calloutSide(i, layout);
              return (
                <MonthCallout
                  key={i}
                  month={i + 1}
                  summary={summaryFor(i + 1)}
                  side={side}
                  position={toPercent(leaderEnd(point, side), view)}
                  now={now}
                  onSelect={onSelectMonth}
                />
              );
            })}

          {todayPoint && (
            <span className={styles.todayMarker} style={toPercent(todayPoint, view)} aria-hidden="true">
              <span className={styles.todayLabel}>today</span>
            </span>
          )}

          <span className={styles.yearEnd} style={toPercent(points[12], view)} aria-hidden="true">
            {year + 1}
          </span>
        </div>
      </div>
    </motion.figure>
  );
}
