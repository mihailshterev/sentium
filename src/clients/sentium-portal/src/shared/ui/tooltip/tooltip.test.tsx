import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Tooltip } from "./tooltip";

describe("Tooltip", () => {
  it("shows its content on hover and hides it on leave", async () => {
    render(
      <Tooltip content="Dashboard" delayMs={0}>
        <button type="button">D</button>
      </Tooltip>,
    );
    await userEvent.hover(screen.getByRole("button"));
    expect(await screen.findByRole("tooltip")).toHaveTextContent("Dashboard");
    await userEvent.unhover(screen.getByRole("button"));
    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("shows on keyboard focus", async () => {
    render(
      <Tooltip content="Settings" delayMs={0}>
        <button type="button">S</button>
      </Tooltip>,
    );
    await userEvent.tab();
    expect(await screen.findByRole("tooltip")).toHaveTextContent("Settings");
  });

  it("renders only the trigger when disabled", async () => {
    render(
      <Tooltip content="Nope" disabled delayMs={0}>
        <button type="button">N</button>
      </Tooltip>,
    );
    await userEvent.hover(screen.getByRole("button"));
    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });
});
