import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useProfile from "./useProfile";
import * as identityService from "../services/identity.service";
import type { UserProfile } from "../services/identity.service";

vi.mock("../services/identity.service", () => ({
  getMe: vi.fn(),
  updateMe: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockProfile: UserProfile = {
  id: "u-1",
  email: "alice@example.com",
  firstName: "Alice",
  lastName: "Smith",
};

beforeEach(() => {
  vi.mocked(identityService.getMe).mockResolvedValue(mockProfile);
  vi.mocked(identityService.updateMe).mockResolvedValue(undefined);
});

describe("useProfile fetching", () => {
  it("profile is null before data arrives", () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    expect(result.current.profile).toBeNull();
    expect(result.current.isLoading).toBe(true);
  });

  it("populates profile after fetch resolves", async () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.profile).toEqual(mockProfile);
  });
});

describe("useProfile save mutation state", () => {
  it("isSaving is false initially", () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    expect(result.current.isSaving).toBe(false);
  });

  it("isSaveSuccess is false initially", () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    expect(result.current.isSaveSuccess).toBe(false);
  });

  it("saveError is null initially", () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    expect(result.current.saveError).toBeNull();
  });

  it("updateProfile is a function", () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    expect(typeof result.current.updateProfile).toBe("function");
  });

  it("resetSave is a function", () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    expect(typeof result.current.resetSave).toBe("function");
  });
});

describe("useProfile updateProfile()", () => {
  it("calls updateMe with the payload", async () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    const payload = { firstName: "Bob", lastName: "Jones", email: "bob@example.com" };
    await act(async () => {
      await result.current.updateProfile(payload);
    });
    expect(identityService.updateMe).toHaveBeenCalledWith(payload);
  });

  it("updates cache optimistically so profile reflects new values", async () => {
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const payload = { firstName: "Bob", lastName: "Jones", email: "bob@example.com" };
    await act(async () => {
      await result.current.updateProfile(payload);
    });

    await waitFor(() => expect(result.current.isSaveSuccess).toBe(true));
    expect(result.current.profile?.firstName).toBe("Bob");
  });

  it("exposes saveError when updateMe rejects", async () => {
    vi.mocked(identityService.updateMe).mockRejectedValueOnce(new Error("Conflict"));
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    await act(async () => {
      await result.current.updateProfile({ firstName: "X", lastName: null, email: "x@example.com" }).catch(() => {});
    });
    await waitFor(() => expect(result.current.saveError).not.toBeNull());
  });

  it("does not update cache when prev is undefined (empty cache)", async () => {
    vi.mocked(identityService.getMe).mockImplementation(() => new Promise(() => {}));
    const { result } = renderHook(() => useProfile(), { wrapper: createWrapper() });
    const payload = { firstName: "Bob", lastName: null, email: "bob@example.com" };
    await act(async () => {
      await result.current.updateProfile(payload);
    });
    expect(result.current.profile).toBeNull();
  });
});
