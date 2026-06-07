import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Orchestration", () => {
  test.beforeEach(async ({ orchestrationPage }) => {
    await orchestrationPage.goto();
    await orchestrationPage.expectLoaded();
  });

  test("renders the Orchestration heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Orchestration" })).toBeVisible();
  });

  test("shows the log output panel", { tag: "@smoke" }, async ({ orchestrationPage }) => {
    await orchestrationPage.expectLogPanelVisible();
  });

  test("shows the seeded baseline workflow in the list", { tag: "@smoke" }, async ({ orchestrationPage }) => {
    await orchestrationPage.expectWorkflowInList("e2e-baseline-workflow");
  });

  test("can switch to history view", { tag: "@regression" }, async ({ orchestrationPage }) => {
    await orchestrationPage.switchToHistory();
    await orchestrationPage.expectHistoryPanelVisible();
  });

  test("shows phase steps in the header", async ({ page }) => {
    await expect(page.getByText(/plan|execute|validate/i).first()).toBeVisible();
  });
});
