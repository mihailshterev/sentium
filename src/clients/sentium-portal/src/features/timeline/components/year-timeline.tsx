import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useReducedMotion } from "framer-motion";
import { ChevronLeft, ChevronRight } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import { useDragPan } from "../hooks/useDragPan";
import type { DaySummary, MonthSummary, YearOverview } from "../types/timeline";
import {
  MONTH_SHORT,
  dayOfYear,
  daysInMonth,
  daysInYear,
  monthProgress,
  monthState,
  yearProgress,
} from "../utils/calendar";
import {
  MAX_MONTH_INDEX,
  MIN_MONTH_INDEX,
  boundaryX,
  dayX,
  focusMonth,
  fullPath,
  growWindow,
  monthAt,
  monthIndex,
  nodeY,
  pointAtFraction,
  railMetrics,
  railWidth,
  scaleMax,
  scrollTarget,
  segmentPath,
  smoothSegment,
  windowAround,
  windowYears,
} from "../utils/timeline-geometry";
import { MonthCallout } from "./month-callout";
import { MonthNode } from "./month-node";

interface YearTimelineProps {
  overviews: Map<number, YearOverview>;
  today: Date;
  selected: { year: number; month: number } | null;
  home: number;
  overdueCount: number;
  narrow: boolean;
  onSelectMonth: (year: number, month: number) => void;
  onYearsChange: (years: number[]) => void;
}

const CARD_TOP = 8;
const NODE_RADIUS = 20;
const PAGE_MONTHS = 6;
const GROW_MARGIN_MONTHS = 4;

const isBusy = (s: MonthSummary | undefined) =>
  !!s && (s.taskCount > 0 || s.eventCount > 0 || s.noteCount > 0 || s.pendingProposalCount > 0);

const monthLoad = (s: MonthSummary | undefined) => (s ? s.taskCount + s.eventCount : 0);

const dotRadius = (day: DaySummary) => {
  const count = day.open + day.done;
  return count >= 3 ? 4.5 : count === 2 ? 3.5 : 2.5;
};

/** Task days by their tasks; a day with only Calendar Events gets the hollow event tone. */
const dotTone = (day: DaySummary) =>
  day.urgent ? "urgent" : day.open > 0 ? "open" : day.done > 0 ? "done" : day.events > 0 ? "event" : "done";

function scrollRail(el: HTMLElement, left: number, smooth: boolean) {
  if (typeof el.scrollTo === "function") {
    el.scrollTo({ left, behavior: smooth ? "smooth" : "auto" });
  } else {
    el.scrollLeft = left;
  }
}

/**
 * The timeline as a glowing line chart of workload you scroll along across years: months sit on a straight baseline
 * and rise with their tasks and events, each node where its month starts; day ticks run in a lane below, month labels
 * under that, and months with something in them get a card in the lane on top. The stretch already lived glows (to
 * today for the current month); the rest is a faint dashed track. The rendered window of months grows as the viewer
 * nears either end. Paths are SVG; nodes, cards and labels are HTML laid over it, so they stay crisp and focusable.
 */
