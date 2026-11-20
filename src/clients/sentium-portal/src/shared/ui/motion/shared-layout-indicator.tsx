import { useLayoutEffect, useRef, type HTMLAttributes } from "react";
import { prefersReducedMotion } from "./reduced-motion";

interface SharedLayoutIndicatorProps extends HTMLAttributes<HTMLSpanElement> {
  /** Indicators with the same id are one indicator moving between places (the active tab, the selected item). */
  layoutId: string;
}

interface Snapshot {
  rect: DOMRect;
  at: number;
}

const DURATION_MS = 320;
// Close to a stiff, slightly underdamped spring: fast start, soft landing.
const EASING = "cubic-bezier(0.22, 1, 0.36, 1)";
/** A snapshot older than this belongs to an indicator that went away, not one that moved. */
const SNAPSHOT_TTL_MS = 100;

const snapshots = new Map<string, Snapshot>();

/**
 * An absolutely positioned highlight that slides from where its previous instance was. Render it inside the active
 * item only; when the active item changes, the new instance animates from the old one's position and size.
 */
export function SharedLayoutIndicator({ layoutId, ...rest }: SharedLayoutIndicatorProps) {
  const ref = useRef<HTMLSpanElement>(null);

  useLayoutEffect(() => {
    const element = ref.current;
    if (!element) {
      return;
    }

    const previous = snapshots.get(layoutId);
    snapshots.delete(layoutId);
    if (previous && performance.now() - previous.at < SNAPSHOT_TTL_MS) {
      slideFrom(element, previous.rect);
    }

    // Runs before React detaches the element, so it can still be measured.
    return () => {
      snapshots.set(layoutId, { rect: element.getBoundingClientRect(), at: performance.now() });
    };
  }, [layoutId]);

  return <span ref={ref} {...rest} />;
}

function slideFrom(element: HTMLElement, from: DOMRect) {
  if (typeof element.animate !== "function" || prefersReducedMotion()) {
    return;
  }

  const to = element.getBoundingClientRect();
  const dx = from.left - to.left;
  const dy = from.top - to.top;
  if (dx === 0 && dy === 0 && from.width === to.width && from.height === to.height) {
    return;
  }

  // Size animates as width/height rather than scale, so rounded corners and borders do not stretch on the way.
  element.animate(
    [
      { transform: `translate(${dx}px, ${dy}px)`, width: `${from.width}px`, height: `${from.height}px` },
      { transform: "translate(0, 0)", width: `${to.width}px`, height: `${to.height}px` },
    ],
    { duration: DURATION_MS, easing: EASING },
  );
}
