import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Button from "./button";

describe("Button", () => {
  it("renders its label", () => {
    render(<Button>Save</Button>);
    expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
  });

  it("fires onClick when enabled", async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Go</Button>);
    await userEvent.click(screen.getByRole("button", { name: "Go" }));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("is disabled when the disabled prop is set", () => {
    render(<Button disabled>Nope</Button>);
    expect(screen.getByRole("button", { name: "Nope" })).toBeDisabled();
  });

  it("is disabled and shows a spinner while loading", () => {
    const { container } = render(<Button loading>Loading</Button>);
    expect(screen.getByRole("button")).toBeDisabled();
    expect(container.querySelector("svg")).toBeInTheDocument();
  });

  it("renders an icon when provided and not loading", () => {
    render(<Button icon={<span data-testid="icon" />}>With icon</Button>);
    expect(screen.getByTestId("icon")).toBeInTheDocument();
  });
});
