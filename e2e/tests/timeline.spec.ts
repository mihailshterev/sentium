import { test, expect } from "../fixtures";
import { localToday } from "../pages/TimelinePage";
import { mockSseRoute, sseDone, sseMessage } from "../support/mock-sse";
import { uniqueName } from "../support/test-data";

test.describe("Timeline", () => {
  test.beforeEach(async ({ timelinePage }) => {
    await timelinePage.goto();
    await timelinePage.expectLoaded();
  });

  test("shows the twelve months with the current one marked", { tag: "@smoke" }, async ({ timelinePage }) => {
    const today = localToday();

    await expect(timelinePage.monthNodes).toHaveCount(12);
    await expect(timelinePage.monthNode(today.month)).toHaveAttribute("aria-current", "date");
    await expect(timelinePage.monthNode(today.month)).toHaveAccessibleName(
      new RegExp(`^${today.monthName} ${today.year}`),
    );
  });

  test("expands a month into its calendar and collapses it again", async ({ timelinePage, page }) => {
    const today = localToday();

    await timelinePage.openMonth(today.month, today.monthName, today.year);
    await expect(page).toHaveURL(new RegExp(`month=${today.month}`));
    await expect(timelinePage.day(today.iso)).toHaveAttribute("data-today", "true");
    await expect(timelinePage.pinnedZone.getByText("e2e-baseline-pinned")).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(timelinePage.calendar).toBeHidden();
    await expect(page).not.toHaveURL(/month=/);

    await page.goBack();
    await expect(timelinePage.calendar).toBeVisible();
  });

  test("adds, edits, completes, pins and deletes a task", { tag: "@regression" }, async ({ timelinePage, page }) => {
    test.slow();
    const today = localToday();
    const title = uniqueName("e2e-task");

    await timelinePage.openMonth(today.month, today.monthName, today.year);

    await test.step("add a task on today", async () => {
      await timelinePage.addTaskOn(today.label, title);
      await expect(timelinePage.day(today.iso).getByRole("button", { name: `Edit task: ${title}` })).toBeVisible();
    });

    await test.step("edit its priority", async () => {
      await timelinePage.chip(title).click();
      await timelinePage.dialog.getByLabel("Priority").selectOption("Urgent");
      await timelinePage.dialog.getByRole("button", { name: "Save" }).click();
      await expect(timelinePage.dialog).toBeHidden();
      await expect(timelinePage.chip(title)).toHaveAttribute("data-priority", "urgent");
    });

    await test.step("complete it from the day's agenda", async () => {
      await timelinePage
        .day(today.iso)
        .getByRole("button", { name: new RegExp(`^${today.label}`) })
        .click();
      // Controlled by the optimistic cache update, which lands a tick after the click.
      await page.getByRole("checkbox", { name: `Complete ${title}` }).click();
      await expect(page.getByRole("checkbox", { name: `Reopen ${title}` })).toBeChecked();
      await expect(timelinePage.chip(title)).toHaveAttribute("data-done", "true");
    });

    await test.step("pin it to the month", async () => {
      await timelinePage.chip(title).click();
      await timelinePage.dialog.getByText("Pinned to a month").click();
      await timelinePage.dialog.getByRole("button", { name: "Save" }).click();
      await expect(timelinePage.dialog).toBeHidden();
      await expect(timelinePage.pinnedZone.getByText(title)).toBeVisible();
      await expect(timelinePage.chip(title)).toHaveCount(0);
    });

    await test.step("delete it", async () => {
      await timelinePage.pinnedZone.getByRole("button", { name: new RegExp(`^${title}`) }).click();
      await timelinePage.dialog.getByRole("button", { name: "Delete" }).click();
      await page.getByTestId("confirm-dialog-confirm").click();
      await expect(timelinePage.pinnedZone.getByText(title)).toBeHidden();
    });
  });

  test("lets the user accept an agent suggestion", async ({ timelinePage, page }) => {
    const today = localToday();
    const suggestion = {
      id: "e2e-proposal",
      kind: "CreateTask",
      status: "Pending",
      taskId: null,
      title: "Suggested by the planner",
      description: null,
      priority: "Normal",
      tags: [],
      assignedAgent: null,
      dueDate: null,
      year: today.year,
      month: today.month,
      date: today.iso,
      fromYear: null,
      fromMonth: null,
      fromDate: null,
      rationale: "Today is light",
      proposedBy: "TimelinePlanner",
      createdAt: new Date().toISOString(),
      resolvedAt: null,
    };

    // Every browser project shares the seeded user, so the suggestion is mocked rather than stored.
    await page.route(/\/api\/tempus\/years\/\d+\/months\/\d+$/, async (route) => {
      const response = await route.fetch();
      const body = await response.json();
      await route.fulfill({ response, json: { ...body, proposals: [suggestion] } });
    });
    await page.route(/\/api\/tempus\/proposals\/e2e-proposal\/accept$/, (route) =>
      route.fulfill({ json: { id: "accepted", title: suggestion.title } }),
    );

    await timelinePage.openMonth(today.month, today.monthName, today.year);
    await expect(timelinePage.day(today.iso).getByText("Suggested by the planner")).toBeVisible();

    const accept = page.waitForRequest(
      (request) => request.method() === "POST" && request.url().endsWith("/proposals/e2e-proposal/accept"),
    );
    await page.getByTestId("proposal-e2e-proposal").getByRole("button", { name: "Accept" }).click();
    await accept;

    // Accepting refreshes the month; don't let a refetch still in flight hit the mock after the page closes.
    await page.unrouteAll({ behavior: "ignoreErrors" });
  });

  test("chats with the planner about a month", async ({ timelinePage, page }) => {
    const today = localToday();
    await mockSseRoute(page, /\/assistant\/chat$/, [
      sseMessage({ type: "message", message: { content: "Your month looks balanced." } }),
      sseDone(),
    ]);

    await timelinePage.openMonth(today.month, today.monthName, today.year);
    await page.getByRole("button", { name: "Plan with agent" }).click();

    const drawer = page.getByRole("complementary", { name: "Agent chat" });
    await expect(drawer.getByText("TimelinePlanner", { exact: true })).toBeVisible();
    await drawer.getByRole("textbox", { name: "Chat message" }).fill("How busy am I?");
    await drawer.getByRole("button", { name: "Send message" }).click();

    await expect(drawer.getByText("Your month looks balanced.")).toBeVisible();
    await expect(drawer.getByRole("link", { name: /Open in assistant/ })).toBeVisible();

    await drawer.getByRole("button", { name: "Close agent chat" }).click();
    await expect(drawer).toBeHidden();
  });
});
