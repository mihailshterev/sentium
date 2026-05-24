import { test, expect } from "../fixtures/auth.fixture";

const AGENT_NAME = `e2e-agent-${Date.now()}`;
const AGENT_DESC = "Created by Playwright E2E suite";
const UPDATED_DESC = "Updated by Playwright E2E suite";

test.describe.serial("Agent Registry", () => {
  test.beforeEach(async ({ agentsPage }) => {
    await agentsPage.goto();
    await agentsPage.expectLoaded();
  });

  test("renders the Agent Registry heading", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Agent Registry" })).toBeVisible();
  });

  test("shows the register agent form", async ({ page }) => {
    await expect(page.getByLabel("Agent Name")).toBeVisible();
    await expect(page.getByLabel("Description")).toBeVisible();
  });

  test("creates a new agent", async ({ agentsPage }) => {
    await agentsPage.fillAgentName(AGENT_NAME);
    await agentsPage.fillAgentDescription(AGENT_DESC);
    await agentsPage.submitCreate();

    await agentsPage.expectAgentVisible(AGENT_NAME);
  });

  test("shows validation error when name is empty", async ({ page }) => {
    await page.locator("form").evaluate((form: HTMLFormElement) => form.requestSubmit());
    await expect(page.getByText(/name is required/i)).toBeVisible();
  });

  test("edits an existing agent's description", async ({ agentsPage }) => {
    await agentsPage.clickEditOnAgent(AGENT_NAME);
    await agentsPage.clearAndFillDescriptionInEdit(UPDATED_DESC);
    await agentsPage.submitEdit();

    await agentsPage.expectAgentVisible(AGENT_NAME);
  });

  test("deletes an agent", async ({ agentsPage, page }) => {
    page.on("dialog", (dialog) => dialog.accept());
    await agentsPage.clickDeleteOnAgent(AGENT_NAME);
    await agentsPage.expectAgentNotVisible(AGENT_NAME);
  });
});
