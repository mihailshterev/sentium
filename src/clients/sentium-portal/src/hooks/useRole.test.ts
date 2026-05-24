import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useAuthStore } from "../stores/auth-store";
import { AUTH_STATUS } from "../utils/constants";
import { useRole } from "./useRole";

const setUser = (roles: string[]) => {
  act(() => {
    useAuthStore.setState({
      user: { sub: "u1", email: "test@example.com", name: "Tester", roles },
      status: AUTH_STATUS.AUTHENTICATED,
    });
  });
};

const clearUser = () => {
  act(() => {
    useAuthStore.setState({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
  });
};

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  clearUser();
});

describe("useRole no user", () => {
  it("returns empty roles array", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.roles).toHaveLength(0);
  });

  it("highestRole is undefined", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.highestRole).toBeUndefined();
  });

  it("isSovereign is false", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isSovereign).toBe(false);
  });

  it("isMemberOrAbove is false", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isMemberOrAbove).toBe(false);
  });

  it("isAuthenticated is false", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isAuthenticated).toBe(false);
  });
});

describe("useRole Guest", () => {
  beforeEach(() => setUser(["Guest"]));

  it("highestRole is 'Guest'", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.highestRole).toBe("Guest");
  });

  it("isSovereign is false", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isSovereign).toBe(false);
  });

  it("isMemberOrAbove is false", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isMemberOrAbove).toBe(false);
  });

  it("isAuthenticated is true", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isAuthenticated).toBe(true);
  });

  it("hasRole('Guest') is true", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.hasRole("Guest")).toBe(true);
  });

  it("hasRole('Member') is false", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.hasRole("Member")).toBe(false);
  });
});

describe("useRole Member", () => {
  beforeEach(() => setUser(["Member"]));

  it("highestRole is 'Member'", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.highestRole).toBe("Member");
  });

  it("isMemberOrAbove is true", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isMemberOrAbove).toBe(true);
  });

  it("isSovereign is false", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isSovereign).toBe(false);
  });
});

describe("useRole Sovereign", () => {
  beforeEach(() => setUser(["Sovereign"]));

  it("highestRole is 'Sovereign'", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.highestRole).toBe("Sovereign");
  });

  it("isSovereign is true", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isSovereign).toBe(true);
  });

  it("isMemberOrAbove is true", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isMemberOrAbove).toBe(true);
  });
});

describe("useRole multiple roles (Member + Guest)", () => {
  beforeEach(() => setUser(["Member", "Guest"]));

  it("highestRole picks Member over Guest", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.highestRole).toBe("Member");
  });
});

describe("useRole multiple roles (Sovereign + Member + Guest)", () => {
  beforeEach(() => setUser(["Sovereign", "Member", "Guest"]));

  it("highestRole picks Sovereign", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.highestRole).toBe("Sovereign");
  });

  it("isSovereign is true", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isSovereign).toBe(true);
  });
});

describe("useRole unknown / unlisted roles", () => {
  beforeEach(() => setUser(["UnknownRole"]));

  it("highestRole is undefined for unlisted role", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.highestRole).toBeUndefined();
  });

  it("isAuthenticated is still true (user has roles)", () => {
    const { result } = renderHook(() => useRole());
    expect(result.current.isAuthenticated).toBe(true);
  });
});
