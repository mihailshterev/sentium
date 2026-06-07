import { test, expect } from "../fixtures/auth.fixture";

const WORKSPACE_NAME = `e2e-workspace-${Date.now()}`;
const WORKSPACE_DESC = "Created by Playwright E2E suite";

test.describe.serial("Workspaces", () => {
  test.beforeEach(async ({ workspacesPage }) => {
    await workspacesPage.goto();
    await workspacesPage.expectLoaded();
  });

  test("renders the workspaces heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Workspaces", exact: true })).toBeVisible();
  });

  test("shows the seeded baseline workspace", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByText("e2e-baseline-workspace")).toBeVisible();
  });

  test("shows new workspace button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /new workspace/i })).toBeVisible();
  });

  test("opens the workspace creation form", async ({ workspacesPage }) => {
    await workspacesPage.openCreateForm();
    await workspacesPage.expectWorkspaceNameInputVisible();
  });

  test("creates a new workspace", { tag: "@regression" }, async ({ workspacesPage }) => {
    await workspacesPage.createWorkspace(WORKSPACE_NAME, WORKSPACE_DESC);
    await workspacesPage.expectWorkspaceVisible(WORKSPACE_NAME);
  });

  test("selects a workspace and shows file upload area", { tag: "@regression" }, async ({ workspacesPage, page }) => {
    await workspacesPage.selectWorkspace(WORKSPACE_NAME);
    await expect(page.getByRole("button", { name: /upload/i })).toBeVisible();
  });

  test("deletes a workspace", { tag: "@regression" }, async ({ workspacesPage }) => {
    await workspacesPage.deleteWorkspace(WORKSPACE_NAME);
    await workspacesPage.expectWorkspaceNotVisible(WORKSPACE_NAME);
  });
});
