import { test, expect } from "../fixtures/auth.fixture";

const AGENT_NAME = `e2e-agent-${Date.now()}`;
const AGENT_DESC = "Created by Playwright E2E suite";
const UPDATED_DESC = "Updated by Playwright E2E suite";

test.describe.serial("Agent Registry", () => {
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

  test("creates a new agent", { tag: "@regression" }, async ({ agentsPage }) => {
    await agentsPage.fillAgentName(AGENT_NAME);
    await agentsPage.fillAgentDescription(AGENT_DESC);
    await agentsPage.fillModel("gemma3:1b");
    await agentsPage.submitCreate();

    await agentsPage.expectAgentVisible(AGENT_NAME);
  });

  test("shows validation error when name is empty", { tag: "@regression" }, async ({ page }) => {
    await page.locator("form").evaluate((form: HTMLFormElement) => form.requestSubmit());
    await expect(page.getByText(/name is required/i)).toBeVisible();
  });

  test("edits an existing agent's description", { tag: "@regression" }, async ({ agentsPage }) => {
    await agentsPage.clickEditOnAgent(AGENT_NAME);
    await agentsPage.clearAndFillDescriptionInEdit(UPDATED_DESC);
    await agentsPage.submitEdit();

    await agentsPage.expectAgentVisible(AGENT_NAME);
  });

  test("deletes an agent", { tag: "@regression" }, async ({ agentsPage }) => {
    await agentsPage.clickDeleteOnAgent(AGENT_NAME);
    await agentsPage.confirmDelete();
    await agentsPage.expectAgentNotVisible(AGENT_NAME);
  });
});
