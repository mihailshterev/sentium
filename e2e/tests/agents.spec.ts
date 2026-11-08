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

  test("overrides a built-in agent's tools and resets them", { tag: "@regression" }, async ({ agentsPage, page }) => {
    const row = agentsPage.builtInRow("ResearchAnalyst");
    await expect(row.getByText("knowledge_base_search")).toBeVisible();

    await test.step("give it every tool", async () => {
      await agentsPage.openBuiltInTools("ResearchAnalyst");
      const dialog = page.getByRole("dialog");
      await dialog.getByRole("button", { name: "All tools" }).click();
      await dialog.getByRole("button", { name: "Save tools" }).click();
      await expect(row.getByText("customized")).toBeVisible();
      await expect(row.getByText("all tools")).toBeVisible();
    });

    await test.step("reset to the code default", async () => {
      await agentsPage.openBuiltInTools("ResearchAnalyst");
      await page.getByRole("dialog").getByRole("button", { name: "Reset to default" }).click();
      await expect(row.getByText("customized")).not.toBeVisible();
      await expect(row.getByText("knowledge_base_search")).toBeVisible();
    });
  });

  test("creates an agent limited to chosen tools", { tag: "@regression" }, async ({ agentsPage, page }) => {
    const name = uniqueName("e2e-tooled");

    await agentsPage.fillAgentName(name);
    await agentsPage.fillModel("gemma-4-e4b");
    await page.getByRole("button", { name: "Selected" }).click();
    // Each cleared group's button turns into "select all".
    const clear = page.getByRole("button", { name: "clear", exact: true });
    while ((await clear.count()) > 0) {
      await clear.first().click();
    }
    await page.getByLabel(/^find_assets/).check();
    await expect(page.getByText("1 of", { exact: false })).toBeVisible();
    await agentsPage.submitCreate();

    const agentRow = page.getByRole("listitem").filter({ hasText: name });
    await expect(agentRow.getByText("1 tool", { exact: true })).toBeVisible();

    await agentsPage.clickDeleteOnAgent(name);
    await agentsPage.confirmDelete();
  });

  test("creates, edits and deletes an agent", { tag: "@regression" }, async ({ agentsPage }) => {
    const name = uniqueName("e2e-agent");

    await test.step("create", async () => {
      await agentsPage.fillAgentName(name);
      await agentsPage.fillAgentDescription("Created by Playwright E2E suite");
      // No models are installed in e2e, so the model selector is free text.
      await agentsPage.fillModel("gemma-4-e4b");
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
