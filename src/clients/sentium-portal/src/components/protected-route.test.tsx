import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import ProtectedRoute from "./protected-route";
import { useAuthStore } from "../stores/auth-store";
import { AUTH_STATUS } from "../utils/constants";

const CHILD_TEXT = "Secret content";
const Child = () => <p>{CHILD_TEXT}</p>;

const setStatus = (status: string) => {
  useAuthStore.setState({ status: status as ReturnType<typeof useAuthStore.getState>["status"] });
};

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  useAuthStore.setState({ user: null, status: AUTH_STATUS.IDLE });
});

describe("ProtectedRoute IDLE / CHECKING states", () => {
  it("renders null when status is IDLE", () => {
    setStatus(AUTH_STATUS.IDLE);
    const { container } = render(
      <MemoryRouter>
        <ProtectedRoute>
          <Child />
        </ProtectedRoute>
      </MemoryRouter>,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("renders null when status is CHECKING", () => {
    setStatus(AUTH_STATUS.CHECKING);
    const { container } = render(
      <MemoryRouter>
        <ProtectedRoute>
          <Child />
        </ProtectedRoute>
      </MemoryRouter>,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("does NOT render children while checking", () => {
    setStatus(AUTH_STATUS.CHECKING);
    render(
      <MemoryRouter>
        <ProtectedRoute>
          <Child />
        </ProtectedRoute>
      </MemoryRouter>,
    );
    expect(screen.queryByText(CHILD_TEXT)).not.toBeInTheDocument();
  });
});

describe("ProtectedRoute UNAUTHENTICATED state", () => {
  it("redirects to /login when unauthenticated", () => {
    setStatus(AUTH_STATUS.UNAUTHENTICATED);
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <ProtectedRoute>
          <Child />
        </ProtectedRoute>
      </MemoryRouter>,
    );
    expect(screen.queryByText(CHILD_TEXT)).not.toBeInTheDocument();
  });
});

describe("ProtectedRoute AUTHENTICATED state", () => {
  it("renders children when authenticated", () => {
    useAuthStore.setState({
      user: { sub: "u1", email: "alice@example.com", name: "Alice", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
    });
    render(
      <MemoryRouter>
        <ProtectedRoute>
          <Child />
        </ProtectedRoute>
      </MemoryRouter>,
    );
    expect(screen.getByText(CHILD_TEXT)).toBeInTheDocument();
  });

  it("renders multiple children when authenticated", () => {
    useAuthStore.setState({
      user: { sub: "u1", email: "alice@example.com", name: "Alice", roles: [] },
      status: AUTH_STATUS.AUTHENTICATED,
    });
    render(
      <MemoryRouter>
        <ProtectedRoute>
          <p>Child A</p>
          <p>Child B</p>
        </ProtectedRoute>
      </MemoryRouter>,
    );
    expect(screen.getByText("Child A")).toBeInTheDocument();
    expect(screen.getByText("Child B")).toBeInTheDocument();
  });
});
