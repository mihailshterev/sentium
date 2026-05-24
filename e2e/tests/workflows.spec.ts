import { test, expect } from "../fixtures/auth.fixture";

const WORKFLOW_NAME = `e2e-workflow-${Date.now()}`;
const WORKFLOW_DESC = "E2E test workflow";
const UPDATED_NAME = `${WORKFLOW_NAME}-updated`;

test.describe.serial("Workflows", () => {
  test.beforeEach(async ({ workflowsPage }) => {
    await workflowsPage.goto();
    await workflowsPage.expectLoaded();
  });

  test("renders the workflows heading", async ({ page }) => {
    await expect(page.getByRole("heading", { name: /workflow/i })).toBeVisible();
  });

  test("shows the New Workflow button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /new workflow/i }).first()).toBeVisible();
  });

  test("opens the workflow editor on new workflow click", async ({ workflowsPage }) => {
    await workflowsPage.openCreateEditor();
    await workflowsPage.expectEditorOpen();
  });

  test("closes the editor without saving", async ({ workflowsPage, page }) => {
    await workflowsPage.openCreateEditor();
    await workflowsPage.expectEditorOpen();
    await page.getByRole("button", { name: /close|cancel/i }).click();
    await workflowsPage.expectEditorClosed();
  });

  test("creates a new workflow", async ({ workflowsPage }) => {
    await workflowsPage.openCreateEditor();
    await workflowsPage.fillWorkflowName(WORKFLOW_NAME);
    await workflowsPage.fillWorkflowDescription(WORKFLOW_DESC);
    await workflowsPage.submitSave();

    await workflowsPage.expectWorkflowVisible(WORKFLOW_NAME);
  });

  test("shows validation error when workflow name is empty", async ({ workflowsPage, page }) => {
    await workflowsPage.openCreateEditor();
    await page.locator("form").evaluate((form: HTMLFormElement) => form.requestSubmit());
    await expect(page.getByText(/name is required/i)).toBeVisible();
  });

  test("edits an existing workflow", async ({ workflowsPage }) => {
    await workflowsPage.clickEditOnWorkflow(WORKFLOW_NAME);
    await workflowsPage.expectEditorOpen();
    await workflowsPage.clearAndFillWorkflowName(UPDATED_NAME);
    await workflowsPage.submitSave();

    await workflowsPage.expectWorkflowVisible(UPDATED_NAME);
  });

  test("deletes a workflow", async ({ workflowsPage, page }) => {
    page.on("dialog", (dialog) => dialog.accept());
    await workflowsPage.clickDeleteOnWorkflow(UPDATED_NAME);
    await workflowsPage.expectWorkflowNotVisible(UPDATED_NAME);
  });
});
