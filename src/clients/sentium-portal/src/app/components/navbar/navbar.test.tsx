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

const renderNav = (path = "/") => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[path]}>
        <Navbar />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

const linkTo = (href: string) => screen.queryAllByRole("link").find((l) => l.getAttribute("href") === href);

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
  useUiStore.setState({ sidebarCollapsed: false, collapsedNavGroups: [] });
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

describe("Navbar navigation links", () => {
  it("starts with Home", () => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name: "Home" })).toHaveAttribute("href", "/");
  });

  it.each([
    ["Assistant", "/assistant"],
    ["Orchestration", "/orchestration"],
    ["Workflows", "/workflows"],
    ["Agents", "/agents"],
    ["Models", "/models"],
    ["Inventory", "/inventory"],
    ["Vigil", "/vigil"],
    ["System", "/system"],
  ])("links to %s", (name, href) => {
    setUser(["Member"]);
    renderNav();
    expect(screen.getByRole("link", { name })).toHaveAttribute("href", href);
  });

  it("groups links by what the user is doing", () => {
    setUser(["Member"]);
    renderNav();
    for (const group of ["Work", "Knowledge", "Build", "Operate"]) {
      expect(screen.getByRole("button", { name: group })).toHaveAttribute("aria-expanded", "true");
    }
  });

  it("leaves account pages to the account menu", () => {
    setUser(["Sovereign"]);
    renderNav();
    expect(linkTo("/settings")).toBeUndefined();
    expect(linkTo("/users")).toBeUndefined();
    expect(linkTo("/profile")).toBeUndefined();
  });

  it("shows MCP Servers to Sovereigns only", () => {
    setUser(["Member"]);
    const { unmount } = renderNav();
    expect(linkTo("/mcp")).toBeUndefined();
    unmount();

    setUser(["Sovereign"]);
    renderNav();
    expect(linkTo("/mcp")).toBeDefined();
  });
});

describe("Navbar groups", () => {
  it("folds a group away and remembers it", async () => {
    setUser([]);
    renderNav();
    await userEvent.click(screen.getByRole("button", { name: "Build" }));
    expect(screen.getByRole("button", { name: "Build" })).toHaveAttribute("aria-expanded", "false");
    expect(linkTo("/agents")).toBeUndefined();
    expect(useUiStore.getState().collapsedNavGroups).toEqual(["build"]);
  });

  it("keeps the group of the current page open", () => {
    useUiStore.setState({ collapsedNavGroups: ["build"] });
    setUser([]);
    renderNav("/agents/123");
    expect(screen.getByRole("button", { name: "Build" })).toHaveAttribute("aria-expanded", "true");
    expect(linkTo("/agents")).toBeDefined();
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

  it("lists every link in the rail, even from folded groups", () => {
    useUiStore.setState({ sidebarCollapsed: true, collapsedNavGroups: ["build"] });
    setUser([]);
    renderNav();
    expect(screen.getByRole("link", { name: "Home" })).toBeInTheDocument();
    expect(linkTo("/agents")).toBeDefined();
  });
});
