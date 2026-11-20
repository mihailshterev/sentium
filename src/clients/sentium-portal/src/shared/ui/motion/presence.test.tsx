import { act, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Animated } from "./animated";
import { Presence } from "./presence";

function View({ show, name = "panel" }: { show: boolean; name?: string }) {
  return (
    <Presence>
      {show && (
        <Animated key={name} from={{ opacity: 0 }} exitTo={{ opacity: 0 }}>
          {name}
        </Animated>
      )}
    </Presence>
  );
}

describe("Presence", () => {
  afterEach(() => {
    delete (HTMLElement.prototype as Partial<HTMLElement>).animate;
  });

  it("removes a child at once where animations are unavailable", () => {
    const { rerender } = render(<View show />);
    rerender(<View show={false} />);

    expect(screen.queryByText("panel")).toBeNull();
  });

  it("keeps an exiting child until its exit animation finishes", async () => {
    let finish!: () => void;
    const finished = new Promise<void>((resolve) => (finish = resolve));
    HTMLElement.prototype.animate = vi.fn(() => ({ finished, cancel: vi.fn() }) as unknown as Animation);

    const { rerender } = render(<View show />);
    rerender(<View show={false} />);
    expect(screen.getByText("panel")).toBeTruthy();

    await act(async () => finish());
    expect(screen.queryByText("panel")).toBeNull();
  });

  it("swaps keyed children, entering the new one while the old one exits", () => {
    HTMLElement.prototype.animate = vi.fn(
      () => ({ finished: new Promise(() => {}), cancel: vi.fn() }) as unknown as Animation,
    );

    const { rerender } = render(<View show name="first" />);
    rerender(<View show name="second" />);

    expect(screen.getByText("first")).toBeTruthy();
    expect(screen.getByText("second")).toBeTruthy();
  });
});
