import styles from "./sentium-mark.module.css";

interface SentiumMarkProps {
  /** Rendered width and height in pixels. */
  size?: number;
  className?: string;
}

/**
 * The Sentium mark: an open hexagonal cell whose core reaches towards three nodes. Decorative, so it is
 * hidden from assistive tech; pair it with the wordmark or a label.
 */
export function SentiumMark({ size = 28, className }: SentiumMarkProps) {
  return (
    <svg
      className={[styles.mark, className ?? ""].filter(Boolean).join(" ")}
      width={size}
      height={size}
      viewBox="0 0 32 32"
      aria-hidden="true"
    >
      <path d="M20.28 5.47 27.26 9.5v8.06M22.98 24.97 16 29l-6.98-4.03M4.74 17.56V9.5l6.98-4.03" />
      <path d="M16 16V9M16 16l6.06 3.5M16 16l-6.06 3.5" />
      <circle className={styles.node} cx="16" cy="3" r="2.4" />
      <circle className={styles.node} cx="27.26" cy="22.5" r="2.4" />
      <circle className={styles.node} cx="4.74" cy="22.5" r="2.4" />
    </svg>
  );
}
