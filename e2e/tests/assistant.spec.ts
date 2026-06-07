import { test, expect } from "../fixtures/auth.fixture";
import { mockSseRoute, sseMessage, sseDone } from "../helpers/mock-sse";

test.describe.serial("Assistant", () => {
  test.beforeEach(async ({ assistantPage }) => {
    await assistantPage.goto();
    await assistantPage.expectLoaded();
  });

  test("renders the assistant heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: /assistant/i })).toBeVisible();
  });

  test("shows the message input", async ({ page }) => {
    await expect(page.getByPlaceholder(/ask sentium/i)).toBeVisible();
  });

  test("shows the new conversation button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /new conversation/i })).toBeVisible();
  });

  test("shows the conversation sidebar", async ({ page }) => {
    await expect(page.getByRole("complementary")).toBeVisible();
  });

  test("creates a new conversation", async ({ assistantPage, page }) => {
    await assistantPage.startNewConversation();
    await expect(page.getByTitle("Delete conversation").first()).toBeVisible();
  });

  test("message input accepts text", async ({ page }) => {
    const input = page.getByPlaceholder(/ask sentium/i);
    await input.fill("Hello, Sentium!");
    await expect(input).toHaveValue("Hello, Sentium!");
  });

  test("send button is visible", async ({ page }) => {
    await expect(page.locator("form button[type='submit']")).toBeVisible();
  });

  test("model selector is visible", async ({ page }) => {
    await expect(page.getByPlaceholder(/model name/i)).toBeVisible();
  });

  test("sends a message and shows mocked SSE response", { tag: "@regression" }, async ({ assistantPage, page }) => {
    await mockSseRoute(page, /\/assistant\/chat/, [
      sseMessage({ type: "content", message: { content: "Hello from mock" } }),
      sseDone(),
    ]);

    await assistantPage.startNewConversation();
    await assistantPage.typeMessage("hello world");
    await assistantPage.sendMessage();
    await assistantPage.expectMessageVisible("Hello from mock");
  });
});
