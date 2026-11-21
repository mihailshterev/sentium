// Matches the portal's shared formatters, so numbers read the same on every page and in tests.
export const LOCALE = "en-GB";

const compact = new Intl.NumberFormat(LOCALE, { notation: "compact", maximumFractionDigits: 1 });
const whole = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 });
const oneDecimal = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 1 });

const COMPACT_FROM = 10_000;
const MS_PER_SECOND = 1000;

/** Counts in full below 10,000 and compact above (12.9k), one style per value. */
export function formatCount(value: number): string {
  return Math.abs(value) >= COMPACT_FROM ? compact.format(value) : whole.format(value);
}

export function formatRate(value: number): string {
  return Math.abs(value) >= COMPACT_FROM ? compact.format(value) : oneDecimal.format(value);
}

export function formatPercent(value: number): string {
  return `${whole.format(value)}%`;
}

/** Sub-second durations in milliseconds, longer ones in seconds; null renders as an em dash. */
export function formatSeconds(seconds: number | null): string {
  if (seconds === null) {
    return "—";
  }

  return seconds < 1 ? `${whole.format(seconds * MS_PER_SECOND)} ms` : `${oneDecimal.format(seconds)} s`;
}
