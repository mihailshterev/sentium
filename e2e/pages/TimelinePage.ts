import type { Locator, Page } from "@playwright/test";
import { expect } from "@playwright/test";

const MONTHS = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
];

export function localToday(): {
  year: number;
  month: number;
  day: number;
  iso: string;
  label: string;
  monthName: string;
} {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth() + 1;
  const day = now.getDate();
  const iso = `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
  return { year, month, day, iso, label: `${day} ${MONTHS[month - 1]}`, monthName: MONTHS[month - 1] };
}

export class TimelinePage {
  constructor(private readonly page: Page) {}

  async goto(query = ""): Promise<void> {
    await this.page.goto(`/timeline${query}`);
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { level: 1, name: "Timeline" })).toBeVisible();
    await expect(this.monthNode(1)).toBeVisible();
  }

  monthNode(month: number): Locator {
    return this.page.getByTestId(`timeline-month-${month}`);
  }

  get monthNodes(): Locator {
    return this.page.locator("[data-testid^='timeline-month-']");
  }

  get calendar(): Locator {
    return this.page.getByTestId("month-calendar");
  }

  get pinnedZone(): Locator {
    return this.page.getByTestId("pinned-zone");
  }

  get dialog(): Locator {
    return this.page.getByRole("dialog");
  }

  day(iso: string): Locator {
    return this.page.getByTestId(`day-${iso}`);
  }

  chip(title: string): Locator {
    return this.page.getByRole("button", { name: `Edit task: ${title}`, exact: true });
  }

  async openMonth(month: number, monthName: string, year: number): Promise<void> {
    await this.monthNode(month).click();
    await expect(this.page.getByRole("heading", { level: 2, name: `${monthName} ${year}` })).toBeVisible();
  }

  async addTaskOn(dayLabel: string, title: string): Promise<void> {
    await this.page.getByRole("button", { name: `Add task on ${dayLabel}`, exact: true }).click();
    await expect(this.dialog).toBeVisible();
    await this.dialog.getByLabel("Title").fill(title);
    await this.dialog.getByRole("button", { name: "Add task" }).click();
    await expect(this.dialog).toBeHidden();
  }
}
