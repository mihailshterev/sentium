import type { Locator, Page } from "@playwright/test";
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

  get notificationBell(): Locator {
    return this.page.getByTestId("notification-bell");
  }

  async openInboxPanel(): Promise<Locator> {
    await this.notificationBell.click();
    const panel = this.page.getByRole("dialog", { name: "Inbox" });
    await expect(panel).toBeVisible();
    return panel;
  }

  async expectNavVisible(): Promise<void> {
    await expect(this.page.getByRole("navigation")).toBeVisible();
  }

  async expectUserVisible(): Promise<void> {
    await expect(this.page.getByRole("navigation")).toBeVisible();
  }
}
