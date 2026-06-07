import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DndContext } from "@dnd-kit/core";
import { SortableContext } from "@dnd-kit/sortable";
import SortableAgent from "./sortable-agent";
import type { SortableAgentItem } from "../../../types/workflows";

const item: SortableAgentItem = {
  sortId: "s1",
  name: "Analyzer",
  model: "gemma",
  description: "analyzes things",
} as SortableAgentItem;

const renderSortable = (onRemove = vi.fn()) =>
  render(
    <DndContext>
      <SortableContext items={[item.sortId]}>
        <SortableAgent item={item} onRemove={onRemove} />
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
});
