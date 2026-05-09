import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useUsers from "./useUsers";
import { identityService } from "../services/identity.service";
import type { UserListItem } from "../services/identity.service";

vi.mock("../services/identity.service", () => ({
  identityService: {
    getUsers: vi.fn(),
    assignRole: vi.fn(),
    removeRole: vi.fn(),
    deleteUser: vi.fn(),
  },
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const alice: UserListItem = {
  id: "u-1",
  email: "alice@example.com",
  firstName: "Alice",
  lastName: "Smith",
  roles: ["Member"],
  isLockedOut: false,
};

const bob: UserListItem = {
  id: "u-2",
  email: "bob@example.com",
  firstName: "Bob",
  lastName: null,
  roles: ["Guest"],
  isLockedOut: true,
};

beforeEach(() => {
  vi.mocked(identityService.getUsers).mockResolvedValue([alice, bob]);
  vi.mocked(identityService.assignRole).mockResolvedValue(undefined);
  vi.mocked(identityService.removeRole).mockResolvedValue(undefined);
  vi.mocked(identityService.deleteUser).mockResolvedValue(undefined);
});

describe("useUsers fetching", () => {
  it("starts with an empty users array while loading", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(result.current.users).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("populates users after fetch resolves", async () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.users).toEqual([alice, bob]);
  });

  it("exposes error when getUsers rejects", async () => {
    vi.mocked(identityService.getUsers).mockRejectedValueOnce(new Error("Forbidden"));
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.users).toEqual([]);
  });

  it("exposes a refetch function", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(typeof result.current.refetch).toBe("function");
  });
});

describe("useUsers mutation state", () => {
  it("isAssigningRole is false initially", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(result.current.isAssigningRole).toBe(false);
  });

  it("isRemovingRole is false initially", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(result.current.isRemovingRole).toBe(false);
  });

  it("isDeletingUser is false initially", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(result.current.isDeletingUser).toBe(false);
  });

  it("assignRole is a function", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(typeof result.current.assignRole).toBe("function");
  });

  it("removeRole is a function", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(typeof result.current.removeRole).toBe("function");
  });

  it("deleteUser is a function", () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    expect(typeof result.current.deleteUser).toBe("function");
  });
});

describe("useUsers assignRole()", () => {
  it("calls identityService.assignRole with the correct payload", async () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const payload = { userId: "u-2", roleName: "Member" };
    await result.current.assignRole(payload);
    expect(identityService.assignRole).toHaveBeenCalledWith(payload);
  });
});

describe("useUsers removeRole()", () => {
  it("calls identityService.removeRole with the correct payload", async () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const payload = { userId: "u-1", roleName: "Member" };
    await result.current.removeRole(payload);
    expect(identityService.removeRole).toHaveBeenCalledWith(payload);
  });
});

describe("useUsers deleteUser()", () => {
  it("calls identityService.deleteUser with the user id", async () => {
    const { result } = renderHook(() => useUsers(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await result.current.deleteUser("u-2");
    expect(identityService.deleteUser).toHaveBeenCalledWith("u-2");
  });
});
