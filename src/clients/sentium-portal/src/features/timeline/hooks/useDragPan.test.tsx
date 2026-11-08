import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { useRef } from "react";
import { useDragPan } from "./useDragPan";

function Rail({ onNode }: { onNode: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  const panning = useDragPan(ref);
  return (
    <div ref={ref} data-testid="rail" data-panning={panning || undefined}>
      <button type="button" onClick={onNode}>
        node
      </button>
    </div>
  );
}

function setup() {
  const onNode = vi.fn();
  render(<Rail onNode={onNode} />);
  const rail = screen.getByTestId("rail");
  // jsdom has no layout: back scrollLeft with a plain value.
  let left = 500;
  Object.defineProperty(rail, "scrollLeft", {
    configurable: true,
    get: () => left,
    set: (value: number) => {
      left = value;
    },
  });
  return { rail, node: screen.getByRole("button", { name: "node" }), onNode, scrollLeft: () => left };
}

const mouse = { pointerId: 1, pointerType: "mouse", button: 0 };

describe("useDragPan", () => {
  it("pans the scroller by the drag and swallows the click that ends it", () => {
    const { rail, node, onNode, scrollLeft } = setup();

    fireEvent.pointerDown(node, { ...mouse, clientX: 300 });
    // Under the threshold: still a press, not a pan.
    fireEvent.pointerMove(rail, { ...mouse, clientX: 298 });
    expect(scrollLeft()).toBe(500);
    expect(rail).not.toHaveAttribute("data-panning");

    fireEvent.pointerMove(rail, { ...mouse, clientX: 200 });
    // Dragging left by 100px moves the content left: later months come into view.
    expect(scrollLeft()).toBe(600);
    expect(rail).toHaveAttribute("data-panning");

    fireEvent.pointerUp(rail, { ...mouse, clientX: 200 });
    fireEvent.click(node);

    expect(rail).not.toHaveAttribute("data-panning");
    expect(onNode).not.toHaveBeenCalled();
  });

  it("keeps a short press a click", () => {
    const { rail, node, onNode, scrollLeft } = setup();

    fireEvent.pointerDown(node, { ...mouse, clientX: 300 });
    fireEvent.pointerMove(rail, { ...mouse, clientX: 302 });
    fireEvent.pointerUp(node, { ...mouse, clientX: 302 });
    fireEvent.click(node);

    expect(onNode).toHaveBeenCalledOnce();
    expect(scrollLeft()).toBe(500);
  });

  it("leaves touch to the native swipe", () => {
    const { rail, scrollLeft } = setup();
    const touch = { ...mouse, pointerType: "touch" };

    fireEvent.pointerDown(rail, { ...touch, clientX: 300 });
    fireEvent.pointerMove(rail, { ...touch, clientX: 100 });

    expect(scrollLeft()).toBe(500);
    expect(rail).not.toHaveAttribute("data-panning");
  });
});