export function YearTimeline({
  overviews,
  today,
  selected,
  home,
  overdueCount,
  narrow,
  onSelectMonth,
  onYearsChange,
}: YearTimelineProps) {
  const mode = selected ? "strip" : "full";
  const m = railMetrics(mode, narrow);
  const anchor = selected ? monthIndex(selected.year, selected.month) : home;
  const align = selected ? "center" : "start";

  const scrollerRef = useRef<HTMLDivElement>(null);
  const panning = useDragPan(scrollerRef);
  const reducedMotion = useReducedMotion();
  const [win, setWin] = useState(() => windowAround(anchor));
  const [viewYear, setViewYear] = useState(() => monthAt(anchor).year);

  if (anchor < win.start || anchor >= win.start + win.count) {
    setWin(windowAround(anchor));
  }

  const pendingShift = useRef(0);
  const growing = useRef(false);

  useLayoutEffect(() => {
    const el = scrollerRef.current;
    if (el && pendingShift.current !== 0) {
      el.scrollLeft += pendingShift.current;
    }
    pendingShift.current = 0;
    growing.current = false;
  }, [win]);

  const lastScroll = useRef<string | null>(null);
  useLayoutEffect(() => {
    const el = scrollerRef.current;
    if (!el) {
      return;
    }

    const key = `${anchor}:${align}:${mode}:${narrow}`;
    const previous = lastScroll.current;
    if (previous === key) {
      return;
    }

    lastScroll.current = key;
    const sameGeometry = previous?.endsWith(`:${mode}:${narrow}`) ?? false;
    scrollRail(el, scrollTarget(anchor, align, win, m, el.clientWidth), sameGeometry && !reducedMotion);
    setViewYear(monthAt(anchor).year);
  }, [anchor, align, mode, narrow, win, m, reducedMotion]);

  const yearsKey = windowYears(win).join(",");

  useEffect(() => {
    onYearsChange(yearsKey.split(",").map(Number));
  }, [yearsKey, onYearsChange]);

  const onScroll = () => {
    const el = scrollerRef.current;
    if (!el) {
      return;
    }

    const margin = m.step * GROW_MARGIN_MONTHS;
    if (!growing.current && el.scrollLeft < margin && win.start > MIN_MONTH_INDEX) {
      const next = growWindow(win, "before");
      growing.current = true;
      pendingShift.current = (win.start - next.start) * m.step;
      setWin(next);
    } else if (
      !growing.current &&
      el.scrollLeft + el.clientWidth > el.scrollWidth - margin &&
      win.start + win.count - 1 < MAX_MONTH_INDEX
    ) {
      growing.current = true;
      setWin(growWindow(win, "after"));
    }
    setViewYear(monthAt(focusMonth(el.scrollLeft, el.clientWidth, mode, win, m)).year);
  };

  const page = (direction: -1 | 1) => {
    const el = scrollerRef.current;
    if (!el) {
      return;
    }
    scrollRail(el, Math.max(0, el.scrollLeft + direction * PAGE_MONTHS * m.step), !reducedMotion);
  };

  const months = useMemo(
    () =>
      Array.from({ length: win.count }, (_, i) => {
        const index = win.start + i;
        const { year, month } = monthAt(index);
        return {
          index,
          year,
          month,
          state: monthState(year, month, today),
          fraction: monthProgress(year, month, today),
          summary: overviews.get(year)?.months[month - 1],
        };
      }),
    [win, today, overviews],
  );

  const nodes = useMemo(() => {
    const loadAt = (index: number) => {
      const { year, month } = monthAt(index);
      return monthLoad(overviews.get(year)?.months[month - 1]);
    };
    const max = scaleMax(months.map((month) => monthLoad(month.summary)));
    return Array.from({ length: win.count + 1 }, (_, i) => ({
      x: boundaryX(win.start + i, win.start, m),
      y: nodeY(loadAt(win.start + i), max, m),
    }));
  }, [months, overviews, win, m]);

  const segments = useMemo(() => nodes.slice(0, -1).map((node, i) => smoothSegment(node, nodes[i + 1])), [nodes]);

  const dayDots = useMemo(() => {
    const first = win.start;
    const last = win.start + win.count - 1;
    return [...overviews.values()].flatMap((overview) =>
      overview.days.flatMap((day) => {
        const [year, month, date] = day.date.split("-").map(Number);
        const index = monthIndex(year, month);
        if (index < first || index > last) return [];
        return [{ day, x: dayX(index, first, date, daysInMonth(year, month), m) }];
      }),
    );
  }, [overviews, win, m]);

  const currentAt = months.findIndex((month) => month.state === "current");
  const todayPoint = currentAt >= 0 ? pointAtFraction(segments[currentAt], months[currentAt].fraction) : null;
  const todayX =
    currentAt >= 0
      ? dayX(
          months[currentAt].index,
          win.start,
          today.getDate(),
          daysInMonth(today.getFullYear(), today.getMonth() + 1),
          m,
        )
      : null;
  const width = railWidth(win.count, m);
  const line = fullPath(segments);
  const lane = m.baseline + m.dayLane;
  const now = today.getTime();

  const progress = yearProgress(viewYear, today);
  const isTodayYear = viewYear === today.getFullYear();

  return (
    <figure
      className={styles.timeline}
      data-layout={mode}
      data-compact={mode === "strip" || undefined}
      aria-label="Timeline"
    >
      <div className={styles.railHead}>
        <p className={styles.readout} aria-label={`${Math.round(progress * 100)} percent of ${viewYear} has passed`}>
          <span className={styles.readoutYear}>{viewYear}</span>
          <span>
            {isTodayYear ? `day ${dayOfYear(today)} / ${daysInYear(viewYear)}` : progress === 1 ? "complete" : "ahead"}
          </span>
          <span className={styles.readoutBar} aria-hidden="true">
            <span style={{ width: `${progress * 100}%` }} />
          </span>
          <span className={styles.readoutPercent}>{(progress * 100).toFixed(1)}%</span>
          {overdueCount > 0 && <span className={styles.readoutOverdue}>{overdueCount} overdue</span>}
        </p>
        <div className={styles.railNav}>
          <Button
            size="sm"
            variant="ghost"
            iconOnly
            icon={<ChevronLeft size={16} />}
            aria-label="Earlier months"
            onClick={() => page(-1)}
          />
          <Button
            size="sm"
            variant="ghost"
            iconOnly
            icon={<ChevronRight size={16} />}
            aria-label="Later months"
            onClick={() => page(1)}
          />
        </div>
      </div>

      <div
        ref={scrollerRef}
        className={styles.scroller}
        onScroll={onScroll}
        data-panning={panning || undefined}
        data-testid="timeline-rail"
      >
        <div
          className={styles.stage}
          style={{ width, height: m.height, "--step": `${m.step}px` } as React.CSSProperties}
        >
          <svg
            className={styles.paths}
            width={width}
            height={m.height}
            viewBox={`0 0 ${width} ${m.height}`}
            aria-hidden="true"
          >
            {segments.length > 0 && (
              <path
                className={styles.area}
                d={`${line} L ${nodes[nodes.length - 1].x} ${m.baseline} L ${nodes[0].x} ${m.baseline} Z`}
              />
            )}
            <line className={styles.axis} x1={m.pad} y1={m.baseline} x2={width - m.pad} y2={m.baseline} />
            {months.map(({ index, month }, i) =>
              month === 1 ? (
                <line
                  key={index}
                  className={styles.yearLine}
                  x1={nodes[i].x}
                  y1={m.baseline - m.rise - 12}
                  x2={nodes[i].x}
                  y2={lane + 8}
                />
              ) : (
                <line
                  key={index}
                  className={styles.boundary}
                  x1={nodes[i].x}
                  y1={m.baseline}
                  x2={nodes[i].x}
                  y2={m.baseline + 4}
                />
              ),
            )}
            <path className={styles.track} d={line} />
            {months.map(
              ({ index, state, fraction }, i) =>
                fraction > 0 && (
                  <g key={index} data-state={state}>
                    <path
                      className={styles.elapsedHalo}
                      d={segmentPath(segments[i])}
                      pathLength={1}
                      strokeDasharray="1 1"
                      strokeDashoffset={1 - fraction}
                    />
                    <path
                      className={styles.elapsed}
                      d={segmentPath(segments[i])}
                      pathLength={1}
                      strokeDasharray="1 1"
                      strokeDashoffset={1 - fraction}
                    />
                  </g>
                ),
            )}
            {todayX !== null && (
              <line className={styles.todayTick} x1={todayX} y1={lane - 7} x2={todayX} y2={lane + 7} />
            )}
            {dayDots.map(({ day, x }) => (
              <circle
                key={day.date}
                className={styles.dayDot}
                data-tone={dotTone(day)}
                cx={x}
                cy={lane}
                r={dotRadius(day)}
              />
            ))}
            {mode === "full" &&
              months.map(({ index, summary }, i) =>
                isBusy(summary) ? (
                  <line
                    key={index}
                    className={styles.leader}
                    x1={nodes[i].x}
                    y1={CARD_TOP + 24}
                    x2={nodes[i].x}
                    y2={nodes[i].y - NODE_RADIUS}
                  />
                ) : null,
              )}
          </svg>

          <div className={styles.overlay}>
            {months.map(({ index, year, month, state }, i) => (
              <span
                key={index}
                className={styles.axisLabel}
                data-state={state}
                data-year={month === 1 || undefined}
                data-selected={(selected?.year === year && selected.month === month) || undefined}
                style={{ left: nodes[i].x, top: m.baseline + m.labelGap }}
                aria-hidden="true"
              >
                {month === 1 ? year : MONTH_SHORT[month - 1]}
              </span>
            ))}

            {months.map(({ index, year, month, state, summary }, i) => (
              <MonthNode
                key={index}
                year={year}
                month={month}
                summary={summary}
                state={state}
                selected={selected?.year === year && selected.month === month}
                position={{ left: `${nodes[i].x}px`, top: `${nodes[i].y}px` }}
                onSelect={onSelectMonth}
              />
            ))}

            {mode === "full" &&
              months.map(({ index, year, month, summary }, i) =>
                isBusy(summary) ? (
                  <MonthCallout
                    key={index}
                    year={year}
                    month={month}
                    summary={summary!}
                    position={{ left: `${nodes[i].x}px`, top: `${CARD_TOP}px` }}
                    now={now}
                    onSelect={onSelectMonth}
                  />
                ) : null,
              )}

            {todayPoint && (
              <span className={styles.todayMarker} style={{ left: todayPoint.x, top: todayPoint.y }} aria-hidden="true">
                <span className={styles.todayLabel}>today</span>
              </span>
            )}
          </div>
        </div>
      </div>
    </figure>
  );
}
