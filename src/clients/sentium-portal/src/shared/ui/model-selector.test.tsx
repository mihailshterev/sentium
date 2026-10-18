import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ModelSelector from "./model-selector";

describe("ModelSelector - field variant (default)", () => {
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

describe("ModelSelector - chip variant", () => {
  it("shows the current value on the trigger button", () => {
    render(<ModelSelector className="x" models={["qwen", "gemma"]} value="gemma" onChange={() => {}} variant="chip" />);
    expect(screen.getByRole("button", { name: /gemma/i })).toBeInTheDocument();
  });

  it("opens a dropdown with alphabetically sorted options on click", async () => {
    render(<ModelSelector className="x" models={["qwen", "gemma"]} value="gemma" onChange={() => {}} variant="chip" />);
    await userEvent.click(screen.getByRole("button", { name: /gemma/i }));
    const options = screen
      .getAllByRole("button")
      .slice(1)
      .map((b) => b.textContent?.trim());
    expect(options[0]).toContain("gemma");
    expect(options[1]).toContain("qwen");
  });

  it("calls onChange and closes the dropdown when an option is clicked", async () => {
    const onChange = vi.fn();
    render(<ModelSelector className="x" models={["qwen", "gemma"]} value="gemma" onChange={onChange} variant="chip" />);
    await userEvent.click(screen.getByRole("button", { name: /gemma/i }));
    await userEvent.click(screen.getByRole("button", { name: /qwen/i }));
    expect(onChange).toHaveBeenCalledWith("qwen");
    expect(screen.queryByRole("button", { name: /qwen/i })).not.toBeInTheDocument();
  });

  it("renders a text input when there are no models", () => {
    render(
      <ModelSelector
        className="x"
        models={[]}
        value=""
        onChange={() => {}}
        placeholder="type a model"
        variant="chip"
      />,
    );
    expect(screen.getByPlaceholderText("type a model")).toBeInTheDocument();
  });

  it("calls onChange as the user types in free-text mode", async () => {
    const onChange = vi.fn();
    render(<ModelSelector className="x" models={[]} value="" onChange={onChange} variant="chip" />);
    await userEvent.type(screen.getByRole("textbox"), "a");
    expect(onChange).toHaveBeenCalledWith("a");
  });
});
