import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Card from "./card";

describe("Card", () => {
  it("renders its children", () => {
    render(<Card>content</Card>);
    expect(screen.getByText("content")).toBeInTheDocument();
  });

  it("adds the padded and interactive classes when requested", () => {
    render(
      <Card padded interactive data-testid="c">
        x
      </Card>,
    );
    const cls = screen.getByTestId("c").className;
    expect(cls).toContain("padded");
    expect(cls).toContain("interactive");
  });

  it("omits modifier classes by default", () => {
    render(<Card data-testid="c">x</Card>);
    const cls = screen.getByTestId("c").className;
    expect(cls).not.toContain("padded");
    expect(cls).not.toContain("interactive");
  });

  it("forwards DOM props such as onClick", async () => {
    const onClick = vi.fn();
    render(
      <Card onClick={onClick} data-testid="c">
        x
      </Card>,
    );
    await userEvent.click(screen.getByTestId("c"));
    expect(onClick).toHaveBeenCalledOnce();
  });
});
