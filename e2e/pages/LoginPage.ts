import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class LoginPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/login");
  }

  async switchToRegister(): Promise<void> {
    await this.page.getByRole("button", { name: "Register" }).click();
  }

  async switchToSignIn(): Promise<void> {
    await this.page.getByRole("button", { name: "Sign in" }).click();
  }

  async fillEmail(email: string): Promise<void> {
    await this.page.getByLabel("Email address").fill(email);
  }

  async fillPassword(password: string): Promise<void> {
    await this.page.getByLabel("Password").fill(password);
  }

  async submitSignIn(): Promise<void> {
    await this.page.locator("form").getByRole("button", { name: "Sign in" }).click();
  }

  async submitCreateAccount(): Promise<void> {
    await this.page.getByRole("button", { name: "Create account" }).click();
  }

  async login(email: string, password: string): Promise<void> {
    await this.fillEmail(email);
    await this.fillPassword(password);
    await this.submitSignIn();
  }

  async register(email: string, password: string): Promise<void> {
    await this.switchToRegister();
    await this.fillEmail(email);
    await this.fillPassword(password);
    await this.submitCreateAccount();
  }

  async expectValidationError(message: string): Promise<void> {
    await expect(this.page.getByRole("alert").filter({ hasText: message })).toBeVisible();
  }

  async expectErrorBanner(text: string): Promise<void> {
    await expect(this.page.getByText(text)).toBeVisible();
  }

  async expectSignInButtonDisabled(): Promise<void> {
    await expect(this.page.getByRole("button", { name: /Signing in/ })).toBeDisabled();
  }
}
