import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { act } from "@testing-library/react";
import { useThemeStore, resolveTheme, applyTheme, observeThemeChanges } from "./theme-store";
import { DEFAULT_PALETTE } from "./palettes";

beforeEach(() => {
  act(() => useThemeStore.setState({ preference: "system", palette: DEFAULT_PALETTE }));
  document.documentElement.removeAttribute("data-theme");
  document.documentElement.removeAttribute("data-palette");
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("resolveTheme", () => {
  it("returns the explicit preference for 'dark'", () => {
    expect(resolveTheme("dark")).toBe("dark");
  });

  it("returns the explicit preference for 'light'", () => {
    expect(resolveTheme("light")).toBe("light");
  });

  it("resolves 'system' to 'light' when the OS does not prefer dark", () => {
    expect(resolveTheme("system")).toBe("light");
  });

  it("resolves 'system' to 'dark' when the OS prefers dark", () => {
    vi.spyOn(window, "matchMedia").mockReturnValue({
      matches: true,
    } as unknown as MediaQueryList);
    expect(resolveTheme("system")).toBe("dark");
  });
});

describe("applyTheme", () => {
  it("writes the resolved theme to the document root", () => {
    applyTheme("dark");
    expect(document.documentElement.getAttribute("data-theme")).toBe("dark");
  });
});

describe("useThemeStore", () => {
  it("defaults to the 'system' preference", () => {
    expect(useThemeStore.getState().preference).toBe("system");
  });

  it("setPreference stores the preference and applies the theme", () => {
    act(() => useThemeStore.getState().setPreference("dark"));
    expect(useThemeStore.getState().preference).toBe("dark");
    expect(document.documentElement.getAttribute("data-theme")).toBe("dark");
  });

  it("toggle flips the resolved theme (system→light resolves, toggles to dark)", () => {
    act(() => useThemeStore.getState().toggle());
    expect(useThemeStore.getState().preference).toBe("dark");
    expect(document.documentElement.getAttribute("data-theme")).toBe("dark");
  });

  it("toggle from dark goes back to light", () => {
    act(() => useThemeStore.getState().setPreference("dark"));
    act(() => useThemeStore.getState().toggle());
    expect(useThemeStore.getState().preference).toBe("light");
    expect(document.documentElement.getAttribute("data-theme")).toBe("light");
  });

  it("defaults to the warm palette", () => {
    expect(useThemeStore.getState().palette).toBe("warm");
  });

  it("setPalette stores the palette and applies it to the document root", () => {
    act(() => useThemeStore.getState().setPalette("forest"));
    expect(useThemeStore.getState().palette).toBe("forest");
    expect(document.documentElement.getAttribute("data-palette")).toBe("forest");
  });

  it("toggling the mode keeps the palette", () => {
    act(() => useThemeStore.getState().setPalette("ember"));
    act(() => useThemeStore.getState().toggle());
    expect(useThemeStore.getState().palette).toBe("ember");
  });
});

describe("observeThemeChanges", () => {
  it("fires when the mode or the palette changes, until unsubscribed", async () => {
    const onChange = vi.fn();
    const stop = observeThemeChanges(onChange);

    applyTheme("dark");
    await vi.waitFor(() => expect(onChange).toHaveBeenCalledTimes(1));
    act(() => useThemeStore.getState().setPalette("cosmos"));
    await vi.waitFor(() => expect(onChange).toHaveBeenCalledTimes(2));

    stop();
    applyTheme("light");
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(onChange).toHaveBeenCalledTimes(2);
  });
});
