import { test, expect } from "../fixtures";

test.describe("Models", () => {
  test.beforeEach(async ({ modelsPage }) => {
    await modelsPage.goto();
    await modelsPage.expectLoaded();
  });

  test("renders the Model Management heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Model Management" })).toBeVisible();
  });

  test("shows the Chat Models panel", { tag: "@smoke" }, async ({ modelsPage }) => {
    await modelsPage.expectChatModelsPanelVisible();
  });

  test("explains how models are added", { tag: "@smoke" }, async ({ modelsPage }) => {
    await modelsPage.expectAddingModelsPanelVisible();
  });

  test(
    "shows no models in E2E mode (the inference stub declares none)",
    { tag: "@regression" },
    async ({ modelsPage }) => {
      await modelsPage.expectEmptyModelList();
    },
  );

  test("shows the refresh button", async ({ page }) => {
    await expect(page.getByTitle("Refresh model list")).toBeVisible();
  });
});
