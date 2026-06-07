import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import RunScript from "./run-script";
import type { SandboxExecutionLog } from "../../../types/sandbox";

const entry = (overrides: Partial<SandboxExecutionLog> = {}): SandboxExecutionLog =>
  ({ code: "print('hi')", fileContext: [], ...overrides }) as SandboxExecutionLog;

describe("RunScript", () => {
  it("renders the source code", () => {
    render(<RunScript entry={entry()} />);
    expect(screen.getByText("print('hi')")).toBeInTheDocument();
  });

  it("renders the file-context section when files are present", () => {
    render(
      <RunScript
        entry={entry({
          fileContext: [{ fileName: "data.csv", content: "a,b,c" }] as never,
        })}
      />,
    );
    expect(screen.getByText(/file context/i)).toBeInTheDocument();
    expect(screen.getByText("data.csv")).toBeInTheDocument();
    expect(screen.getByText("a,b,c")).toBeInTheDocument();
  });
});
