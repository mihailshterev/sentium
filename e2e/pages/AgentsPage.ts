import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class AgentsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/agents");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Agent Registry" })).toBeVisible();
  }

  async fillAgentName(name: string): Promise<void> {
    await this.page.getByLabel("Agent Name").fill(name);
  }

  async fillAgentDescription(description: string): Promise<void> {
    await this.page.getByLabel("Description").fill(description);
  }

  async selectModel(modelName: string): Promise<void> {
    await this.page.getByLabel("Model").selectOption(modelName);
  }

  async fillModel(modelName: string): Promise<void> {
    await this.page.getByLabel("Model", { exact: true }).fill(modelName);
  }

  async submitCreate(): Promise<void> {
    await this.page.getByRole("button", { name: /register agent/i }).click();
  }

  async createAgent(name: string, description: string, model?: string): Promise<void> {
    await this.fillAgentName(name);
    await this.fillAgentDescription(description);
    if (model) await this.selectModel(model);
    await this.submitCreate();
  }

  async clickEditOnAgent(agentName: string): Promise<void> {
    await this.page.getByTestId(`agent-edit-${agentName}`).click();
  }

  async clearAndFillDescriptionInEdit(description: string): Promise<void> {
    const field = this.page.locator("#edit-description");
    await field.clear();
    await field.fill(description);
  }

  async submitEdit(): Promise<void> {
    await this.page.getByRole("button", { name: /save changes/i }).click();
  }

  async clickDeleteOnAgent(agentName: string): Promise<void> {
    await this.page.getByTestId(`agent-delete-${agentName}`).click();
  }

  async confirmDelete(): Promise<void> {
    await this.page.getByTestId("confirm-dialog-confirm").click();
  }

  async expectAgentVisible(name: string): Promise<void> {
    await expect(this.page.getByText(name)).toBeVisible();
  }

  async expectAgentNotVisible(name: string): Promise<void> {
    await expect(this.page.getByText(name)).not.toBeVisible();
  }

  async expectSuccessFeedback(): Promise<void> {
    await expect(
      this.page
        .getByRole("status")
        .or(this.page.getByText(/success/i))
        .first(),
    ).toBeVisible();
  }

  async expectEmptyState(): Promise<void> {
    await expect(this.page.getByText(/no agents registered yet/i)).toBeVisible();
  }
}
