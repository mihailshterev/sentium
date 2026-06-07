import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Models", () => {
  test.beforeEach(async ({ modelsPage }) => {
    await modelsPage.goto();
    await modelsPage.expectLoaded();
  });

  test("renders the Model Management heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Model Management" })).toBeVisible();
  });

  test("shows the Installed Models panel", { tag: "@smoke" }, async ({ modelsPage }) => {
    await modelsPage.expectInstalledPanelVisible();
  });

  test("shows the Download Model panel", { tag: "@smoke" }, async ({ modelsPage }) => {
    await modelsPage.expectDownloadPanelVisible();
  });

  test("shows no models installed in E2E mode (Ollama excluded)", { tag: "@regression" }, async ({ modelsPage }) => {
    await modelsPage.expectEmptyModelList();
  });

  test("shows the refresh button", async ({ page }) => {
    await expect(page.getByTitle("Refresh model list")).toBeVisible();
  });
});
