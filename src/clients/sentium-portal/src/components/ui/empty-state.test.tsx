import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import EmptyState from "./empty-state";

describe("EmptyState", () => {
  it("renders the icon and title", () => {
    render(<EmptyState icon={<span data-testid="icon" />} title="Nothing here" />);
    expect(screen.getByTestId("icon")).toBeInTheDocument();
    expect(screen.getByText("Nothing here")).toBeInTheDocument();
  });

  it("renders the hint when provided", () => {
    render(<EmptyState icon={null} title="Empty" hint="Try adding one" />);
    expect(screen.getByText("Try adding one")).toBeInTheDocument();
  });

  it("renders an action when provided", () => {
    render(<EmptyState icon={null} title="Empty" action={<button>Add</button>} />);
    expect(screen.getByRole("button", { name: "Add" })).toBeInTheDocument();
  });

  it("omits hint and action when not provided", () => {
    render(<EmptyState icon={null} title="Empty" />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
