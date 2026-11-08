import { useEffect, useState } from "react";
import styles from "./page-header.module.css";

const formatLocal = (date: Date) =>
  date.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });

export function SystemReadout() {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(id);
  }, []);

  return (
    <span className={styles.readout}>
      <span>V = c</span>
      <span className={styles.readoutDot} />
      <span>{formatLocal(now)}</span>
    </span>
  );
}
