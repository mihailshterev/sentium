import { test, expect } from "../fixtures";
import { mockAssistantTurn, turnFrame } from "../support/mock-sse";
import { uniqueName } from "../support/test-data";

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

  test(
    "uploads a file from the composer and sends it as an attachment, not as text",
    { tag: "@regression" },
    async ({ assistantPage, page }) => {
      await mockAssistantTurn(page, [turnFrame("message", "Read it.")]);
      const fileName = `${uniqueName("brief")}.md`;

      await assistantPage.startNewConversation();
      await page
        .getByTestId("composer-file-input")
        .setInputFiles({ name: fileName, mimeType: "text/markdown", buffer: Buffer.from("# Brief") });
      await expect(page.getByTestId("composer-attachment")).toContainText(fileName);
      await expect(page.locator("form button[type='submit']")).toBeEnabled();

      const chatRequest = page.waitForRequest(/\/assistant\/chat$/);
      await assistantPage.typeMessage("Summarise the brief");
      await assistantPage.sendMessage();

      const body = (await chatRequest).postDataJSON() as { fileIds?: string[]; messages: { content: string }[] };
      expect(body.fileIds).toHaveLength(1);
      expect(body.messages.at(-1)?.content).toBe("Summarise the brief");

      await assistantPage.expectMessageVisible("Read it.");
      await expect(page.getByRole("button", { name: `Preview ${fileName}` })).toBeVisible();
      await expect(page.getByText("[File:")).toHaveCount(0);
    },
  );

  test("sends a message and shows mocked SSE response", { tag: "@regression" }, async ({ assistantPage, page }) => {
    await mockAssistantTurn(page, [turnFrame("message", "Hello from mock")]);

    await assistantPage.startNewConversation();
    await assistantPage.typeMessage("hello world");
    await assistantPage.sendMessage();
    await assistantPage.expectMessageVisible("Hello from mock");
  });

  test(
    "shows the agent's thinking and tool calls as an ordered timeline",
    { tag: "@regression" },
    async ({ assistantPage, page }) => {
      const toolCall = (callId: string, name: string, args: object) =>
        turnFrame("tool_call", JSON.stringify({ callId, name, arguments: JSON.stringify(args) }));
      const toolResult = (callId: string, result: string, durationMs: number) =>
        turnFrame("tool_result", JSON.stringify({ callId, status: "ok", result, durationMs }));

      await mockAssistantTurn(page, [
        turnFrame("thought", "The user wants their drill."),
        toolCall("c1", "find_assets", { query: "drill" }),
        toolResult("c1", "Cordless drill - garage", 42),
        turnFrame("thought", "Found it in the garage."),
        turnFrame("message", "Your drill is in the garage."),
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
