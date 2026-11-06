import { describe, it, expect } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { Page } from "./page";

describe("Page", () => {
  it("renders the title, subtitle, actions and content", () => {
    render(
      <Page title="Agents" subtitle="Manage agents" actions={<button type="button">New</button>}>
        <p>body</p>
      </Page>,
    );
    expect(screen.getByRole("heading", { level: 1, name: "Agents" })).toBeInTheDocument();
    expect(screen.getByText("Manage agents")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New" })).toBeInTheDocument();
    expect(screen.getByText("body")).toBeInTheDocument();
  });

  it("turns the header into a glass bar once the body scrolls", () => {
    render(
      <Page title="Agents">
        <p>body</p>
      </Page>,
    );
    const header = screen.getByRole("banner");
    expect(header).not.toHaveAttribute("data-scrolled");
    const scroller = screen.getByText("body").parentElement!.parentElement!;
    scroller.scrollTop = 40;
    fireEvent.scroll(scroller);
    expect(header).toHaveAttribute("data-scrolled");
  });

  it("renders a fixed body without a scroll container when scroll is false", () => {
    render(
      <Page title="Chat" scroll={false}>
        <p>panes</p>
      </Page>,
    );
    expect(screen.getByText("panes").parentElement).toHaveAttribute("data-width", "default");
  });
});
