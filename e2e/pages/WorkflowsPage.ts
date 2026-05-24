import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class WorkflowsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/workflows");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: /workflow/i })).toBeVisible();
  }

  async openCreateEditor(): Promise<void> {
    await this.page
      .getByRole("button", { name: /new workflow/i })
      .first()
      .click();
  }

  async fillWorkflowName(name: string): Promise<void> {
    await this.page.getByLabel("Name").fill(name);
  }

  async fillWorkflowDescription(description: string): Promise<void> {
    await this.page.getByLabel("Description").fill(description);
  }

  async addAgentToWorkflow(agentName: string): Promise<void> {
    await this.page.getByRole("button", { name: new RegExp(`add ${agentName}`, "i") }).click();
  }

  async submitSave(): Promise<void> {
    await this.page.getByRole("button", { name: /create workflow|save changes/i }).click();
  }

  async createWorkflow(name: string, description: string): Promise<void> {
    await this.openCreateEditor();
    await this.fillWorkflowName(name);
    await this.fillWorkflowDescription(description);
    await this.submitSave();
  }

  async clickEditOnWorkflow(workflowName: string): Promise<void> {
    await this.page
      .getByText(workflowName, { exact: true })
      .locator("xpath=ancestor::div[.//button[@title='Edit']][1]")
      .getByTitle("Edit")
      .click();
  }

  async clearAndFillWorkflowName(name: string): Promise<void> {
    const field = this.page.getByLabel("Name");
    await field.clear();
    await field.fill(name);
  }

  async clickDeleteOnWorkflow(workflowName: string): Promise<void> {
    await this.page
      .getByText(workflowName, { exact: true })
      .locator("xpath=ancestor::div[.//button[@title='Delete']][1]")
      .getByTitle("Delete")
      .click();
  }

  async expectWorkflowVisible(name: string): Promise<void> {
    await expect(this.page.getByText(name)).toBeVisible();
  }

  async expectWorkflowNotVisible(name: string): Promise<void> {
    await expect(this.page.getByText(name)).not.toBeVisible();
  }

  async expectEditorOpen(): Promise<void> {
    await expect(this.page.getByRole("button", { name: /create workflow|save changes/i })).toBeVisible();
  }

  async expectEditorClosed(): Promise<void> {
    await expect(this.page.getByRole("button", { name: /create workflow|save changes/i })).not.toBeVisible();
  }
}
