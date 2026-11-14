import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { OrbitalMap } from "./orbital-map";

const outer = [
  { id: "s1", label: "svc-one", tone: "ok" as const, title: "Service one" },
  { id: "s2", label: "svc-two", tone: "critical" as const },
];
const inner = [{ id: "a1", label: "a01", tone: "cyan" as const }];

describe("OrbitalMap", () => {
  it("exposes the figure by its label and draws the lattice core", () => {
    const { container } = render(<OrbitalMap aria-label="System map" outer={outer} inner={inner} />);
    expect(screen.getByRole("figure", { name: "System map" })).toBeInTheDocument();
    // Two 30-edge shells plus one trail per pulse.
    expect(container.querySelectorAll("[data-lattice] line")).toHaveLength(64);
  });

  it("labels every node except the focused one, which the readout names", () => {
    render(
      <OrbitalMap
        aria-label="Map"
        outer={outer}
        inner={inner}
        readout={[{ label: "node", value: "focus-node" }]}
        caption="fig.01"
        meta="2 svc"
      />,
    );
    expect(screen.queryByText("svc-one")).toBeNull();
    expect(screen.getByText("svc-two")).toBeInTheDocument();
    expect(screen.getByText("a01")).toBeInTheDocument();
    expect(screen.getByText("focus-node")).toBeInTheDocument();
    expect(screen.getByText("fig.01")).toBeInTheDocument();
    expect(screen.getByText("2 svc")).toBeInTheDocument();
  });

  it("colours nodes by tone and keeps tooltips", () => {
    const { container } = render(<OrbitalMap aria-label="Map" outer={outer} />);
    expect(container.querySelector('[data-tone="critical"]')).not.toBeNull();
    expect(container.querySelector("title")?.textContent).toBe("Service one");
  });

  it("focuses the inner ring when there are no outer nodes, and renders bare", () => {
    const { container } = render(
      <OrbitalMap aria-label="Map" bare inner={inner} readout={[{ label: "node", value: "agent" }]} />,
    );
    expect(screen.queryByText("a01")).toBeNull();
    expect(screen.getByText("agent")).toBeInTheDocument();
    expect(container.querySelector("figure")?.className).toContain("bare");
  });

  it("renders the empty frame without nodes or readout", () => {
    render(<OrbitalMap aria-label="Empty" readout={[{ label: "node", value: "x" }]} />);
    expect(screen.queryByText("x")).toBeNull();
  });
});
