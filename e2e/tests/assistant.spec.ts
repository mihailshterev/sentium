import { test, expect } from "../fixtures";
import { mockSseRoute, sseMessage, sseDone } from "../support/mock-sse";

test.describe("Assistant", () => {
  test.beforeEach(async ({ assistantPage }) => {
    await assistantPage.goto();
    await assistantPage.expectLoaded();
  });

  test("renders the assistant heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { level: 1, name: "Assistant" })).toBeVisible();
  });

  test("shows the message input", async ({ page }) => {
    await expect(page.getByPlaceholder(/ask sentium/i)).toBeVisible();
  });

  test("shows the new conversation button", async ({ assistantPage }) => {
    await expect(assistantPage.newConversationButton).toBeVisible();
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

  test(
    "shows the agent's thinking and tool calls as an ordered timeline",
    { tag: "@regression" },
    async ({ assistantPage, page }) => {
      const toolCall = (callId: string, name: string, args: object) => ({
        type: "tool_call",
        message: { content: JSON.stringify({ callId, name, arguments: JSON.stringify(args) }) },
      });
      const toolResult = (callId: string, result: string, durationMs: number) => ({
        type: "tool_result",
        message: { content: JSON.stringify({ callId, status: "ok", result, durationMs }) },
      });

      await mockSseRoute(page, /\/assistant\/chat/, [
        sseMessage({ type: "thought", message: { content: "The user wants their drill." } }),
        sseMessage(toolCall("c1", "find_assets", { query: "drill" })),
        sseMessage(toolResult("c1", "Cordless drill - garage", 42)),
        sseMessage({ type: "thought", message: { content: "Found it in the garage." } }),
        sseMessage({ type: "message", message: { content: "Your drill is in the garage." } }),
        sseDone(),
      ]);

      await assistantPage.startNewConversation();
      await assistantPage.typeMessage("where is my drill?");
      await assistantPage.sendMessage();
      await assistantPage.expectMessageVisible("Your drill is in the garage.");

      const summary = page.getByRole("button", { name: /2 thoughts · 1 tool call/ });
      await expect(summary).toBeVisible();
      await summary.click();

      const steps = page.locator("ol > li[data-kind]");
      await expect(steps).toHaveCount(3);
      await expect(steps.nth(0)).toHaveAttribute("data-kind", "thought");
      await expect(steps.nth(1)).toHaveAttribute("data-kind", "tool");
      await expect(steps.nth(2)).toHaveAttribute("data-kind", "thought");

      await steps
        .nth(1)
        .getByRole("button", { name: /find_assets/ })
        .click();
      await expect(steps.nth(1).getByText("Cordless drill - garage")).toBeVisible();
      await expect(steps.nth(1).getByText("42ms")).toBeVisible();
    },
  );
});
