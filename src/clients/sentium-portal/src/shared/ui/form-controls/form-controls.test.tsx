import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Input, SearchInput, Select, Switch, SwitchRow, Textarea } from "./form-controls";

describe("Input / Textarea", () => {
  it("marks invalid controls with aria-invalid", () => {
    render(
      <>
        <Input aria-label="Name" invalid />
        <Textarea aria-label="Notes" invalid />
      </>,
    );
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByRole("textbox", { name: "Notes" })).toHaveAttribute("aria-invalid", "true");
  });

  it("forwards refs (react-hook-form register)", () => {
    const ref = { current: null as HTMLInputElement | null };
    render(<Input aria-label="Name" ref={ref} />);
    expect(ref.current).toBeInstanceOf(HTMLInputElement);
  });
});

describe("Select", () => {
  it("renders a native combobox and reports changes", async () => {
    const onChange = vi.fn();
    render(
      <Select aria-label="Model" value="a" onChange={(e) => onChange(e.target.value)}>
        <option value="a">A</option>
        <option value="b">B</option>
      </Select>,
    );
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Model" }), "b");
    expect(onChange).toHaveBeenCalledWith("b");
  });
});

describe("SearchInput", () => {
  it("renders a searchbox", () => {
    render(<SearchInput placeholder="Search agents" />);
    expect(screen.getByRole("searchbox")).toHaveAttribute("placeholder", "Search agents");
  });
});

describe("Switch", () => {
  it("is a checkbox input exposed as a switch", async () => {
    const onChange = vi.fn();
    render(<Switch aria-label="Lockdown" onChange={onChange} />);
    const control = screen.getByRole("switch", { name: "Lockdown" });
    expect(control).toHaveAttribute("type", "checkbox");
    await userEvent.click(control);
    expect(onChange).toHaveBeenCalled();
  });
});

describe("SwitchRow", () => {
  it("labels and describes the switch, and toggles from the label text", async () => {
    const onChange = vi.fn();
    render(<SwitchRow label="Enable harness" description="Adds governance rules" onChange={onChange} />);
    const control = screen.getByRole("switch", { name: "Enable harness" });
    expect(control).toHaveAccessibleDescription("Adds governance rules");
    await userEvent.click(screen.getByText("Enable harness"));
    expect(onChange).toHaveBeenCalled();
  });

  it("keeps the checkbox inside the label text's grandparent (e2e page objects rely on it)", () => {
    render(<SwitchRow label="Enable harness" />);
    const row = screen.getByText("Enable harness").parentElement!.parentElement!;
    expect(row.querySelector("input[type=checkbox]")).not.toBeNull();
  });
});
