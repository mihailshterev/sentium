import type { Locator, Page } from "@playwright/test";
import { expect } from "@playwright/test";

/** The sidebar plus the top bar's notifications and account menu. */
export class NavbarComponent {
  constructor(private readonly page: Page) {}

  get sidebar(): Locator {
    return this.page.getByRole("navigation", { name: "Main navigation" });
  }

  async navigateTo(label: string): Promise<void> {
    await this.sidebar.getByRole("link", { name: label, exact: true }).click();
  }

  async clickLogout(): Promise<void> {
    await this.page.getByRole("button", { name: /account menu/i }).click();
    await this.page.getByRole("menuitem", { name: "Sign out" }).click();
  }

  async expectActiveLink(label: string): Promise<void> {
    await expect(this.sidebar.getByRole("link", { name: label, exact: true })).toHaveAttribute("aria-current", "page");
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
    await expect(this.sidebar).toBeVisible();
  }

  async expectUserVisible(): Promise<void> {
    await expect(this.page.getByRole("button", { name: /account menu/i })).toBeVisible();
  }
}
