import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import Placeholder from "./placeholder";

describe("Placeholder", () => {
  it("renders the title passed as prop", () => {
    render(<Placeholder title="Dashboard" />);
    expect(screen.getByText("Dashboard Page")).toBeInTheDocument();
  });

  it("renders the under-construction message", () => {
    render(<Placeholder title="Test" />);
    expect(screen.getByText(/under construction/i)).toBeInTheDocument();
  });

  it("renders correctly with an empty string title", () => {
    render(<Placeholder title="" />);
    expect(screen.getByText(/page/i)).toBeInTheDocument();
  });

  it("renders with a very long title", () => {
    const longTitle = "A".repeat(300);
    render(<Placeholder title={longTitle} />);
    expect(screen.getByText(`${longTitle} Page`)).toBeInTheDocument();
  });
});
