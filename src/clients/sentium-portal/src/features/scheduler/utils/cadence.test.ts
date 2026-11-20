import { describe, expect, it } from "vitest";
import { type Cadence, describeCadence, parseCron, toCron, withType } from "./cadence";

describe("cadence", () => {
  const roundTrips: [Cadence, string][] = [
    [{ type: "daily", time: "07:30" }, "0 30 7 ? * *"],
    [{ type: "weekdays", time: "08:05" }, "0 5 8 ? * MON-FRI"],
    [{ type: "weekly", days: [0], time: "18:00" }, "0 0 18 ? * SUN"],
    [{ type: "weekly", days: [1, 3, 5], time: "06:15" }, "0 15 6 ? * MON,WED,FRI"],
    [{ type: "monthly", day: 1, time: "09:00" }, "0 0 9 1 * ?"],
    [{ type: "hourly", minute: 15 }, "0 15 * ? * *"],
  ];

  it.each(roundTrips)("turns %o into %s and back", (cadence, cron) => {
    expect(toCron(cadence)).toBe(cron);
    expect(parseCron(cron)).toEqual(cadence);
  });

  it("keeps an expression it cannot express as custom", () => {
    expect(parseCron("0 0/15 9-17 ? * MON-FRI")).toEqual({ type: "custom", cron: "0 0/15 9-17 ? * MON-FRI" });
    expect(parseCron("0 * * * *")).toEqual({ type: "custom", cron: "0 * * * *" });
  });

  it("describes the shipped templates in words", () => {
    expect(describeCadence("0 30 7 ? * *")).toBe("Every day at 07:30");
    expect(describeCadence("0 0 18 ? * SUN")).toBe("Every Sunday at 18:00");
    expect(describeCadence("0 15 * ? * *")).toBe("Every hour at :15");
  });

  it("keeps the time of day when switching type", () => {
    expect(withType({ type: "daily", time: "21:45" }, "weekly")).toEqual({ type: "weekly", days: [1], time: "21:45" });
    expect(withType({ type: "daily", time: "21:45" }, "custom")).toEqual({ type: "custom", cron: "0 45 21 ? * *" });
  });
});
