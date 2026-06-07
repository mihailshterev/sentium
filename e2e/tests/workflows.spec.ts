import { test, expect } from "../fixtures/auth.fixture";

const WORKFLOW_NAME = `e2e-workflow-${Date.now()}`;
const WORKFLOW_DESC = "E2E test workflow";
const UPDATED_NAME = `${WORKFLOW_NAME}-updated`;

test.describe.serial("Workflows", () => {
  test.beforeEach(async ({ workflowsPage }) => {
    await workflowsPage.goto();
    await workflowsPage.expectLoaded();
  });

  test("renders the workflows heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: /workflow/i })).toBeVisible();
  });

  test("shows the seeded baseline workflow", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByText("e2e-baseline-workflow")).toBeVisible();
  });

  test("shows the New Workflow button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /new workflow/i }).first()).toBeVisible();
  });

  test("creates a new workflow", { tag: "@regression" }, async ({ workflowsPage }) => {
    await workflowsPage.openCreateEditor();
    await workflowsPage.fillWorkflowName(WORKFLOW_NAME);
    await workflowsPage.fillWorkflowDescription(WORKFLOW_DESC);
    await workflowsPage.submitSave();

    await workflowsPage.expectWorkflowVisible(WORKFLOW_NAME);
  });

  test("edits an existing workflow", { tag: "@regression" }, async ({ workflowsPage }) => {
    await workflowsPage.clickEditOnWorkflow(WORKFLOW_NAME);
    await workflowsPage.expectEditorOpen();
    await workflowsPage.clearAndFillWorkflowName(UPDATED_NAME);
    await workflowsPage.submitSave();

    await workflowsPage.expectWorkflowVisible(UPDATED_NAME);
  });

  test("deletes a workflow", { tag: "@regression" }, async ({ workflowsPage }) => {
    await workflowsPage.clickDeleteOnWorkflow(UPDATED_NAME);
    await workflowsPage.confirmDelete();
    await workflowsPage.expectWorkflowNotVisible(UPDATED_NAME);
  });
});
