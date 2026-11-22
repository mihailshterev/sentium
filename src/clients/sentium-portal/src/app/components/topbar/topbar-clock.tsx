import { useEffect, useState } from "react";
import styles from "./topbar.module.css";

const MINUTE_MS = 60_000;

/** Local time to the minute, re-rendering only when the minute changes; the full date is in the tooltip. */
export function TopbarClock() {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const untilNextMinute = MINUTE_MS - (now.getTime() % MINUTE_MS);
    const id = window.setTimeout(() => setNow(new Date()), untilNextMinute);
    return () => window.clearTimeout(id);
  }, [now]);

  const time = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", hour12: false });
  const date = now.toLocaleDateString([], { weekday: "long", day: "numeric", month: "long", year: "numeric" });

  return (
    <time className={styles.clock} dateTime={now.toISOString()} title={date}>
      <span className={styles.clockDot} aria-hidden="true" />
      {time}
    </time>
  );
}
