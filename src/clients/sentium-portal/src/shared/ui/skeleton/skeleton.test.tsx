import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { Skeleton, SkeletonText } from "./skeleton";

describe("Skeleton", () => {
  it("renders a decorative block with the given size", () => {
    const { container } = render(<Skeleton width={40} height={10} radius="full" />);
    const el = container.firstElementChild as HTMLElement;
    expect(el).toHaveAttribute("aria-hidden", "true");
    expect(el.style.width).toBe("40px");
    expect(el.style.height).toBe("10px");
  });

  it("renders the requested number of text lines", () => {
    const { container } = render(<SkeletonText lines={4} />);
    expect(container.firstElementChild!.children).toHaveLength(4);
  });
});
