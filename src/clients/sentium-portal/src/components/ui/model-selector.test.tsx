import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ModelSelector from "./model-selector";

describe("ModelSelector", () => {
  it("renders a select with alphabetically sorted options when models exist", () => {
    render(<ModelSelector className="x" models={["qwen", "gemma"]} value="gemma" onChange={() => {}} />);
    const options = screen.getAllByRole("option").map((o) => o.textContent);
    expect(options).toEqual(["gemma", "qwen"]);
  });

  it("calls onChange when a different option is selected", async () => {
    const onChange = vi.fn();
    render(<ModelSelector className="x" models={["qwen", "gemma"]} value="gemma" onChange={onChange} />);
    await userEvent.selectOptions(screen.getByRole("combobox"), "qwen");
    expect(onChange).toHaveBeenCalledWith("qwen");
  });

  it("falls back to a free-text input when there are no models", () => {
    render(<ModelSelector className="x" models={[]} value="" onChange={() => {}} placeholder="type a model" />);
    expect(screen.getByPlaceholderText("type a model")).toBeInTheDocument();
  });

  it("calls onChange as the user types in free-text mode", async () => {
    const onChange = vi.fn();
    render(<ModelSelector className="x" models={[]} value="" onChange={onChange} />);
    await userEvent.type(screen.getByRole("textbox"), "a");
    expect(onChange).toHaveBeenCalledWith("a");
  });
});
