import { describe, expect, it } from "vitest";
import { buildBreadcrumbs } from "./build-breadcrumbs";

describe("buildBreadcrumbs", () => {
  it("is just Home on the home page", () => {
    expect(buildBreadcrumbs("/")).toEqual([{ label: "Home" }]);
  });

  it("shows the group and the current page without linking either", () => {
    expect(buildBreadcrumbs("/agents")).toEqual([
      { label: "Home", to: "/" },
      { label: "Build" },
      { label: "Agents", to: undefined },
    ]);
  });

  it("links the parent page from a detail page", () => {
    expect(buildBreadcrumbs("/workflows/0b6f9c1e-1234")).toEqual([
      { label: "Home", to: "/" },
      { label: "Build" },
      { label: "Workflows", to: "/workflows" },
      { label: "Workflow" },
    ]);
  });

  it("names nested detail routes", () => {
    expect(buildBreadcrumbs("/orchestration/runs/abc").at(-1)).toEqual({ label: "Run" });
    expect(buildBreadcrumbs("/evaluations/suites/abc").at(-1)).toEqual({ label: "Eval suite" });
    expect(buildBreadcrumbs("/workflows/new").at(-1)).toEqual({ label: "New workflow" });
  });

  it("names account pages, which have no group", () => {
    expect(buildBreadcrumbs("/settings/appearance")).toEqual([
      { label: "Home", to: "/" },
      { label: "Settings", to: "/settings" },
      { label: "Appearance" },
    ]);
  });

  it("ignores a trailing slash", () => {
    expect(buildBreadcrumbs("/agents/")).toEqual(buildBreadcrumbs("/agents"));
  });

  it("falls back to Home for unknown paths", () => {
    expect(buildBreadcrumbs("/nowhere")).toEqual([{ label: "Home", to: "/" }]);
  });
});
