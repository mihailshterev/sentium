import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class OrchestrationPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/orchestration");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Orchestration" })).toBeVisible();
  }

  async selectPredefinedMode(): Promise<void> {
    await this.page.getByRole("button", { name: /predefined/i }).click();
  }

  async selectDynamicMode(): Promise<void> {
    await this.page.getByRole("button", { name: /dynamic/i }).click();
  }

  async selectWorkflow(name: string): Promise<void> {
    await this.page.getByText(name).click();
  }

  async fillScenario(text: string): Promise<void> {
    await this.page.getByPlaceholder(/describe the activity/i).fill(text);
  }

  async switchToHistory(): Promise<void> {
    await this.page.getByRole("button", { name: /history/i }).click();
  }

  async expectLogPanelVisible(): Promise<void> {
    await expect(this.page.getByText("Output", { exact: true })).toBeVisible();
  }

  async expectWorkflowInList(name: string): Promise<void> {
    await expect(this.page.getByText(name)).toBeVisible();
  }

  async expectHistoryPanelVisible(): Promise<void> {
    await expect(this.page.getByText(/no runs recorded/i)).toBeVisible();
  }
}
