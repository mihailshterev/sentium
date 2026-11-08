/**
 * Calendar helpers on the viewer's LOCAL date. Dates travel as `YYYY-MM-DD` strings; they are never parsed with
 * `new Date("YYYY-MM-DD")`, which means UTC midnight and shows the previous day west of Greenwich.
 */

const pad2 = (value: number) => String(value).padStart(2, "0");

export const MONTH_NAMES = Array.from({ length: 12 }, (_, i) =>
  new Date(2000, i, 1).toLocaleString("en-GB", { month: "long" }),
);

export const MONTH_SHORT = Array.from({ length: 12 }, (_, i) =>
  new Date(2000, i, 1).toLocaleString("en-GB", { month: "short" }),
);

export const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] as const;

export const isoDate = (year: number, month: number, day: number) => `${year}-${pad2(month)}-${pad2(day)}`;

export const isoMonth = (year: number, month: number) => `${year}-${pad2(month)}`;

export const toIsoDate = (date: Date) => isoDate(date.getFullYear(), date.getMonth() + 1, date.getDate());

export function parseIsoDate(value: string): Date {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day);
}

export const daysInMonth = (year: number, month: number) => new Date(year, month, 0).getDate();

export const daysInYear = (year: number) => (new Date(year, 1, 29).getMonth() === 1 ? 366 : 365);

export function dayOfYear(date: Date): number {
  const start = new Date(date.getFullYear(), 0, 1);
  return (
    Math.round(
      (new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime() - start.getTime()) / 86_400_000,
    ) + 1
  );
}

/**
 * The month's days as a Monday-first grid: `null` pads the first and last week.
 */
export function monthGrid(year: number, month: number): (string | null)[] {
  const leading = (new Date(year, month - 1, 1).getDay() + 6) % 7;
  const days = daysInMonth(year, month);
  const cells: (string | null)[] = Array.from({ length: leading }, () => null);
  for (let day = 1; day <= days; day++) {
    cells.push(isoDate(year, month, day));
  }
  while (cells.length % 7 !== 0) {
    cells.push(null);
  }
  return cells;
}

export type MonthState = "past" | "current" | "future";

export function monthState(year: number, month: number, today: Date): MonthState {
  const index = year * 12 + (month - 1);
  const current = today.getFullYear() * 12 + today.getMonth();
  return index < current ? "past" : index > current ? "future" : "current";
}

/**
 * How much of the month has elapsed: 1 for past months, 0 for future ones, and for the current month the middle of
 * today (so the "today" marker sits inside its day).
 */
export function monthProgress(year: number, month: number, today: Date): number {
  const state = monthState(year, month, today);
  if (state !== "current") {
    return state === "past" ? 1 : 0;
  }
  return (today.getDate() - 0.5) / daysInMonth(year, month);
}

/** Elapsed share of the year, counting today as elapsed. */
export function yearProgress(year: number, today: Date): number {
  if (year < today.getFullYear()) return 1;
  if (year > today.getFullYear()) return 0;
  return dayOfYear(today) / daysInYear(year);
}

/** "14 November". */
export function formatDayLabel(value: string): string {
  const date = parseIsoDate(value);
  return `${date.getDate()} ${MONTH_NAMES[date.getMonth()]}`;
}

/** "Sat 14 Nov". */
export function formatShortDay(value: string): string {
  const date = parseIsoDate(value);
  return `${WEEKDAYS[(date.getDay() + 6) % 7]} ${date.getDate()} ${MONTH_SHORT[date.getMonth()]}`;
}

export const monthLabel = (year: number, month: number) => `${MONTH_NAMES[month - 1]} ${year}`;
