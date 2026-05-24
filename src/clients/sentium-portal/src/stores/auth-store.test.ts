import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useAuthStore } from "./auth-store";
import { AUTH_STATUS } from "../utils/constants";

beforeEach(() => {
  act(() => {
    useAuthStore.setState({ user: null, status: AUTH_STATUS.IDLE });
  });

  vi.stubGlobal("fetch", vi.fn());
});

describe("useAuthStore initial state", () => {
  it("starts with null user", () => {
    const { result } = renderHook(() => useAuthStore());
    expect(result.current.user).toBeNull();
  });

  it("starts with IDLE status", () => {
    const { result } = renderHook(() => useAuthStore());
    expect(result.current.status).toBe(AUTH_STATUS.IDLE);
  });
});

describe("checkAuth()", () => {
  it("sets status to CHECKING then AUTHENTICATED on success", async () => {
    const mockUser = { sub: "u1", email: "alice@example.com", name: "Alice", roles: ["Member"] };

    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => mockUser,
    } as unknown as Response);

    const { result } = renderHook(() => useAuthStore());
    await act(async () => {
      await result.current.checkAuth();
    });

    expect(result.current.status).toBe(AUTH_STATUS.AUTHENTICATED);
    expect(result.current.user).toEqual(mockUser);
  });

  it("sets status to UNAUTHENTICATED when BFF returns non-ok", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 401,
      json: async () => ({}),
    } as unknown as Response);

    const { result } = renderHook(() => useAuthStore());
    await act(async () => {
      await result.current.checkAuth();
    });

    expect(result.current.status).toBe(AUTH_STATUS.UNAUTHENTICATED);
    expect(result.current.user).toBeNull();
  });

  it("sets status to UNAUTHENTICATED when fetch throws", async () => {
    vi.mocked(fetch).mockRejectedValueOnce(new Error("Network error"));

    const { result } = renderHook(() => useAuthStore());
    await act(async () => {
      await result.current.checkAuth();
    });

    expect(result.current.status).toBe(AUTH_STATUS.UNAUTHENTICATED);
    expect(result.current.user).toBeNull();
  });

  it("transitions through CHECKING state before resolving", async () => {
    let resolvePromise!: (value: Response) => void;
    vi.mocked(fetch).mockReturnValueOnce(
      new Promise<Response>((res) => {
        resolvePromise = res;
      }),
    );

    const { result } = renderHook(() => useAuthStore());
    act(() => {
      void result.current.checkAuth();
    });

    expect(result.current.status).toBe(AUTH_STATUS.CHECKING);

    await act(async () => {
      resolvePromise({ ok: false, status: 401, json: async () => ({}) } as unknown as Response);
      await waitFor(() => expect(result.current.status).toBe(AUTH_STATUS.UNAUTHENTICATED));
    });
  });
});

describe("login()", () => {
  it("redirects to BFF login with encoded returnUrl when custom url provided", () => {
    const { result } = renderHook(() => useAuthStore());
    act(() => result.current.login("/dashboard"));

    expect(window.location.href).toContain("/bff/login?returnUrl=");
    expect(window.location.href).toContain(encodeURIComponent("http://localhost/dashboard"));
  });

  it("uses window.location.pathname when no returnUrl provided", () => {
    window.location.pathname = "/some-page";
    const { result } = renderHook(() => useAuthStore());
    act(() => result.current.login());

    expect(window.location.href).toContain("/bff/login?returnUrl=");
  });
});

describe("logout()", () => {
  it("clears user and sets status to CHECKING then redirects", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      url: "/bff/logout",
    } as Response);

    act(() => {
      useAuthStore.setState({
        user: { sub: "u1", email: "alice@example.com", name: "Alice", roles: [] },
        status: AUTH_STATUS.AUTHENTICATED,
      });
    });

    const { result } = renderHook(() => useAuthStore());

    await act(async () => {
      await result.current.logout();
    });

    expect(result.current.user).toBeNull();
    expect(result.current.status).toBe(AUTH_STATUS.CHECKING);
  });
});
