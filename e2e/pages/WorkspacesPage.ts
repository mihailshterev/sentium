import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class WorkspacesPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/workspaces");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Workspaces", exact: true })).toBeVisible();
  }

  async openCreateForm(): Promise<void> {
    await this.page.getByRole("button", { name: /new workspace/i }).click();
  }

  async expectWorkspaceNameInputVisible(): Promise<void> {
    await expect(this.page.getByLabel("Name")).toBeVisible();
  }

  async fillWorkspaceName(name: string): Promise<void> {
    await this.page.getByLabel("Name").fill(name);
  }

  async fillWorkspaceDescription(description: string): Promise<void> {
    await this.page.getByLabel("Description").fill(description);
  }

  async submitWorkspace(): Promise<void> {
    await this.page.getByRole("button", { name: /create workspace/i }).click();
  }

  async createWorkspace(name: string, description: string): Promise<void> {
    await this.openCreateForm();
    await this.fillWorkspaceName(name);
    await this.fillWorkspaceDescription(description);
    await this.submitWorkspace();
  }

  async selectWorkspace(name: string): Promise<void> {
    await this.page.getByText(name, { exact: true }).first().click();
  }

  async deleteWorkspace(name: string): Promise<void> {
    await this.page.getByTestId(`workspace-delete-${name}`).click();
    await this.page.getByTestId("confirm-dialog-confirm").click();
  }

  async expectWorkspaceVisible(name: string): Promise<void> {
    await expect(this.page.getByRole("heading", { name, exact: true })).toBeVisible();
  }

  async expectWorkspaceNotVisible(name: string): Promise<void> {
    await expect(this.page.getByRole("heading", { name, exact: true })).not.toBeVisible();
  }

  async expectFileVisible(fileName: string): Promise<void> {
    await expect(this.page.getByText(fileName, { exact: true })).toBeVisible();
  }

  async uploadTextFile(fileName: string, content: string): Promise<void> {
    await this.page
      .locator('input[type="file"]')
      .setInputFiles({ name: fileName, mimeType: "text/markdown", buffer: Buffer.from(content) });
    await this.page.getByRole("button", { name: /upload to/i }).click();
  }

  async previewFile(): Promise<void> {
    await this.page.getByTitle("Preview").first().click();
  }

  async expectEmptyState(): Promise<void> {
    await expect(this.page.getByText(/no workspaces/i)).toBeVisible();
  }
}
