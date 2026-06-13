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

  async expectInstalledPanelVisible(): Promise<void> {
    await expect(this.page.getByText(/installed models/i)).toBeVisible();
  }

  async expectDownloadPanelVisible(): Promise<void> {
    await expect(this.page.getByText(/download model/i)).toBeVisible();
  }

  async expectEmptyModelList(): Promise<void> {
    await expect(this.page.getByText(/no models installed/i)).toBeVisible();
  }

  async expectModelVisible(name: string): Promise<void> {
    await expect(this.page.getByText(name)).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByTitle("Refresh model list").click();
  }
}
