import { describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { Select } from "./select";

function renderSelect(onChange = vi.fn(), onDocumentKey = vi.fn()) {
  document.addEventListener("keydown", onDocumentKey);
  render(
    <Select aria-label="Priority" defaultValue="low" onChange={(e) => onChange(e.target.value)}>
      <option value="low">Low</option>
      <option value="high">High</option>
      <option value="off" disabled>
        Off
      </option>
      <option value="urgent">Urgent</option>
    </Select>,
  );
  return { select: screen.getByRole("combobox", { name: "Priority" }) as HTMLSelectElement, onChange };
}

const menuOption = (label: string) => screen.getByText(label, { selector: "span" });

describe("Select menu", () => {
  it("opens a styled menu on mouse down and selects through the native select", () => {
    const { select, onChange } = renderSelect();
    fireEvent.mouseDown(select);
    expect(select).toHaveAttribute("aria-expanded", "true");

    fireEvent.click(menuOption("Urgent"));

    expect(onChange).toHaveBeenCalledWith("urgent");
    expect(select.value).toBe("urgent");
    expect(select).toHaveAttribute("aria-expanded", "false");
  });

  it("ignores disabled options", () => {
    const { select, onChange } = renderSelect();
    fireEvent.mouseDown(select);
    fireEvent.click(menuOption("Off"));
    expect(onChange).not.toHaveBeenCalled();
  });

  it("navigates with the keyboard, skipping disabled options", () => {
    const { select, onChange } = renderSelect();
    fireEvent.keyDown(select, { key: "Enter" });
    fireEvent.keyDown(select, { key: "ArrowDown" });
    fireEvent.keyDown(select, { key: "ArrowDown" });
    fireEvent.keyDown(select, { key: "Enter" });
    expect(onChange).toHaveBeenCalledWith("urgent");
  });

  it("closes on Escape without letting it reach an enclosing dialog", () => {
    const onDocumentKey = vi.fn();
    const { select } = renderSelect(vi.fn(), onDocumentKey);
    fireEvent.keyDown(select, { key: " " });
    fireEvent.keyDown(select, { key: "Escape" });
    expect(select).toHaveAttribute("aria-expanded", "false");
    expect(onDocumentKey).toHaveBeenCalledTimes(1);
    document.removeEventListener("keydown", onDocumentKey);
  });

  it("closes when the select loses focus", () => {
    const { select } = renderSelect();
    fireEvent.mouseDown(select);
    fireEvent.blur(select);
    expect(select).toHaveAttribute("aria-expanded", "false");
  });
});
