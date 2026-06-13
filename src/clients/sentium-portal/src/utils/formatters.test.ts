import { describe, it, expect, afterEach, vi } from "vitest";
import { formatBytesToMb, formatTimeHms, formatDateTimeShort, formatRelativeTime } from "./formatters";

afterEach(() => {
  vi.useRealTimers();
});

describe("formatBytesToMb", () => {
  it("formats sub-kilobyte values as bytes", () => {
    expect(formatBytesToMb(512)).toBe("512 B");
  });

  it("formats sub-megabyte values as KB with one decimal", () => {
    expect(formatBytesToMb(2048)).toBe("2.0 KB");
  });

  it("formats megabyte-or-larger values as MB with one decimal", () => {
    expect(formatBytesToMb(5 * 1024 * 1024)).toBe("5.0 MB");
  });

  it("treats exactly 1024 as KB (boundary)", () => {
    expect(formatBytesToMb(1024)).toBe("1.0 KB");
  });

  it("treats exactly 1 MB as MB (boundary)", () => {
    expect(formatBytesToMb(1024 * 1024)).toBe("1.0 MB");
  });
});

describe("formatTimeHms", () => {
  it("renders a two-digit hour:minute:second time", () => {
    const out = formatTimeHms("2025-01-01T13:05:09Z", "en-GB");
    expect(out).toMatch(/\d{2}:\d{2}:\d{2}/);
  });
});

describe("formatDateTimeShort", () => {
  it("produces a non-empty short date-time string", () => {
    const out = formatDateTimeShort("2025-01-01T13:05:09Z", "en-GB");
    expect(out.length).toBeGreaterThan(0);
  });
});

describe("formatRelativeTime", () => {
  const fixNow = (iso: string) => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(iso));
  };

  it("returns 'just now' for very recent timestamps", () => {
    fixNow("2025-01-01T00:00:03Z");
    expect(formatRelativeTime("2025-01-01T00:00:00Z")).toBe("just now");
  });

  it("returns seconds for under a minute", () => {
    fixNow("2025-01-01T00:00:30Z");
    expect(formatRelativeTime("2025-01-01T00:00:00Z")).toBe("30s ago");
  });

  it("returns minutes for under an hour", () => {
    fixNow("2025-01-01T00:10:00Z");
    expect(formatRelativeTime("2025-01-01T00:00:00Z")).toBe("10m ago");
  });

  it("returns hours for under a day", () => {
    fixNow("2025-01-01T05:00:00Z");
    expect(formatRelativeTime("2025-01-01T00:00:00Z")).toBe("5h ago");
  });

  it("returns days for under a week", () => {
    fixNow("2025-01-04T00:00:00Z");
    expect(formatRelativeTime("2025-01-01T00:00:00Z")).toBe("3d ago");
  });

  it("falls back to a short date-time for a week or older", () => {
    fixNow("2025-02-01T00:00:00Z");
    const out = formatRelativeTime("2025-01-01T00:00:00Z");
    expect(out).not.toMatch(/ago$/);
    expect(out.length).toBeGreaterThan(0);
  });
});
