import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import styles from "./tooltip.module.css";

type Side = "top" | "right" | "bottom";

interface TooltipProps {
  content: React.ReactNode;
  side?: Side;
  /** Render the trigger without any tooltip behaviour (e.g. when a label is already visible). */
  disabled?: boolean;
  delayMs?: number;
  children: React.ReactNode;
}

interface Position {
  left: number;
  top: number;
}

const GAP = 8;

function place(rect: DOMRect, side: Side): Position {
  switch (side) {
    case "right":
      return { left: rect.right + GAP, top: rect.top + rect.height / 2 };
    case "bottom":
      return { left: rect.left + rect.width / 2, top: rect.bottom + GAP };
    default:
      return { left: rect.left + rect.width / 2, top: rect.top - GAP };
  }
}

/**
 * Small label shown on hover / keyboard focus. Rendered in a portal so scroll containers can't clip it.
 * Visual only: give icon-only triggers their own accessible name (aria-label).
 */
export function Tooltip({ content, side = "top", disabled = false, delayMs = 300, children }: TooltipProps) {
  const [position, setPosition] = useState<Position | null>(null);
  const timer = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  if (disabled) {
    return <>{children}</>;
  }

  const show = (target: EventTarget & Element) => {
    const anchor = (target.firstElementChild ?? target) as Element;
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => setPosition(place(anchor.getBoundingClientRect(), side)), delayMs);
  };

  const hide = () => {
    window.clearTimeout(timer.current);
    setPosition(null);
  };

  return (
    <span
      className={styles.trigger}
      onMouseEnter={(e) => show(e.currentTarget)}
      onMouseLeave={hide}
      onFocus={(e) => show(e.currentTarget)}
      onBlur={hide}
      onClick={hide}
    >
      {children}
      {position &&
        createPortal(
          <span role="tooltip" className={`${styles.bubble} ${styles[side]}`} style={position}>
            {content}
          </span>,
          document.body,
        )}
    </span>
  );
}
