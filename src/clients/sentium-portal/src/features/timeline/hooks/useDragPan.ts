import { useEffect, useState, type RefObject } from "react";

/** Pixels the mouse must travel before a press becomes a pan (shorter is a click). */
const DRAG_THRESHOLD = 5;
/** Share of the glide speed kept per 16 ms frame after release. */
const FRICTION = 0.92;
/** Glide stops below this speed (px/ms). */
const MIN_SPEED = 0.02;
/** A pointer held still this long before release lands without a glide. */
const GLIDE_WINDOW_MS = 80;

/**
 * Lets a mouse (or pen) drag a horizontal scroller by its content, with a short glide after release. Touch keeps its
 * native swipe. A press that moves less than a few pixels stays a click, and the click that ends a real drag is
 * swallowed, so letting go over a button does not trigger it. Scrolls by deltas, never to absolute positions, so
 * content prepended mid-drag (and the scroll compensation that comes with it) is respected.
 *
 * @returns Whether a pan is in progress (for the grabbing cursor).
 */
export function useDragPan(ref: RefObject<HTMLElement | null>): boolean {
  const [panning, setPanning] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    let pointerId: number | null = null;
    let startX = 0;
    let lastX = 0;
    let lastTime = 0;
    let velocity = 0;
    let dragged = false;
    let frame = 0;

    const stopGlide = () => {
      cancelAnimationFrame(frame);
      frame = 0;
    };

    const glide = (initial: number) => {
      let speed = initial;
      let previous = performance.now();
      const step = (now: number) => {
        const elapsed = now - previous;
        previous = now;
        el.scrollLeft -= speed * elapsed;
        speed *= Math.pow(FRICTION, elapsed / 16);
        frame = Math.abs(speed) > MIN_SPEED ? requestAnimationFrame(step) : 0;
      };
      frame = requestAnimationFrame(step);
    };

    const onDown = (e: PointerEvent) => {
      if (e.pointerType === "touch" || e.button !== 0) return;
      stopGlide();
      pointerId = e.pointerId;
      startX = lastX = e.clientX;
      lastTime = e.timeStamp;
      velocity = 0;
      dragged = false;
    };

    const onMove = (e: PointerEvent) => {
      if (e.pointerId !== pointerId) return;
      if (!dragged) {
        if (Math.abs(e.clientX - startX) < DRAG_THRESHOLD) return;
        dragged = true;
        try {
          el.setPointerCapture?.(e.pointerId);
        } catch {
          // The pointer is already gone (released between events): nothing to capture.
        }
        setPanning(true);
      }
      const dx = e.clientX - lastX;
      const dt = Math.max(1, e.timeStamp - lastTime);
      el.scrollLeft -= dx;
      velocity = 0.8 * (dx / dt) + 0.2 * velocity;
      lastX = e.clientX;
      lastTime = e.timeStamp;
    };

    const swallowClick = (e: MouseEvent) => {
      e.stopPropagation();
      e.preventDefault();
    };

    const onUp = (e: PointerEvent) => {
      if (e.pointerId !== pointerId) return;
      pointerId = null;
      if (!dragged) return;
      dragged = false;
      setPanning(false);

      el.addEventListener("click", swallowClick, { capture: true, once: true });
      setTimeout(() => el.removeEventListener("click", swallowClick, { capture: true }), 0);

      const reduceMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
      if (e.type === "pointerup" && !reduceMotion && e.timeStamp - lastTime < GLIDE_WINDOW_MS) {
        glide(velocity);
      }
    };

    const onDragStart = (e: DragEvent) => e.preventDefault();

    el.addEventListener("pointerdown", onDown);
    el.addEventListener("pointermove", onMove);
    el.addEventListener("pointerup", onUp);
    el.addEventListener("pointercancel", onUp);
    el.addEventListener("dragstart", onDragStart);
    el.addEventListener("wheel", stopGlide, { passive: true });

    return () => {
      stopGlide();
      el.removeEventListener("pointerdown", onDown);
      el.removeEventListener("pointermove", onMove);
      el.removeEventListener("pointerup", onUp);
      el.removeEventListener("pointercancel", onUp);
      el.removeEventListener("dragstart", onDragStart);
      el.removeEventListener("wheel", stopGlide);
      el.removeEventListener("click", swallowClick, { capture: true });
    };
  }, [ref]);

  return panning;
}
