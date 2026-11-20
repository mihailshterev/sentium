import { test, expect } from "../fixtures";

test.describe("Scheduler", () => {
  test.beforeEach(async ({ schedulerPage }) => {
    await schedulerPage.goto();
    await schedulerPage.expectLoaded();
  });

  test("renders the Scheduler heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Scheduler", exact: true })).toBeVisible();
  });

  test("shows the schedule stats", { tag: "@smoke" }, async ({ schedulerPage }) => {
    await schedulerPage.expectStatsVisible();
  });

  test("shows empty state when no schedules exist", { tag: "@regression" }, async ({ schedulerPage }) => {
    await schedulerPage.expectEmptyState();
  });

  test("shows the Refresh button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /^refresh$/i })).toBeVisible();
  });

  test("warns that deleting a schedule is permanent", async ({ page }) => {
    await expect(page.getByText(/deleting a schedule removes it permanently/i)).toBeVisible();
  });

  test("offers the morning briefing and weekly digest templates", async ({ page, schedulerPage }) => {
    await schedulerPage.openNewSchedule();
    await expect(page.getByTestId("schedule-template-morning-briefing")).toBeVisible();
    await expect(page.getByTestId("schedule-template-weekly-digest")).toBeVisible();
  });

  test("lists the background services for a Sovereign", async ({ page, schedulerPage }) => {
    await schedulerPage.openBackgroundJobs();
    await expect(page.getByTestId("background-services")).toBeVisible();
    await expect(page.getByText("Workflow run processor")).toBeVisible();
  });
});
