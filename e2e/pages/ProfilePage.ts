import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class ProfilePage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/profile");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("button", { name: /save changes/i })).toBeVisible();
  }

  async fillFirstName(text: string): Promise<void> {
    await this.page.locator("#firstName").clear();
    await this.page.locator("#firstName").fill(text);
  }

  async fillLastName(text: string): Promise<void> {
    await this.page.locator("#lastName").clear();
    await this.page.locator("#lastName").fill(text);
  }

  async fillEmail(text: string): Promise<void> {
    await this.page.locator("#email").clear();
    await this.page.locator("#email").fill(text);
  }

  async saveProfile(): Promise<void> {
    await this.page.getByRole("button", { name: /save changes/i }).click();
  }

  async expectSuccessFeedback(): Promise<void> {
    await expect(this.page.getByText(/profile updated successfully/i)).toBeVisible();
  }

  async expectEmailVisible(email: string): Promise<void> {
    await expect(this.page.getByText(email)).toBeVisible();
  }

  async expectRoleBadge(role: string): Promise<void> {
    await expect(this.page.getByRole("main").getByText(new RegExp(role, "i")).first()).toBeVisible();
  }
}
