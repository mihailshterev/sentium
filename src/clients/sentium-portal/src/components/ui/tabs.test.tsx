import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Tabs, { type TabItem } from "./tabs";

const tabs: TabItem[] = [
  { id: "a", label: "Alpha", count: 3 },
  { id: "b", label: "Beta" },
];

describe("Tabs", () => {
  it("renders a tab per item inside a tablist", () => {
    render(<Tabs tabs={tabs} active="a" onChange={() => {}} />);
    expect(screen.getByRole("tablist")).toBeInTheDocument();
    expect(screen.getAllByRole("tab")).toHaveLength(2);
  });

  it("marks the active tab with aria-selected", () => {
    render(<Tabs tabs={tabs} active="a" onChange={() => {}} />);
    expect(screen.getByRole("tab", { name: /Alpha/ })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: /Beta/ })).toHaveAttribute("aria-selected", "false");
  });

  it("renders a count badge when provided", () => {
    render(<Tabs tabs={tabs} active="a" onChange={() => {}} />);
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("calls onChange with the tab id when clicked", async () => {
    const onChange = vi.fn();
    render(<Tabs tabs={tabs} active="a" onChange={onChange} />);
    await userEvent.click(screen.getByRole("tab", { name: /Beta/ }));
    expect(onChange).toHaveBeenCalledWith("b");
  });
});
