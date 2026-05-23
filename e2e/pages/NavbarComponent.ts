import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class NavbarComponent {
  constructor(private readonly page: Page) {}

  async navigateTo(label: string): Promise<void> {
    await this.page.getByRole("link", { name: label }).click();
  }

  async clickLogout(): Promise<void> {
    await this.page.getByRole("button", { name: /log out/i }).click();
  }

  async expectActiveLink(label: string): Promise<void> {
    await expect(this.page.getByRole("link", { name: label })).toHaveAttribute("aria-current", "page");
  }

  async expectNavVisible(): Promise<void> {
    await expect(this.page.getByRole("navigation")).toBeVisible();
  }

  async expectUserVisible(): Promise<void> {
    await expect(this.page.getByRole("navigation")).toBeVisible();
  }
}
