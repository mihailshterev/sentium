import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import PageHeader from "./page-header";

describe("PageHeader", () => {
  it("renders the title as a heading", () => {
    render(<PageHeader title="Agents" />);
    expect(screen.getByRole("heading", { name: "Agents" })).toBeInTheDocument();
  });

  it("renders the subtitle when provided", () => {
    render(<PageHeader title="Agents" subtitle="Manage your agents" />);
    expect(screen.getByText("Manage your agents")).toBeInTheDocument();
  });

  it("renders the icon and right-side content", () => {
    render(<PageHeader title="Agents" icon={<span data-testid="icon" />} right={<button>New</button>} />);
    expect(screen.getByTestId("icon")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New" })).toBeInTheDocument();
  });

  it("omits subtitle when not provided", () => {
    render(<PageHeader title="Agents" />);
    expect(screen.queryByText("Manage your agents")).not.toBeInTheDocument();
  });
});
