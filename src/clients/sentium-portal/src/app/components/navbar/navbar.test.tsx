import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Navbar } from "./navbar";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { useUiStore } from "@/shared/ui/state/ui-store";
import { AUTH_STATUS } from "@/shared/utils/constants";

vi.mock("./navbar.module.css", () => ({
  default: new Proxy({} as Record<string, string>, { get: (_, key) => String(key) }),
}));

const setUser = (roles: string[], overrides: Partial<{ name: string }> = {}) => {
  useAuthStore.setState({
    user: {
      sub: "u1",
      name: overrides.name ?? "Test User",
      roles,
    },
    status: AUTH_STATUS.AUTHENTICATED,
  });
};

const renderNav = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Type": "application/json" }),
      json: async () => ({ id: "u1", userName: "user", firstName: "Test", lastName: "User" }),
    }),
  );
  useAuthStore.setState({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
  useUiStore.setState({ sidebarCollapsed: false });
});

describe("Navbar brand", () => {
  it("renders the Sentium wordmark", () => {
    setUser([]);
    renderNav();
    expect(screen.getByText("Sentium")).toBeInTheDocument();
  });

  it("labels the navigation landmark", () => {
    setUser([]);
    renderNav();
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("renders the version footer", () => {
    setUser([]);
    renderNav();
    expect(screen.getByText(/v1\.0\.0/)).toBeInTheDocument();
  });
});

describe("Navbar user display", () => {
  it("shows the user name", () => {
    setUser([], { name: "Alice Smith" });
    renderNav();
    expect(screen.getByText("Alice Smith")).toBeInTheDocument();
  });

  it("shows the username as the name", () => {
    setUser([], { name: "alice" });
    renderNav();
    expect(screen.getByText("alice")).toBeInTheDocument();
  });

  it("renders the display span when name is blank", () => {
    setUser([], { name: "   " });
    renderNav();
    const displaySpan = document.querySelector(".userDisplayName");
    expect(displaySpan).toBeInTheDocument();
  });
});

describe("Navbar role badge", () => {
  it("renders Sovereign role badge for Sovereign users", () => {
    setUser(["Sovereign"]);
    renderNav();
    expect(screen.getByText("Sovereign")).toBeInTheDocument();
  });

  it("renders Member role badge for Member users", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByText("Member")).toBeInTheDocument();
  });

  it("renders no role badge when user has no recognised roles", () => {
    setUser([]);
    renderNav();
    expect(screen.queryByText(/^(Sovereign|Member)$/)).not.toBeInTheDocument();
  });
});

describe("Navbar navigation links", () => {
  it("shows Dashboard link for all users", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /dashboard/i })).toBeInTheDocument();
  });

  it("shows Assistant link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /assistant/i })).toBeInTheDocument();
  });

  it("shows Orchestration link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /orchestration/i })).toBeInTheDocument();
  });

  it("shows Workflows link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /workflows/i })).toBeInTheDocument();
  });

  it("shows Agents link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /^agents$/i })).toBeInTheDocument();
  });

  it("shows Models link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /models/i })).toBeInTheDocument();
  });

  it("shows System link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /system/i })).toBeInTheDocument();
  });

  it("shows Settings link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /settings/i })).toBeInTheDocument();
  });

  it("shows Inventory link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /inventory/i })).toHaveAttribute("href", "/inventory");
  });

  it("shows Vigil link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /vigil/i })).toBeInTheDocument();
  });
});

describe("Navbar sovereign-only links", () => {
  it("shows Users link for Sovereign users", () => {
    setUser(["Sovereign"]);
    renderNav();
    const links = screen.getAllByRole("link");
    const usersLink = links.find((l) => l.getAttribute("href") === "/users");
    expect(usersLink).toBeInTheDocument();
  });

  it("hides Users link for Member users", () => {
    setUser(["Member"]);
    renderNav();
    const links = screen.getAllByRole("link");
    const usersLink = links.find((l) => l.getAttribute("href") === "/users");
    expect(usersLink).toBeUndefined();
  });

  it("hides Users link for users without the Sovereign role", () => {
    setUser([]);
    renderNav();
    const links = screen.getAllByRole("link");
    const usersLink = links.find((l) => l.getAttribute("href") === "/users");
    expect(usersLink).toBeUndefined();
  });
});

describe("Navbar logout", () => {
  it("renders a Log out button", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("button", { name: /log out/i })).toBeInTheDocument();
  });

  it("calls logout from auth store when Log out is clicked", async () => {
    const logoutSpy = vi.fn();
    useAuthStore.setState({
      user: { sub: "u1", name: "User", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
      logout: logoutSpy as unknown as () => Promise<void>,
    });

    renderNav();
    await userEvent.click(screen.getByRole("button", { name: /log out/i }));
    expect(logoutSpy).toHaveBeenCalledOnce();
  });

  it("shows the username as displayName", () => {
    useAuthStore.setState({
      user: { sub: "u1", name: "user", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
    });
    renderNav();
    expect(screen.getByText("user")).toBeInTheDocument();
  });
});

describe("Navbar collapse", () => {
  it("collapses to the icon rail from the toggle button", async () => {
    setUser([]);
    renderNav();
    await userEvent.click(screen.getByRole("button", { name: "Collapse sidebar" }));
    expect(useUiStore.getState().sidebarCollapsed).toBe(true);
    expect(screen.getByRole("navigation")).toHaveAttribute("data-collapsed");
    expect(screen.getByRole("button", { name: "Expand sidebar" })).toBeInTheDocument();
  });

  it("toggles with Ctrl+B", async () => {
    setUser([]);
    renderNav();
    await userEvent.keyboard("{Control>}b{/Control}");
    expect(useUiStore.getState().sidebarCollapsed).toBe(true);
  });

  it("keeps link names accessible while collapsed", () => {
    useUiStore.setState({ sidebarCollapsed: true });
    setUser([]);
    renderNav();
    expect(screen.getByRole("link", { name: /dashboard/i })).toBeInTheDocument();
  });
});
