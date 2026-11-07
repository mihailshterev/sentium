import { test, expect } from "../fixtures";
import { uniqueName } from "../support/test-data";

test.describe("Workspaces", () => {
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

  test("creates, opens and deletes a workspace", { tag: "@regression" }, async ({ workspacesPage, page }) => {
    const name = uniqueName("e2e-workspace");

    await test.step("create", async () => {
      await workspacesPage.createWorkspace(name, "Created by Playwright E2E suite");
      await workspacesPage.expectWorkspaceVisible(name);
    });

    await test.step("open it and see the file upload area", async () => {
      await workspacesPage.selectWorkspace(name);
      await expect(page.getByRole("button", { name: /upload/i })).toBeVisible();
    });

    await test.step("delete it from the list", async () => {
      await workspacesPage.goto();
      await workspacesPage.deleteWorkspace(name);
      await workspacesPage.expectWorkspaceNotVisible(name);
    });
  });
});
