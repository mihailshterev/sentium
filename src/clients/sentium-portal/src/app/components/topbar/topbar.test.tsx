import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Topbar } from "./topbar";
import { OPEN_COMMAND_PALETTE_EVENT } from "../command-palette/open-command-palette";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { AUTH_STATUS } from "@/shared/utils/constants";

vi.mock("@/features/notifications", () => ({
  NotificationBell: () => <button type="button">Notifications</button>,
}));

const renderTopbar = (path: string) => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[path]}>
        <Topbar />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404, headers: new Headers(), json: async () => ({}) }));
  useAuthStore.setState({ user: { sub: "u1", name: "User", roles: ["Member"] }, status: AUTH_STATUS.AUTHENTICATED });
});

describe("Topbar", () => {
  it("shows where the user is, linking back up the trail", () => {
    renderTopbar("/workflows/w1");
    const trail = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(trail).toHaveTextContent("Home/Build/Workflows/Workflow");
    expect(screen.getByRole("link", { name: "Workflows" })).toHaveAttribute("href", "/workflows");
    expect(screen.getByText("Workflow")).toHaveAttribute("aria-current", "page");
  });

  it("opens the command palette from the search button", async () => {
    const listener = vi.fn();
    window.addEventListener(OPEN_COMMAND_PALETTE_EVENT, listener);
    renderTopbar("/");
    await userEvent.click(screen.getByRole("button", { name: "Search or jump to" }));
    expect(listener).toHaveBeenCalledOnce();
    window.removeEventListener(OPEN_COMMAND_PALETTE_EVENT, listener);
  });

  it("shows the local time to the minute", () => {
    vi.useFakeTimers({ now: new Date(2026, 10, 22, 17, 49, 15), toFake: ["Date"] });
    try {
      renderTopbar("/");
      const clock = document.querySelector("time");
      expect(clock).toHaveTextContent("17:49");
      expect(clock).not.toHaveTextContent("17:49:15");
    } finally {
      vi.useRealTimers();
    }
  });

  it("holds the notifications and account controls", () => {
    renderTopbar("/");
    expect(screen.getByRole("button", { name: "Notifications" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /account menu/i })).toBeInTheDocument();
  });
});
