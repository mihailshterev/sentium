import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SettingsPage {
  constructor(private readonly page: Page) {}

  async goto(section?: string): Promise<void> {
    await this.page.goto(section ? `/settings/${section}` : "/settings");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { level: 1, name: "Settings" })).toBeVisible();
    await expect(this.page.getByRole("heading", { level: 2, name: "Agents" })).toBeVisible();
  }

  section(name: string) {
    return this.page.getByRole("region", { name, exact: true });
  }

  async toggleBuiltInHarness(): Promise<void> {
    await this.page.getByRole("switch", { name: "Built-in governance harness" }).click();
  }

  async togglePromptEnhancement(): Promise<void> {
    await this.page.getByRole("switch", { name: "Prompt enhancement" }).click();
  }

  async fillUserHarnessPrompt(text: string): Promise<void> {
    const textarea = this.page.getByLabel("Global prompt", { exact: true });
    await textarea.clear();
    await textarea.fill(text);
  }

  async search(text: string): Promise<void> {
    await this.page.getByRole("searchbox", { name: "Search settings" }).fill(text);
  }

  async saveSettings(): Promise<void> {
    await this.page.getByRole("button", { name: /save changes/i }).click();
  }

  async expectSuccessFeedback(): Promise<void> {
    await expect(this.page.getByText(/successfully saved/i)).toBeVisible();
  }

  async expectErrorFeedback(): Promise<void> {
    await expect(this.page.getByText(/failed to save/i)).toBeVisible();
  }

  async expectSaveButtonVisible(): Promise<void> {
    await expect(this.page.getByRole("button", { name: /save changes/i })).toBeVisible();
  }
}
