import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import OrchestratorPlanView, { parseAssignments } from "./orchestrator-plan-view";
import LogEntryView from "./log-entry-view";
import type { LogEntry } from "../../../types/orchestration";

const PLAN_JSON =
  '[{"agent":"DotNet Architect","task":"Design the C# Clean Architecture layers."},' +
  '{"agent":"NodeJS Developer","task":"Design the NodeJS equivalent."},' +
  '{"agent":"Summarizer","task":"Synthesize both into one guide."}]';

describe("parseAssignments", () => {
  it("parses a valid assignments array in order", () => {
    const result = parseAssignments(PLAN_JSON);
    expect(result?.map((a) => a.agent)).toEqual(["DotNet Architect", "NodeJS Developer", "Summarizer"]);
    expect(result?.[0].task).toBe("Design the C# Clean Architecture layers.");
  });

  it("returns null for partial/streaming JSON", () => {
    expect(parseAssignments('[{"agent":"DotNet Arc')).toBeNull();
  });

  it("returns null for non-array or plain text", () => {
    expect(parseAssignments("Here is my plan...")).toBeNull();
    expect(parseAssignments('{"agent":"x","task":"y"}')).toBeNull();
  });

  it("drops entries missing agent or task", () => {
    const result = parseAssignments('[{"agent":"A","task":"t"},{"agent":"B"},{"task":"only"}]');
    expect(result?.map((a) => a.agent)).toEqual(["A"]);
  });
});

describe("OrchestratorPlanView", () => {
  it("renders a card per assignment with agent name and task", () => {
    render(<OrchestratorPlanView assignments={parseAssignments(PLAN_JSON)!} getRoleClass={() => "roleSquad"} />);

    expect(screen.getByText("DotNet Architect")).toBeInTheDocument();
    expect(screen.getByText("NodeJS Developer")).toBeInTheDocument();
    expect(screen.getByText("Synthesize both into one guide.")).toBeInTheDocument();
  });
});

describe("LogEntryView orchestrator plan", () => {
  const renderLog = (log: LogEntry) =>
    render(<LogEntryView log={log} entryId="1" expanded={false} onToggle={vi.fn()} getRoleClass={() => "roleSquad"} />);

  it("renders the Orchestrator plan JSON as cards, not raw JSON", () => {
    const { container } = renderLog({ author: "Orchestrator", text: PLAN_JSON, type: "message" });

    expect(screen.getByText("DotNet Architect")).toBeInTheDocument();
    expect(container.textContent).not.toContain('{"agent"');
  });

  it("falls back to text for a non-orchestrator message", () => {
    const { container } = renderLog({ author: "NodeJS Developer", text: "Plain analysis text.", type: "message" });

    expect(container.textContent).toContain("Plain analysis text.");
  });

  it("falls back to text for an Orchestrator message that is not assignment JSON", () => {
    const { container } = renderLog({
      author: "Orchestrator",
      text: "Thinking out loud, no JSON here.",
      type: "message",
    });

    expect(container.textContent).toContain("Thinking out loud");
  });
});
