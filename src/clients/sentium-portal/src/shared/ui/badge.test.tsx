import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import Badge from "./badge";

describe("Badge", () => {
  it("renders its children", () => {
    render(<Badge>Active</Badge>);
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("applies the tone class", () => {
    render(<Badge tone="blue">Info</Badge>);
    expect(screen.getByText("Info").className).toContain("blue");
  });

  it("renders a status dot element when dot is true", () => {
    render(<Badge dot>Online</Badge>);
    expect(screen.getByText("Online").firstElementChild).not.toBeNull();
  });

  it("does not render a dot by default", () => {
    render(<Badge>Offline</Badge>);
    expect(screen.getByText("Offline").firstElementChild).toBeNull();
  });

  it("forwards extra props and className", () => {
    render(
      <Badge className="extra" data-testid="b">
        X
      </Badge>,
    );
    expect(screen.getByTestId("b").className).toContain("extra");
  });
});
