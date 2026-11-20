/**
 * A human-friendly cadence over Quartz cron (seconds minutes hours day-of-month month day-of-week). Every simple
 * cadence round-trips through `toCron` and `parseCron`; anything else stays a custom expression.
 */
export type Cadence =
  | { type: "hourly"; minute: number }
  | { type: "daily"; time: string }
  | { type: "weekdays"; time: string }
  | { type: "weekly"; days: number[]; time: string }
  | { type: "monthly"; day: number; time: string }
  | { type: "custom"; cron: string };

export type CadenceType = Cadence["type"];

/** Quartz day-of-week names, Sunday first. */
export const WEEKDAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"] as const;

export const WEEKDAY_LABELS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

export const DEFAULT_TIME = "07:30";

const NUMBER = /^\d{1,2}$/;

const pad = (n: number) => String(n).padStart(2, "0");

const splitTime = (time: string): [number, number] => {
  const [h, m] = time.split(":").map(Number);
  return [Number.isFinite(h) ? h : 0, Number.isFinite(m) ? m : 0];
};

export function toCron(cadence: Cadence): string {
  if (cadence.type === "custom") {
    return cadence.cron.trim();
  }
  if (cadence.type === "hourly") {
    return `0 ${cadence.minute} * ? * *`;
  }

  const [hour, minute] = splitTime(cadence.time);
  switch (cadence.type) {
    case "daily":
      return `0 ${minute} ${hour} ? * *`;
    case "weekdays":
      return `0 ${minute} ${hour} ? * MON-FRI`;
    case "weekly": {
      const days = [...cadence.days].sort((a, b) => a - b).map((d) => WEEKDAYS[d]);
      return `0 ${minute} ${hour} ? * ${days.length > 0 ? days.join(",") : "SUN"}`;
    }
    case "monthly":
      return `0 ${minute} ${hour} ${cadence.day} * ?`;
  }
}

export function parseCron(cron: string): Cadence {
  const fields = cron.trim().split(/\s+/);
  if (fields.length !== 6 || fields[0] !== "0" || !NUMBER.test(fields[1])) {
    return { type: "custom", cron };
  }

  const [, minuteField, hourField, dom, month, dow] = fields;
  const minute = Number(minuteField);

  if (hourField === "*" && dom === "?" && month === "*" && dow === "*") {
    return { type: "hourly", minute };
  }
  if (!NUMBER.test(hourField) || month !== "*") {
    return { type: "custom", cron };
  }

  const time = `${pad(Number(hourField))}:${pad(minute)}`;

  if (dom === "?" && dow === "*") {
    return { type: "daily", time };
  }
  if (dom === "?" && dow === "MON-FRI") {
    return { type: "weekdays", time };
  }
  if (dom === "?") {
    const days = dow.split(",").map((d) => WEEKDAYS.indexOf(d.toUpperCase() as (typeof WEEKDAYS)[number]));
    return days.every((d) => d >= 0) ? { type: "weekly", days, time } : { type: "custom", cron };
  }
  if (NUMBER.test(dom) && dow === "?") {
    return { type: "monthly", day: Number(dom), time };
  }
  return { type: "custom", cron };
}

export function describeCadence(cron: string): string {
  const cadence = parseCron(cron);
  switch (cadence.type) {
    case "hourly":
      return `Every hour at :${pad(cadence.minute)}`;
    case "daily":
      return `Every day at ${cadence.time}`;
    case "weekdays":
      return `Weekdays at ${cadence.time}`;
    case "weekly": {
      const days = [...cadence.days].sort((a, b) => a - b).map((d) => WEEKDAY_LABELS[d]);
      return `Every ${days.join(", ")} at ${cadence.time}`;
    }
    case "monthly":
      return `Monthly on day ${cadence.day} at ${cadence.time}`;
    case "custom":
      return cadence.cron;
  }
}

/** Switches the cadence type, keeping the time of day where both have one. */
export function withType(cadence: Cadence, type: CadenceType): Cadence {
  const time = "time" in cadence ? cadence.time : DEFAULT_TIME;
  switch (type) {
    case "hourly":
      return { type, minute: 0 };
    case "daily":
    case "weekdays":
      return { type, time };
    case "weekly":
      return { type, days: [1], time };
    case "monthly":
      return { type, day: 1, time };
    case "custom":
      return { type, cron: toCron(cadence) };
  }
}
