import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { ToolPicker } from "./tool-picker";
import type { ToolDescriptor } from "../types/agents";

const tool = (name: string, resource: string, extra: Partial<ToolDescriptor> = {}): ToolDescriptor => ({
  name,
  description: `${name} does things. More detail.`,
  resource,
  action: "Read",
  risk: "Low",
  requiresApproval: false,
  ...extra,
});

const TOOLS = [
  tool("knowledge_base_search", "VectorDb"),
  tool("recall_learnings", "VectorDb"),
  tool("execute_code_sandbox", "Code", { requiresApproval: true, action: "Execute" }),
  tool("find_assets", "Inventory"),
];

describe("ToolPicker", () => {
  it("defaults to every tool and switches to an explicit list holding all of them", () => {
    const onChange = vi.fn();
    render(<ToolPicker tools={TOOLS} value={null} onChange={onChange} />);

    expect(screen.getByRole("button", { name: "All tools" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText(/including ones added later/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Selected" }));
    expect(onChange).toHaveBeenCalledWith(TOOLS.map((t) => t.name));
  });

  it("groups tools, marks approval-gated ones and toggles in catalog order", () => {
    const onChange = vi.fn();
    render(<ToolPicker tools={TOOLS} value={["find_assets"]} onChange={onChange} />);

    expect(screen.getByText("1 of 4")).toBeInTheDocument();
    const code = screen.getByRole("group", { name: "code & scheduling" });
    expect(within(code).getByText("approval")).toBeInTheDocument();
    expect(screen.getByText("knowledge_base_search does things.")).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText(/knowledge_base_search/));
    expect(onChange).toHaveBeenLastCalledWith(["knowledge_base_search", "find_assets"]);

    fireEvent.click(screen.getByLabelText(/find_assets/));
    expect(onChange).toHaveBeenLastCalledWith([]);
  });

  it("selects and clears a whole group", () => {
    const onChange = vi.fn();
    const { rerender } = render(<ToolPicker tools={TOOLS} value={[]} onChange={onChange} />);

    const knowledge = screen.getByRole("group", { name: "knowledge" });
    fireEvent.click(within(knowledge).getByRole("button", { name: "select all" }));
    expect(onChange).toHaveBeenLastCalledWith(["knowledge_base_search", "recall_learnings"]);

    rerender(<ToolPicker tools={TOOLS} value={["knowledge_base_search", "recall_learnings"]} onChange={onChange} />);
    fireEvent.click(within(screen.getByRole("group", { name: "knowledge" })).getByRole("button", { name: "clear" }));
    expect(onChange).toHaveBeenLastCalledWith([]);
  });

  it("never folds MCP tools into 'all tools' and groups them by server", () => {
    const onChange = vi.fn();
    const withMcp = [
      ...TOOLS,
      tool("ha__get_state", "External", { server: "Home Assistant", action: "Execute", requiresApproval: true }),
    ];
    const { rerender } = render(<ToolPicker tools={withMcp} value={null} onChange={onChange} />);

    expect(screen.getByText("all 4")).toBeInTheDocument();
    expect(screen.getByText(/MCP tools are never included automatically/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Selected" }));
    expect(onChange).toHaveBeenLastCalledWith(TOOLS.map((t) => t.name));

    rerender(<ToolPicker tools={withMcp} value={[]} onChange={onChange} />);
    const server = screen.getByRole("group", { name: "mcp · Home Assistant" });
    expect(within(server).getByText("mcp")).toBeInTheDocument();
    fireEvent.click(within(server).getByLabelText(/ha__get_state/));
    expect(onChange).toHaveBeenLastCalledWith(["ha__get_state"]);
  });

  it("explains an empty selection", () => {
    render(<ToolPicker tools={TOOLS} value={[]} onChange={vi.fn()} />);
    expect(screen.getByText(/No tools: the agent can only answer/)).toBeInTheDocument();
  });
});
