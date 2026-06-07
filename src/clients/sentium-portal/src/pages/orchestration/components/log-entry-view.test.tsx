import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import LogEntryView from "./log-entry-view";
import type { LogEntry } from "../../../types/orchestration";

const getRoleClass = () => "roleSystem";

const renderEntry = (log: LogEntry, props: Partial<React.ComponentProps<typeof LogEntryView>> = {}) =>
  render(
    <LogEntryView log={log} entryId="e1" expanded={false} onToggle={vi.fn()} getRoleClass={getRoleClass} {...props} />,
  );

describe("LogEntryView", () => {
  it("renders a passed status row", () => {
    renderEntry({ author: "System", text: "Validation passed", type: "status" } as LogEntry);
    expect(screen.getByText("Validation passed")).toBeInTheDocument();
  });

  it("renders a failed status row", () => {
    renderEntry({ author: "System", text: "Check failed", type: "status" } as LogEntry);
    expect(screen.getByText("Check failed")).toBeInTheDocument();
  });

  it("renders a tool call row", () => {
    renderEntry({ author: "Worker", text: "search(query)", type: "tool" } as LogEntry);
    expect(screen.getByText("search(query)")).toBeInTheDocument();
    expect(screen.getByText("Worker")).toBeInTheDocument();
  });

  it("renders a thought block and toggles it", () => {
    const onToggle = vi.fn();
    renderEntry({ author: "Planner", text: "reasoning", type: "thought" } as LogEntry, {
      expanded: true,
      onToggle,
    });
    expect(screen.getByText("Thinking")).toBeInTheDocument();
    expect(screen.getByText("reasoning")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Thinking"));
    expect(onToggle).toHaveBeenCalledWith("e1");
  });

  it("renders a plain message", () => {
    renderEntry({ author: "Worker", text: "just a message", type: "message" } as LogEntry);
    expect(screen.getByText("just a message")).toBeInTheDocument();
  });

  it("renders an orchestrator plan when the message contains assignments", () => {
    renderEntry({
      author: "Orchestrator",
      text: '[{"agent":"A","task":"do thing"}]',
      type: "message",
    } as LogEntry);
    expect(screen.getByText("A")).toBeInTheDocument();
    expect(screen.getByText("do thing")).toBeInTheDocument();
  });
});
