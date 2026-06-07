import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Layout from "./layout";
import { useAuthStore } from "../../stores/auth-store";
import { AUTH_STATUS } from "../../utils/constants";

beforeEach(() => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Type": "application/json" }),
      json: async () => ({}),
    }),
  );
  useAuthStore.setState({
    user: { sub: "u1", email: "user@example.com", name: "User", roles: ["Member"] },
    status: AUTH_STATUS.AUTHENTICATED,
  });
});

describe("Layout", () => {
  it("renders the navbar and the routed outlet child", () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={["/home"]}>
          <Routes>
            <Route element={<Layout />}>
              <Route path="home" element={<div>child page</div>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(screen.getByText("SENTIUM")).toBeInTheDocument();
    expect(screen.getByText("child page")).toBeInTheDocument();
    expect(screen.getByRole("main")).toBeInTheDocument();
  });
});
