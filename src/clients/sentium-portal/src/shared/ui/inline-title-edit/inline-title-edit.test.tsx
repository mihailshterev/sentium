import { describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { InlineTitleEdit } from "./inline-title-edit";

const setup = (value = "Old title") => {
  const onSave = vi.fn();
  const onCancel = vi.fn();
  render(<InlineTitleEdit value={value} ariaLabel="Rename" onSave={onSave} onCancel={onCancel} />);
  return { input: screen.getByLabelText("Rename") as HTMLInputElement, onSave, onCancel };
};

describe("InlineTitleEdit", () => {
  it("focuses the field with the current value", () => {
    const { input } = setup();
    expect(input).toHaveValue("Old title");
    expect(input).toHaveFocus();
  });

  it("saves the trimmed value on Enter", () => {
    const { input, onSave, onCancel } = setup();
    fireEvent.change(input, { target: { value: "  New title  " } });
    fireEvent.keyDown(input, { key: "Enter" });
    expect(onSave).toHaveBeenCalledWith("New title");
    expect(onCancel).not.toHaveBeenCalled();
  });

  it("saves on blur", () => {
    const { input, onSave } = setup();
    fireEvent.change(input, { target: { value: "Blurred" } });
    fireEvent.blur(input);
    expect(onSave).toHaveBeenCalledWith("Blurred");
  });

  it("cancels on Escape without saving", () => {
    const { input, onSave, onCancel } = setup();
    fireEvent.change(input, { target: { value: "Discarded" } });
    fireEvent.keyDown(input, { key: "Escape" });
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onSave).not.toHaveBeenCalled();
  });

  it("treats a blank or unchanged value as a cancel", () => {
    const blank = setup();
    fireEvent.change(blank.input, { target: { value: "   " } });
    fireEvent.keyDown(blank.input, { key: "Enter" });
    expect(blank.onSave).not.toHaveBeenCalled();
    expect(blank.onCancel).toHaveBeenCalled();
  });

  it("settles only once when Enter is followed by blur", () => {
    const { input, onSave } = setup();
    fireEvent.change(input, { target: { value: "Once" } });
    fireEvent.keyDown(input, { key: "Enter" });
    fireEvent.blur(input);
    expect(onSave).toHaveBeenCalledTimes(1);
  });

  it("keeps clicks from reaching the surrounding row", () => {
    const onRowClick = vi.fn();
    render(
      <div onClick={onRowClick}>
        <InlineTitleEdit value="x" ariaLabel="Rename row" onSave={vi.fn()} onCancel={vi.fn()} />
      </div>,
    );
    fireEvent.click(screen.getByLabelText("Rename row"));
    expect(onRowClick).not.toHaveBeenCalled();
  });
});
