import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DndContext } from "@dnd-kit/core";
import { SortableContext } from "@dnd-kit/sortable";
import { SortableAgent } from "./sortable-agent";
import type { SortableAgentItem } from "@/features/workflows/types/workflows";

const item: SortableAgentItem = {
  sortId: "s1",
  name: "Analyzer",
  model: "gemma",
  description: "analyzes things",
} as SortableAgentItem;

const renderSortable = (onRemove = vi.fn(), index?: number) =>
  render(
    <DndContext>
      <SortableContext items={[item.sortId]}>
        <ol>
          <SortableAgent item={item} index={index} onRemove={onRemove} />
        </ol>
      </SortableContext>
    </DndContext>,
  );

describe("SortableAgent", () => {
  it("renders the agent name, model and description", () => {
    renderSortable();
    expect(screen.getByText("Analyzer")).toBeInTheDocument();
    expect(screen.getByText("gemma")).toBeInTheDocument();
    expect(screen.getByText("analyzes things")).toBeInTheDocument();
  });

  it("calls onRemove with the sort id when the remove button is clicked", () => {
    const onRemove = vi.fn();
    renderSortable(onRemove);
    const buttons = screen.getAllByRole("button");
    fireEvent.click(buttons[buttons.length - 1]);
    expect(onRemove).toHaveBeenCalledWith("s1");
  });

  it("labels the step from its position and exposes the node as a reorder handle", () => {
    renderSortable(vi.fn(), 2);
    expect(screen.getByText("step 03")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reorder Analyzer" })).toHaveAttribute(
      "aria-roledescription",
      "sortable",
    );
  });
});
