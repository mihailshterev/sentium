import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Navbar from "./navbar";
import { useAuthStore } from "../stores/auth-store";
import { AUTH_STATUS } from "../utils/constants";

vi.mock("./navbar.module.scss", () => ({
  default: new Proxy({} as Record<string, string>, { get: (_, key) => String(key) }),
}));

const setUser = (roles: string[], overrides: Partial<{ name: string; email: string }> = {}) => {
  useAuthStore.setState({
    user: {
      sub: "u1",
      email: overrides.email ?? "user@example.com",
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
      json: async () => ({ id: "u1", email: "user@example.com", firstName: "Test", lastName: "User" }),
    }),
  );
  useAuthStore.setState({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
});

describe("Navbar brand", () => {
  it("renders the SENTIUM brand name", () => {
    setUser([]);
    renderNav();
    expect(screen.getByText("SENTIUM")).toBeInTheDocument();
  });

  it("renders the ONLINE status badge", () => {
    setUser([]);
    renderNav();
    expect(screen.getByText("ONLINE")).toBeInTheDocument();
  });

  it("renders the version footer", () => {
    setUser([]);
    renderNav();
    expect(screen.getByText(/v1\.0\.0/)).toBeInTheDocument();
  });
});

describe("Navbar user display", () => {
  it("shows display name when name differs from email", () => {
    setUser([], { name: "Alice Smith", email: "alice@example.com" });
    renderNav();
    expect(screen.getByText("Alice Smith")).toBeInTheDocument();
  });

  it("falls back to email when name equals email", () => {
    setUser([], { name: "alice@example.com", email: "alice@example.com" });
    renderNav();
    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
  });

  it("shows whitespace name when name is blank (not equal to email)", () => {
    setUser([], { name: "   ", email: "alice@example.com" });
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

  it("shows Watchdog link", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: /watchdog/i })).toBeInTheDocument();
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
      user: { sub: "u1", email: "user@example.com", name: "User", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
      logout: logoutSpy as unknown as () => Promise<void>,
    });

    renderNav();
    await userEvent.click(screen.getByRole("button", { name: /log out/i }));
    expect(logoutSpy).toHaveBeenCalledOnce();
  });

  it("shows email as displayName when user name equals email", () => {
    useAuthStore.setState({
      user: { sub: "u1", email: "user@example.com", name: "user@example.com", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
    });
    renderNav();
    expect(screen.getByText("user@example.com")).toBeInTheDocument();
  });
});
