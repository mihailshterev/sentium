import { describe, expect, it, vi } from "vitest";
import { attachPanZoom } from "./pan-zoom";

const pointer = (type: string, init: Partial<PointerEventInit>) =>
  new MouseEvent(type, { bubbles: true, ...init }) as unknown as PointerEvent;

function setup() {
  const element = document.createElement("div");
  element.getBoundingClientRect = () => ({ left: 0, top: 0 }) as DOMRect;
  const onChange = vi.fn();
  const handle = attachPanZoom(element, { minScale: 0.5, maxScale: 4, onChange });
  return { element, handle, onChange };
}

function press(element: HTMLElement, x: number, y: number) {
  const down = pointer("pointerdown", { clientX: x, clientY: y, button: 0 });
  Object.assign(down, { pointerId: 1, pointerType: "mouse" });
  element.dispatchEvent(down);
}

function move(element: HTMLElement, x: number, y: number) {
  const event = pointer("pointermove", { clientX: x, clientY: y });
  Object.assign(event, { pointerId: 1, pointerType: "mouse" });
  element.dispatchEvent(event);
}

describe("attachPanZoom", () => {
  it("pans by the pointer delta and marks the press as a drag", () => {
    const { element, handle } = setup();

    press(element, 10, 10);
    move(element, 30, 15);

    expect(handle.transform).toEqual({ x: 20, y: 5, k: 1 });
    expect(handle.dragged).toBe(true);
  });

  it("does not count a press without movement as a drag", () => {
    const { element, handle } = setup();

    press(element, 10, 10);

    expect(handle.dragged).toBe(false);
  });

  it("zooms around the pointer and clamps the scale", () => {
    const { element, handle } = setup();

    element.dispatchEvent(new WheelEvent("wheel", { deltaY: -500, clientX: 100, clientY: 50, cancelable: true }));

    expect(handle.transform.k).toBe(2);
    expect(handle.toWorld(100, 50)).toEqual([100, 50]);

    handle.setTransform({ x: 0, y: 0, k: 100 });
    expect(handle.transform.k).toBe(4);
  });

  it("stops listening after detach", () => {
    const { element, handle, onChange } = setup();
    handle.detach();

    element.dispatchEvent(new WheelEvent("wheel", { deltaY: -500, cancelable: true }));

    expect(onChange).not.toHaveBeenCalled();
  });
});
