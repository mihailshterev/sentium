import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import StatCard from "./stat-card";

describe("StatCard", () => {
  it("renders the value and label", () => {
    render(<StatCard icon={<span data-testid="icon" />} value={42} label="Executions" />);
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("Executions")).toBeInTheDocument();
    expect(screen.getByTestId("icon")).toBeInTheDocument();
  });

  it("renders a chip when provided", () => {
    render(<StatCard icon={null} value={1} label="Up" chip="+5%" chipVariant="green" />);
    expect(screen.getByText("+5%")).toBeInTheDocument();
  });

  it("omits the chip when not provided", () => {
    render(<StatCard icon={null} value={1} label="Up" />);
    expect(screen.queryByText("+5%")).not.toBeInTheDocument();
  });

  it("renders with a non-default icon color without error", () => {
    render(<StatCard icon={<span data-testid="icon" />} value={1} label="Up" iconColor="red" />);
    expect(screen.getByText("Up")).toBeInTheDocument();
    expect(screen.getByTestId("icon")).toBeInTheDocument();
  });
});
