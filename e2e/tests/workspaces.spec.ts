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

  test("uploads, previews and removes a file", { tag: "@regression" }, async ({ workspacesPage, page }) => {
    const name = uniqueName("e2e-files");
    const fileName = `${uniqueName("notes")}.md`;

    await workspacesPage.createWorkspace(name, "Files round trip");
    await workspacesPage.selectWorkspace(name);

    await test.step("upload", async () => {
      await workspacesPage.uploadTextFile(fileName, "# Release notes\nThe e2e marker line.");
      await workspacesPage.expectFileVisible(fileName);
    });

    await test.step("preview its text", async () => {
      await workspacesPage.previewFile();
      const dialog = page.getByRole("dialog");
      await expect(dialog.getByTestId("file-preview-text")).toContainText("The e2e marker line.");
      await expect(dialog.getByRole("link", { name: /download original/i })).toBeVisible();
      await page.keyboard.press("Escape");
    });

    await test.step("remove it", async () => {
      await page.getByTitle("Remove file").click();
      await page.getByTestId("confirm-dialog-confirm").click();
      await expect(page.getByText(fileName, { exact: true })).not.toBeVisible();
    });
  });
});
