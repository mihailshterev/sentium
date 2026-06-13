import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import FormField from "./form-field";

describe("FormField", () => {
  it("renders the label and its child control", () => {
    render(
      <FormField id="name" label="Name">
        <input id="name" />
      </FormField>,
    );
    expect(screen.getByText("Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Name")).toBeInTheDocument();
  });

  it("associates the label with the control via htmlFor/id", () => {
    render(
      <FormField id="email" label="Email">
        <input id="email" />
      </FormField>,
    );
    const label = screen.getByText("Email");
    expect(label).toHaveAttribute("for", "email");
  });

  it("renders the char count when provided", () => {
    render(
      <FormField label="Bio" charCount={{ current: 10, max: 100 }}>
        <textarea />
      </FormField>,
    );
    expect(screen.getByText("10/100")).toBeInTheDocument();
  });

  it("omits the char count when not provided", () => {
    render(
      <FormField label="Bio">
        <textarea />
      </FormField>,
    );
    expect(screen.queryByText(/\/\d+/)).not.toBeInTheDocument();
  });
});
