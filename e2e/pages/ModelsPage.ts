import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class ModelsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/models");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Model Management" })).toBeVisible();
  }

  async expectChatModelsPanelVisible(): Promise<void> {
    await expect(this.page.getByText("Chat Models", { exact: true })).toBeVisible();
  }

  async expectAddingModelsPanelVisible(): Promise<void> {
    await expect(this.page.getByText("Adding Models", { exact: true })).toBeVisible();
  }

  async expectEmptyModelList(): Promise<void> {
    await expect(this.page.getByText(/no models available/i)).toBeVisible();
  }

  async expectModelVisible(name: string): Promise<void> {
    await expect(this.page.getByText(name)).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByTitle("Refresh model list").click();
  }
}
