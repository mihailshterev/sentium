import { describe, it, expect, beforeEach } from "vitest";
import { useUiStore } from "./ui-store";

describe("ui-store", () => {
  beforeEach(() => useUiStore.setState({ sidebarCollapsed: false, collapsedNavGroups: [] }));

  it("folds and unfolds a navigation group", () => {
    useUiStore.getState().toggleNavGroup("build");
    expect(useUiStore.getState().collapsedNavGroups).toEqual(["build"]);
    useUiStore.getState().toggleNavGroup("build");
    expect(useUiStore.getState().collapsedNavGroups).toEqual([]);
  });

  it("starts expanded", () => {
    expect(useUiStore.getState().sidebarCollapsed).toBe(false);
  });

  it("toggles the sidebar", () => {
    useUiStore.getState().toggleSidebar();
    expect(useUiStore.getState().sidebarCollapsed).toBe(true);
    useUiStore.getState().toggleSidebar();
    expect(useUiStore.getState().sidebarCollapsed).toBe(false);
  });

  it("sets the collapsed state explicitly", () => {
    useUiStore.getState().setSidebarCollapsed(true);
    expect(useUiStore.getState().sidebarCollapsed).toBe(true);
  });
});
