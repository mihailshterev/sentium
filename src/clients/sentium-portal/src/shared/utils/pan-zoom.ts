/** A uniform scale `k` followed by a translation: screen = world * k + (x, y). */
export interface ViewTransform {
  x: number;
  y: number;
  k: number;
}

export interface PanZoomOptions {
  minScale: number;
  maxScale: number;
  onChange: (transform: ViewTransform) => void;
}

export interface PanZoomHandle {
  readonly transform: ViewTransform;
  setTransform: (transform: ViewTransform) => void;
  /** Converts an element-relative screen point to world coordinates. */
  toWorld: (screenX: number, screenY: number) => [number, number];
  /** True when the last pointer press moved; a click that ends a pan should not select anything. */
  readonly dragged: boolean;
  detach: () => void;
}

// Per wheel "line" and per pixel, the same rates d3-zoom used, so zoom speed feels unchanged.
const WHEEL_LINE_RATE = 0.05;
const WHEEL_PIXEL_RATE = 0.002;
const PINCH_WHEEL_BOOST = 10;
const DOUBLE_CLICK_FACTOR = 2;

export const identityTransform: ViewTransform = { x: 0, y: 0, k: 1 };

/**
 * Drag-to-pan, wheel/pinch/double-click zoom on an element (usually a canvas), with the zoom anchored at the pointer.
 * Only reports transforms; the caller redraws.
 */
export function attachPanZoom(element: HTMLElement, options: PanZoomOptions): PanZoomHandle {
  let transform: ViewTransform = identityTransform;
  let dragged = false;
  const pointers = new Map<number, { x: number; y: number }>();
  let pinchDistance = 0;

  const clampScale = (k: number) => Math.max(options.minScale, Math.min(options.maxScale, k));

  const update = (next: ViewTransform) => {
    transform = next;
    options.onChange(transform);
  };

  const local = (event: { clientX: number; clientY: number }): [number, number] => {
    const rect = element.getBoundingClientRect();
    return [event.clientX - rect.left, event.clientY - rect.top];
  };

  const zoomAround = (screenX: number, screenY: number, factor: number) => {
    const k = clampScale(transform.k * factor);
    const worldX = (screenX - transform.x) / transform.k;
    const worldY = (screenY - transform.y) / transform.k;
    update({ k, x: screenX - worldX * k, y: screenY - worldY * k });
  };

  const onWheel = (event: WheelEvent) => {
    event.preventDefault();
    const rate = event.deltaMode === 1 ? WHEEL_LINE_RATE : event.deltaMode ? 1 : WHEEL_PIXEL_RATE;
    const delta = -event.deltaY * rate * (event.ctrlKey ? PINCH_WHEEL_BOOST : 1);
    const [x, y] = local(event);
    zoomAround(x, y, Math.pow(2, delta));
  };

  const onPointerDown = (event: PointerEvent) => {
    if (event.pointerType === "mouse" && event.button !== 0) {
      return;
    }
    if (pointers.size === 0) {
      dragged = false;
    }
    element.setPointerCapture?.(event.pointerId);
    pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    pinchDistance = pointers.size === 2 ? distanceBetween(pointers) : 0;
  };

  const onPointerMove = (event: PointerEvent) => {
    const previous = pointers.get(event.pointerId);
    if (!previous) {
      return;
    }
    if (event.clientX === previous.x && event.clientY === previous.y) {
      return;
    }
    dragged = true;
    pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });

    if (pointers.size === 1) {
      update({ ...transform, x: transform.x + event.clientX - previous.x, y: transform.y + event.clientY - previous.y });
      return;
    }

    if (pointers.size === 2 && pinchDistance > 0) {
      const distance = distanceBetween(pointers);
      const [cx, cy] = local(midpoint(pointers));
      zoomAround(cx, cy, distance / pinchDistance);
      pinchDistance = distance;
    }
  };

  const onPointerEnd = (event: PointerEvent) => {
    pointers.delete(event.pointerId);
    pinchDistance = pointers.size === 2 ? distanceBetween(pointers) : 0;
  };

  const onDoubleClick = (event: MouseEvent) => {
    const [x, y] = local(event);
    zoomAround(x, y, event.shiftKey ? 1 / DOUBLE_CLICK_FACTOR : DOUBLE_CLICK_FACTOR);
  };

  element.style.touchAction = "none";
  element.addEventListener("wheel", onWheel, { passive: false });
  element.addEventListener("pointerdown", onPointerDown);
  element.addEventListener("pointermove", onPointerMove);
  element.addEventListener("pointerup", onPointerEnd);
  element.addEventListener("pointercancel", onPointerEnd);
  element.addEventListener("dblclick", onDoubleClick);

  return {
    get transform() {
      return transform;
    },
    get dragged() {
      return dragged;
    },
    setTransform: (next) => update({ ...next, k: clampScale(next.k) }),
    toWorld: (screenX, screenY) => [(screenX - transform.x) / transform.k, (screenY - transform.y) / transform.k],
    detach: () => {
      element.removeEventListener("wheel", onWheel);
      element.removeEventListener("pointerdown", onPointerDown);
      element.removeEventListener("pointermove", onPointerMove);
      element.removeEventListener("pointerup", onPointerEnd);
      element.removeEventListener("pointercancel", onPointerEnd);
      element.removeEventListener("dblclick", onDoubleClick);
    },
  };
}

function distanceBetween(pointers: Map<number, { x: number; y: number }>): number {
  const [a, b] = [...pointers.values()];
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function midpoint(pointers: Map<number, { x: number; y: number }>): { clientX: number; clientY: number } {
  const [a, b] = [...pointers.values()];
  return { clientX: (a.x + b.x) / 2, clientY: (a.y + b.y) / 2 };
}
