import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import App from "./App";
import { useAuthStore } from "./stores/auth-store";
import { AUTH_STATUS } from "./utils/constants";

vi.mock("../routes/routes", () => ({
  routes: [],
}));

const renderApp = () =>
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  useAuthStore.setState({ user: null, status: AUTH_STATUS.IDLE });
});

describe("App loading state", () => {
  it("shows a loading spinner while auth status is IDLE", () => {
    useAuthStore.setState({ user: null, status: AUTH_STATUS.IDLE });
    renderApp();
    const spinner = document.querySelector(".animate-spin");
    expect(spinner).toBeInTheDocument();
  });

  it("shows a loading spinner while auth status is CHECKING", () => {
    useAuthStore.setState({ user: null, status: AUTH_STATUS.CHECKING });
    renderApp();
    const spinner = document.querySelector(".animate-spin");
    expect(spinner).toBeInTheDocument();
  });

  it("the loading container occupies full viewport height", () => {
    useAuthStore.setState({ user: null, status: AUTH_STATUS.IDLE });
    renderApp();
    const container = document.querySelector("[style]") as HTMLElement;
    expect(container?.style?.height).toBe("100vh");
  });
});

describe("App auth check on mount", () => {
  it("calls checkAuth on initial render", async () => {
    const checkAuth = vi.fn().mockResolvedValue(undefined);
    useAuthStore.setState({
      user: null,
      status: AUTH_STATUS.IDLE,
      checkAuth: checkAuth as unknown as () => Promise<void>,
    });

    renderApp();

    await waitFor(() => expect(checkAuth).toHaveBeenCalledTimes(1));
  });
});

describe("App authenticated state", () => {
  it("does NOT show the loading spinner when authenticated", () => {
    useAuthStore.setState({
      user: { sub: "u1", email: "alice@example.com", name: "Alice", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
    });
    renderApp();
    expect(document.querySelector(".animate-spin")).not.toBeInTheDocument();
  });
});
