import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AccountMenu } from "./account-menu";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { resolveTheme, useThemeStore } from "@/shared/ui/state/theme-store";
import { AUTH_STATUS } from "@/shared/utils/constants";

const setUser = (roles: string[], name = "Test User") =>
  useAuthStore.setState({ user: { sub: "u1", name, roles }, status: AUTH_STATUS.AUTHENTICATED });

const renderMenu = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <AccountMenu />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

const openMenu = async () => {
  await userEvent.click(screen.getByRole("button", { name: /account menu/i }));
  return screen.getByRole("menu", { name: "Account" });
};

beforeEach(() => {
  // No profile from the server, so the name comes from the auth session.
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404, headers: new Headers(), json: async () => ({}) }));
  useAuthStore.setState({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
});

describe("AccountMenu", () => {
  it("shows who is signed in on the trigger", () => {
    setUser(["Member"], "Alice Smith");
    renderMenu();
    expect(screen.getByRole("button", { name: "Account menu, Alice Smith" })).toHaveAttribute("aria-expanded", "false");
  });

  it("opens with focus on the first item and shows the role", async () => {
    setUser(["Sovereign"]);
    renderMenu();
    const menu = await openMenu();
    expect(screen.getByRole("menuitem", { name: "Profile" })).toHaveFocus();
    expect(menu).toHaveTextContent("Sovereign");
  });

  it("lists Users for Sovereigns only", async () => {
    setUser(["Member"]);
    const { unmount } = renderMenu();
    await openMenu();
    expect(screen.queryByRole("menuitem", { name: "Users" })).not.toBeInTheDocument();
    unmount();

    setUser(["Sovereign"]);
    renderMenu();
    await openMenu();
    expect(screen.getByRole("menuitem", { name: "Users" })).toHaveAttribute("href", "/users");
  });

  it("moves through items with the arrow keys and closes on Escape", async () => {
    setUser(["Member"]);
    renderMenu();
    await openMenu();
    await userEvent.keyboard("{ArrowDown}");
    expect(screen.getByRole("menuitem", { name: "Inbox" })).toHaveFocus();
    await userEvent.keyboard("{ArrowUp}{ArrowUp}");
    expect(screen.getByRole("menuitem", { name: "Sign out" })).toHaveFocus();
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /account menu/i })).toHaveFocus();
  });

  it("switches the theme", async () => {
    useThemeStore.setState({ preference: "dark" });
    setUser(["Member"]);
    renderMenu();
    await openMenu();
    await userEvent.click(screen.getByRole("menuitem", { name: "Switch to light theme" }));
    expect(resolveTheme(useThemeStore.getState().preference)).toBe("light");
    expect(screen.getByRole("menuitem", { name: "Switch to dark theme" })).toBeInTheDocument();
  });

  it("signs out", async () => {
    const logout = vi.fn().mockResolvedValue(undefined);
    useAuthStore.setState({
      user: { sub: "u1", name: "User", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
      logout: logout as unknown as () => Promise<void>,
    });
    renderMenu();
    await openMenu();
    await userEvent.click(screen.getByRole("menuitem", { name: "Sign out" }));
    expect(logout).toHaveBeenCalledOnce();
  });
});
