import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatCard, StatGrid } from "./stat-card";

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

describe("StatGrid", () => {
  it("wraps cells with auto-fit columns by default", () => {
    const { container } = render(
      <StatGrid minCellWidth={140}>
        <StatCard icon={null} value={1} label="One" />
      </StatGrid>,
    );
    const grid = container.firstElementChild as HTMLElement;
    expect(grid.style.getPropertyValue("--min-cell")).toBe("140px");
    expect(grid.style.getPropertyValue("--cols")).toBe("");
    expect(screen.getByText("One")).toBeInTheDocument();
  });

  it("uses a fixed column count when given", () => {
    const { container } = render(
      <StatGrid columns={3} aria-busy>
        <StatCard icon={null} value={1} label="One" />
      </StatGrid>,
    );
    const grid = container.firstElementChild as HTMLElement;
    expect(grid.style.getPropertyValue("--cols")).toBe("repeat(3, minmax(0, 1fr))");
    expect(grid).toHaveAttribute("aria-busy", "true");
  });
});
