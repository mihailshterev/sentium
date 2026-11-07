import { test, expect } from "../fixtures";
import { uniqueName } from "../support/test-data";

test.describe("Workflows", () => {
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

  test("creates, renames and deletes a workflow", { tag: "@regression" }, async ({ workflowsPage }) => {
    const name = uniqueName("e2e-workflow");
    const renamed = `${name}-renamed`;

    await test.step("create", async () => {
      await workflowsPage.openCreateEditor();
      await workflowsPage.fillWorkflowName(name);
      await workflowsPage.fillWorkflowDescription("E2E test workflow");
      await workflowsPage.submitSave();
      await workflowsPage.expectWorkflowVisible(name);
    });

    await test.step("rename", async () => {
      await workflowsPage.clickEditOnWorkflow(name);
      await workflowsPage.expectEditorOpen();
      await workflowsPage.clearAndFillWorkflowName(renamed);
      await workflowsPage.submitSave();
      await workflowsPage.expectWorkflowVisible(renamed);
    });

    await test.step("delete", async () => {
      await workflowsPage.clickDeleteOnWorkflow(renamed);
      await workflowsPage.confirmDelete();
      await workflowsPage.expectWorkflowNotVisible(renamed);
    });
  });
});
