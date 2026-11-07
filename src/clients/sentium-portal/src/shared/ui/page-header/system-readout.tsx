import { useEffect, useState } from "react";
import styles from "./page-header.module.css";

const formatUtc = (date: Date) => date.toISOString().slice(11, 19);

/** Quiet top-right status line: the physical constant joke plus a live UTC clock. */
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
      <span>UTC {formatUtc(now)}</span>
    </span>
  );
}
