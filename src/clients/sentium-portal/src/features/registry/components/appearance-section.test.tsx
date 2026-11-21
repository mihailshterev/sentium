import { describe, it, expect, beforeEach } from "vitest";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AppearanceSection } from "./appearance-section";
import { PALETTES } from "@/shared/ui/state/palettes";
import { useThemeStore } from "@/shared/ui/state/theme-store";

beforeEach(() => {
  act(() => useThemeStore.setState({ preference: "dark", palette: "warm" }));
});

describe("AppearanceSection", () => {
  it("offers every palette and marks the current one", () => {
    render(<AppearanceSection />);
    const group = screen.getByRole("radiogroup", { name: "Palette" });

    expect(group.querySelectorAll("input[type=radio]")).toHaveLength(PALETTES.length);
    expect(screen.getByRole("radio", { name: /Warm charcoal/ })).toBeChecked();
  });

  it("switches the palette immediately", async () => {
    render(<AppearanceSection />);
    await userEvent.click(screen.getByRole("radio", { name: /Aubergine/ }));

    expect(useThemeStore.getState().palette).toBe("aubergine");
    expect(document.documentElement.getAttribute("data-palette")).toBe("aubergine");
  });

  it("switches the mode", async () => {
    render(<AppearanceSection />);
    expect(screen.getByRole("radio", { name: "Dark" })).toBeChecked();

    await userEvent.click(screen.getByRole("radio", { name: "System" }));
    expect(useThemeStore.getState().preference).toBe("system");
  });
});
