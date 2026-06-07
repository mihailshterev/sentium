import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import StatusMessage from "./status-message";

describe("StatusMessage", () => {
  it("renders the message text", () => {
    render(<StatusMessage variant="success" message="Saved!" />);
    expect(screen.getByText("Saved!")).toBeInTheDocument();
  });

  it("renders the success variant", () => {
    render(<StatusMessage variant="success" message="ok" />);
    expect(screen.getByText("ok")).toBeInTheDocument();
  });

  it("renders the error variant", () => {
    render(<StatusMessage variant="error" message="bad" />);
    expect(screen.getByText("bad")).toBeInTheDocument();
  });

  it("renders an optional icon", () => {
    render(<StatusMessage variant="loading" message="..." icon={<span data-testid="icon" />} />);
    expect(screen.getByTestId("icon")).toBeInTheDocument();
  });
});
