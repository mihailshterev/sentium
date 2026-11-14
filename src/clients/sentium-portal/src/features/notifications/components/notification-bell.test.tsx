import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { NotificationBell } from "./notification-bell";
import type { Notification } from "@/features/notifications/types/notification";

const service = vi.hoisted(() => ({
  fetchNotifications: vi.fn(),
  fetchUnreadCount: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
}));

vi.mock("@/features/notifications/api/notifications.service", () => service);

const notification = (overrides: Partial<Notification> = {}): Notification => ({
  id: "n1",
  category: "system.incident",
  severity: "Critical",
  title: "Sandbox is down",
  body: "Connection refused",
  link: "/vigil",
  occurrences: 1,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  readAt: null,
  ...overrides,
});

const renderBell = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.clearAllMocks();
  service.fetchUnreadCount.mockResolvedValue({ count: 3 });
  service.fetchNotifications.mockResolvedValue([
    notification(),
    notification({ id: "n2", severity: "Info", title: "Run finished", link: null, readAt: new Date().toISOString() }),
  ]);
  service.markNotificationRead.mockResolvedValue(undefined);
  service.markAllNotificationsRead.mockResolvedValue({ count: 1 });
});

describe("NotificationBell", () => {
  it("shows the unread count", async () => {
    renderBell();

    expect(await screen.findByTestId("notification-badge")).toHaveTextContent("3");
    expect(screen.getByRole("button", { name: /notifications, 3 unread/i })).toBeInTheDocument();
  });

  it("hides the badge when everything is read", async () => {
    service.fetchUnreadCount.mockResolvedValue({ count: 0 });
    renderBell();

    await waitFor(() => expect(service.fetchUnreadCount).toHaveBeenCalled());
    expect(screen.queryByTestId("notification-badge")).not.toBeInTheDocument();
  });

  it("opens the inbox panel with the latest notifications", async () => {
    renderBell();

    await userEvent.click(screen.getByTestId("notification-bell"));

    const panel = await screen.findByRole("dialog", { name: /inbox/i });
    expect(await within(panel).findByText("Sandbox is down")).toBeInTheDocument();
    expect(within(panel).getAllByTestId("notification-item")).toHaveLength(2);
    expect(within(panel).getByRole("link", { name: /open inbox/i })).toHaveAttribute("href", "/inbox");
  });

  it("marks an unread notification read when it is opened", async () => {
    renderBell();
    await userEvent.click(screen.getByTestId("notification-bell"));

    await userEvent.click(await screen.findByText("Sandbox is down"));

    await waitFor(() => expect(service.markNotificationRead).toHaveBeenCalledWith("n1", expect.anything()));
  });

  it("does not mark an already read notification again", async () => {
    renderBell();
    await userEvent.click(screen.getByTestId("notification-bell"));

    await userEvent.click(await screen.findByText("Run finished"));

    expect(service.markNotificationRead).not.toHaveBeenCalled();
  });

  it("marks everything read", async () => {
    renderBell();
    await userEvent.click(screen.getByTestId("notification-bell"));

    await userEvent.click(await screen.findByRole("button", { name: /mark all read/i }));

    await waitFor(() => expect(service.markAllNotificationsRead).toHaveBeenCalled());
  });

  it("closes on Escape", async () => {
    renderBell();
    await userEvent.click(screen.getByTestId("notification-bell"));
    await screen.findByRole("dialog", { name: /inbox/i });

    await userEvent.keyboard("{Escape}");

    expect(screen.queryByRole("dialog", { name: /inbox/i })).not.toBeInTheDocument();
  });
});
