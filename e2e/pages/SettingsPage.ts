import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SettingsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/settings");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Settings" })).toBeVisible();
  }

  async toggleBuiltInHarness(): Promise<void> {
    await this.page
      .getByText("Enable built-in harness")
      .locator("..")
      .locator("..")
      .locator("input[type=checkbox]")
      .click();
  }

  async togglePromptEnhancement(): Promise<void> {
    await this.page
      .getByText("Enable prompt enhancement")
      .locator("..")
      .locator("..")
      .locator("input[type=checkbox]")
      .click();
  }

  async fillUserHarnessPrompt(text: string): Promise<void> {
    const textarea = this.page.locator("textarea");
    await textarea.clear();
    await textarea.fill(text);
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
