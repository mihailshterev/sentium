import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class UsersPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/users");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "User Management" })).toBeVisible();
  }

  async expectUserVisible(email: string): Promise<void> {
    await expect(this.page.getByText(email)).toBeVisible();
  }

  async expectRegisteredUsersHeading(): Promise<void> {
    await expect(this.page.getByText(/registered users/i)).toBeVisible();
  }

  async expectUserCountVisible(): Promise<void> {
    await expect(this.page.getByText(/user[s]?$/i)).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByRole("button", { name: /refresh/i }).click();
  }
}
