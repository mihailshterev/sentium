import { test, expect } from "../fixtures";
import { uniqueName } from "../support/test-data";

test.describe("Agent Registry", () => {
  test.beforeEach(async ({ agentsPage }) => {
    await agentsPage.goto();
    await agentsPage.expectLoaded();
  });

  test("renders the Agent Registry heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Agent Registry" })).toBeVisible();
  });

  test("shows the seeded baseline agent", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByText("e2e-baseline-agent")).toBeVisible();
  });

  test("shows the register agent form", async ({ page }) => {
    await expect(page.getByLabel("Agent Name")).toBeVisible();
    await expect(page.getByLabel("Description")).toBeVisible();
  });

  test("shows validation error when name is empty", { tag: "@regression" }, async ({ page }) => {
    await page.locator("form").evaluate((form: HTMLFormElement) => form.requestSubmit());
    await expect(page.getByText(/name is required/i)).toBeVisible();
  });

  test("creates, edits and deletes an agent", { tag: "@regression" }, async ({ agentsPage }) => {
    const name = uniqueName("e2e-agent");

    await test.step("create", async () => {
      await agentsPage.fillAgentName(name);
      await agentsPage.fillAgentDescription("Created by Playwright E2E suite");
      // No models are installed in e2e, so the model selector is free text.
      await agentsPage.fillModel("gemma3:1b");
      await agentsPage.submitCreate();
      await agentsPage.expectAgentVisible(name);
    });

    await test.step("edit the description", async () => {
      await agentsPage.clickEditOnAgent(name);
      await agentsPage.clearAndFillDescriptionInEdit("Updated by Playwright E2E suite");
      await agentsPage.submitEdit();
      await agentsPage.expectAgentVisible(name);
    });

    await test.step("delete", async () => {
      await agentsPage.clickDeleteOnAgent(name);
      await agentsPage.confirmDelete();
      await agentsPage.expectAgentNotVisible(name);
    });
  });
});
