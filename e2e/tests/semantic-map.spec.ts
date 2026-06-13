import { test, expect } from "../fixtures/auth.fixture";

// Quarantined in CI: this spec drives a WebGL canvas + D3 force-simulation
// visualization. Under software-rendered headless WebGL the page intermittently
// fails to fully initialize (a blank/stuck render - a different assertion times
// out each run, consistent with WebGL context exhaustion across repeated
// navigations). The page's data/demo logic is covered by unit tests. Re-enable
// (swap .skip back to .serial) when running against a GPU-backed browser.
test.describe.skip("Semantic Map", () => {
  test.beforeEach(async ({ semanticMapPage }) => {
    test.slow();
    await semanticMapPage.goto();
    await semanticMapPage.expectLoaded();
  });

  test("renders the canvas element", { tag: "@smoke" }, async ({ semanticMapPage }) => {
    await semanticMapPage.expectCanvasVisible();
  });

  test("shows the search input", { tag: "@smoke" }, async ({ semanticMapPage }) => {
    await semanticMapPage.expectSearchInputVisible();
  });

  test("shows demo mode when no vector data exists", { tag: "@regression" }, async ({ semanticMapPage }) => {
    await semanticMapPage.expectDemoModeVisible();
  });

  test("search input accepts text", { tag: "@regression" }, async ({ page }) => {
    const input = page.getByPlaceholder(/search/i);
    await input.fill("test query");
    await expect(input).toHaveValue("test query");
  });

  test("canvas remains visible after load", async ({ page }) => {
    await expect(page.locator("canvas")).toBeVisible();
  });
});
