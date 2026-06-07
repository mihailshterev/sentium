import { describe, it, expect, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { act } from "@testing-library/react";
import ThemeToggle from "./theme-toggle";
import { useThemeStore } from "../../stores/theme-store";

beforeEach(() => {
  act(() => useThemeStore.setState({ preference: "light" }));
});

describe("ThemeToggle", () => {
  it("labels the control to switch to dark when currently light", () => {
    render(<ThemeToggle />);
    expect(screen.getByRole("button", { name: "Switch to dark theme" })).toBeInTheDocument();
  });

  it("labels the control to switch to light when currently dark", () => {
    act(() => useThemeStore.setState({ preference: "dark" }));
    render(<ThemeToggle />);
    expect(screen.getByRole("button", { name: "Switch to light theme" })).toBeInTheDocument();
  });

  it("toggles the theme preference on click", async () => {
    render(<ThemeToggle />);
    await userEvent.click(screen.getByRole("button"));
    expect(useThemeStore.getState().preference).toBe("dark");
  });
});
