import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import WorkflowCard from "./workflow-card";
import type { WorkflowRecord } from "../../../types/workflows";

const workflow = (overrides: Partial<WorkflowRecord> = {}): WorkflowRecord =>
  ({
    id: "wf1",
    name: "Pipeline",
    description: "does things",
    agents: [{ id: "a1" }, { id: "a2" }],
    ...overrides,
  }) as WorkflowRecord;

describe("WorkflowCard", () => {
  it("renders the name, description and agent count", () => {
    render(
      <WorkflowCard workflow={workflow()} isActive={false} onSelect={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    );
    expect(screen.getByText("Pipeline")).toBeInTheDocument();
    expect(screen.getByText("does things")).toBeInTheDocument();
    expect(screen.getByText("2 agents")).toBeInTheDocument();
  });

  it("falls back to 'No description' and singular agent label", () => {
    render(
      <WorkflowCard
        workflow={workflow({ description: "", agents: [{ id: "a1" }] as never })}
        isActive
        onSelect={vi.fn()}
        onEdit={vi.fn()}
        onDelete={vi.fn()}
      />,
    );
    expect(screen.getByText("No description")).toBeInTheDocument();
    expect(screen.getByText("1 agent")).toBeInTheDocument();
  });

  it("selects on card click", () => {
    const onSelect = vi.fn();
    render(
      <WorkflowCard workflow={workflow()} isActive={false} onSelect={onSelect} onEdit={vi.fn()} onDelete={vi.fn()} />,
    );
    fireEvent.click(screen.getByText("Pipeline"));
    expect(onSelect).toHaveBeenCalled();
  });

  it("edits and deletes without triggering select", () => {
    const onSelect = vi.fn();
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    render(
      <WorkflowCard workflow={workflow()} isActive={false} onSelect={onSelect} onEdit={onEdit} onDelete={onDelete} />,
    );
    fireEvent.click(screen.getByTestId("workflow-edit-Pipeline"));
    expect(onEdit).toHaveBeenCalled();
    fireEvent.click(screen.getByTestId("workflow-delete-Pipeline"));
    expect(onDelete).toHaveBeenCalledWith("wf1");
    expect(onSelect).not.toHaveBeenCalled();
  });
});
