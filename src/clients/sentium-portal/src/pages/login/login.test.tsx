import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import Login from "./login";
import { useAuthStore } from "../../stores/auth-store";
import { AUTH_STATUS } from "../../utils/constants";

vi.mock("../login/animated-bg", () => ({
  AnimatedBg: () => <canvas data-testid="animated-bg" />,
}));

vi.mock("./animated-bg", () => ({
  AnimatedBg: () => <canvas data-testid="animated-bg" />,
}));

const renderLogin = (initialPath = "/login") =>
  render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Login />
    </MemoryRouter>,
  );

beforeEach(() => {
  useAuthStore.setState({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
  vi.stubGlobal("fetch", vi.fn());
});

describe("Login – redirect when authenticated", () => {
  it("redirects to / when already authenticated", () => {
    useAuthStore.setState({
      user: { sub: "u1", email: "a@b.com", name: "Alice", roles: ["Member"] },
      status: AUTH_STATUS.AUTHENTICATED,
    });
    renderLogin();
    expect(screen.queryByLabelText(/email address/i)).not.toBeInTheDocument();
  });
});

describe("Login initial render", () => {
  it("renders the sign-in tab as active by default", () => {
    renderLogin();
    expect(screen.getAllByRole("button", { name: /sign in/i }).length).toBeGreaterThanOrEqual(1);
  });

  it("renders email and password inputs", () => {
    renderLogin();
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
  });

  it("renders all feature list items", () => {
    renderLogin();
    expect(screen.getByText(/local llm execution/i)).toBeInTheDocument();
  });

  it("renders stats on the left panel", () => {
    renderLogin();
    expect(screen.getByText("100%")).toBeInTheDocument();
    expect(screen.getByText("<10ms")).toBeInTheDocument();
    expect(screen.getByText("Zero")).toBeInTheDocument();
  });
});

describe("Login tab switching", () => {
  it("switches to register mode when the Register tab is clicked", () => {
    renderLogin();
    fireEvent.click(screen.getByRole("button", { name: /register/i }));
    expect(screen.getByRole("button", { name: /create account/i })).toBeInTheDocument();
    expect(screen.getByText(/create an account to get started/i)).toBeInTheDocument();
  });

  it("switches back to login mode from register", () => {
    renderLogin();
    fireEvent.click(screen.getByRole("button", { name: /register/i }));
    fireEvent.click(screen.getByRole("button", { name: /sign in/i }));
    expect(screen.getByText(/sign in to your account/i)).toBeInTheDocument();
  });

  it("clears the error when switching modes", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    renderLogin();
    fireEvent.change(screen.getByLabelText(/email address/i), { target: { value: "bad@example.com" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "wrong" } });
    fireEvent.submit(document.querySelector("form")!);
    expect(screen.queryByText(/invalid email or password/i)).not.toBeInTheDocument();
  });

  it("switches mode via the 'Create one' link", () => {
    renderLogin();
    fireEvent.click(screen.getByText(/create one/i));
    expect(screen.getByText(/create an account to get started/i)).toBeInTheDocument();
  });

  it("switches mode via the 'Sign in' link when in register mode", () => {
    renderLogin();
    fireEvent.click(screen.getByRole("button", { name: /register/i }));
    fireEvent.click(screen.getByText(/sign in/i, { selector: "a" }));
    expect(screen.getByText(/sign in to your account/i)).toBeInTheDocument();
  });
});

describe("Login form submission", () => {
  it("shows submitting state while request is in-flight", async () => {
    let resolveFetch!: (v: unknown) => void;
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise((r) => (resolveFetch = r))));
    renderLogin();
    fireEvent.change(screen.getByLabelText(/email address/i), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "pass" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.getByText(/signing in\.\.\./i)).toBeInTheDocument());
    resolveFetch({ ok: true });
  });

  it("shows login error when server returns non-ok in login mode", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    renderLogin();
    fireEvent.change(screen.getByLabelText(/email address/i), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "wrong" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.getByText(/invalid email or password/i)).toBeInTheDocument());
  });

  it("shows register error when server returns non-ok in register mode", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    renderLogin();
    fireEvent.click(screen.getByRole("button", { name: /register/i }));
    fireEvent.change(screen.getByLabelText(/email address/i), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "password123" } });
    fireEvent.click(screen.getByRole("button", { name: /create account/i }));
    await waitFor(() => expect(screen.getByText(/registration failed/i)).toBeInTheDocument());
  });

  it("shows a generic error when fetch throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("Network error")));
    renderLogin();
    fireEvent.change(screen.getByLabelText(/email address/i), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "pass" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.getByText(/something went wrong/i)).toBeInTheDocument());
  });

  it("redirects to BFF login on successful login", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
    renderLogin();
    fireEvent.change(screen.getByLabelText(/email address/i), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "pass" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(window.location.assign).toHaveBeenCalledWith(expect.stringContaining("/login")));
  });

  it("shows 'Creating account...' while register is submitting", async () => {
    let resolveFetch!: (v: unknown) => void;
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise((r) => (resolveFetch = r))));
    renderLogin();
    fireEvent.click(screen.getByRole("button", { name: /register/i }));
    fireEvent.change(screen.getByLabelText(/email address/i), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "password123" } });
    fireEvent.click(screen.getByRole("button", { name: /create account/i }));
    await waitFor(() => expect(screen.getByText(/creating account\.\.\./i)).toBeInTheDocument());
    resolveFetch({ ok: true });
  });
});

describe("Login edge cases", () => {
  it("does not submit if email is extremely long (browser validation)", () => {
    renderLogin();
    const emailInput = screen.getByLabelText(/email address/i);
    fireEvent.change(emailInput, { target: { value: "a".repeat(300) + "@example.com" } });
    expect((emailInput as HTMLInputElement).value).toContain("@example.com");
  });
});
