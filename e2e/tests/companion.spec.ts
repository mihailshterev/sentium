import { test, expect } from "../fixtures";
import { mockAssistantTurn, turnFrame } from "../support/mock-sse";

// The e2e stack runs with reduced motion and without the speech containers, so these cover the Companion's
// silent behaviour: it is always there, it reflects real activity, and no voice control is offered.
test.describe("Companion", () => {
  test("is shown on every page and keeps its place across navigation", { tag: "@smoke" }, async ({ page, navbar }) => {
    await page.goto("/");
    const companion = page.getByTestId("companion");
    await expect(companion).toBeVisible();
    await expect(companion).toHaveAccessibleName(/^Companion, .+\. Open quick chat$/);

    await navbar.navigateTo("Inventory");
    await expect(page.getByRole("heading", { level: 1, name: "Inventory" })).toBeVisible();
    await expect(companion).toBeVisible();
  });

  test(
    "shows thinking while a reply streams and settles afterwards",
    { tag: "@regression" },
    async ({ assistantPage, page }) => {
      // Held open long enough to outlast the Companion's settle delay; an instant reply is deliberately not shown.
      await mockAssistantTurn(page, [turnFrame("message", "Hello from the companion test")], { delayMs: 1500 });

      await assistantPage.goto();
      await assistantPage.expectLoaded();
      const companion = page.getByTestId("companion");
      await expect(companion).not.toHaveAttribute("data-state", "thinking");

      await assistantPage.startNewConversation();
      await assistantPage.typeMessage("hello");
      await assistantPage.sendMessage();

      await expect(companion).toHaveAttribute("data-state", "thinking");
      await assistantPage.expectMessageVisible("Hello from the companion test");
      // Idle, or alert when the seeded user has an unread warning.
      await expect(companion).toHaveAttribute("data-state", /^(idle|alert)$/);
    },
  );

  test("opens a quick chat without leaving the page", async ({ page }) => {
    await page.goto("/inventory");
    await page.getByTestId("companion").click();

    const quickChat = page.getByRole("dialog", { name: "Quick chat" });
    await expect(quickChat).toBeVisible();
    await expect(quickChat.getByRole("textbox", { name: "Quick chat message" })).toBeFocused();
    await expect(page).toHaveURL(/\/inventory$/);

    await page.keyboard.press("Escape");
    await expect(quickChat).toBeHidden();
  });

  test("offers no voice controls while speech is unavailable", async ({ assistantPage, page }) => {
    const status = page.waitForResponse((r) => r.url().endsWith("/api/praxis/speech") && r.ok());
    await assistantPage.goto();
    await assistantPage.expectLoaded();
    expect(await (await status).json()).toMatchObject({ transcription: false, synthesis: false });

    await expect(page.getByTestId("push-to-talk")).toHaveCount(0);

    await page.getByTestId("companion").click();
    await expect(page.getByRole("dialog", { name: "Quick chat" })).toBeVisible();
    await expect(page.getByTestId("push-to-talk")).toHaveCount(0);
  });
});
