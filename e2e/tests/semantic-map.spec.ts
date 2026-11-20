import { test, expect } from "../fixtures";

test.describe("Semantic Map", () => {
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
    await expect(page.getByRole("main").locator("canvas")).toBeVisible();
  });
});
