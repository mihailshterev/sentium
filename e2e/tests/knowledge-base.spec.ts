import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Knowledge Base", () => {
  test.beforeEach(async ({ knowledgeBasePage }) => {
    await knowledgeBasePage.goto();
    await knowledgeBasePage.expectLoaded();
  });

  test("renders the Knowledge Base heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Knowledge Base" })).toBeVisible();
  });

  test("shows the Global Context tab", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByTestId("tab-global-context")).toBeVisible();
  });

  test("shows the Agent Learnings tab", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByTestId("tab-agent-learnings")).toBeVisible();
  });

  test("Global Context tab shows collection stats", { tag: "@regression" }, async ({ knowledgeBasePage }) => {
    await knowledgeBasePage.switchToGlobalContext();
    await knowledgeBasePage.expectCollectionStatsVisible();
  });

  test("Agent Learnings tab shows seeded learnings", { tag: "@regression" }, async ({ knowledgeBasePage, page }) => {
    await knowledgeBasePage.switchToAgentLearnings();
    await expect(page.getByText("Captured Learnings", { exact: true })).toBeVisible();
    await expect(page.getByText(/e2e baseline learning/i).first()).toBeVisible();
  });
});
