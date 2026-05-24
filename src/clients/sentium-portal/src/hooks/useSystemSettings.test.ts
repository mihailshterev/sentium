import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSystemSettings } from "./useSystemSettings";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { SystemSettings } from "../types/agentConfig";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockSettings: SystemSettings = {
  userHarnessPrompt: "You are a helpful assistant.",
  isBuiltInHarnessEnabled: true,
  updatedAt: "2025-01-01T00:00:00Z",
  updatedBy: null,
};

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchSystemSettings").mockResolvedValue(mockSettings);
  vi.spyOn(agentRuntimeService, "updateSystemSettings").mockResolvedValue({
    ...mockSettings,
    userHarnessPrompt: "Updated prompt",
  });
});

describe("useSystemSettings fetching", () => {
  it("settings is undefined before data arrives", () => {
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    expect(result.current.settings).toBeUndefined();
    expect(result.current.isLoading).toBe(true);
  });

  it("populates settings after fetch resolves", async () => {
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.settings).toEqual(mockSettings);
  });

  it("exposes error when fetch fails (query settles to error state)", async () => {
    vi.spyOn(agentRuntimeService, "fetchSystemSettings").mockRejectedValue(new Error("Server error"));
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.error).not.toBeNull(), { timeout: 5000 });
    expect(result.current.error).toBeTruthy();
  });
});

describe("useSystemSettings save mutation", () => {
  it("isSaving is false initially", async () => {
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(result.current.isSaving).toBe(false);
  });

  it("isSaveSuccess is false before any save", async () => {
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(result.current.isSaveSuccess).toBe(false);
  });

  it("isSaveError is false before any save", async () => {
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(result.current.isSaveError).toBe(false);
  });

  it("save is a function", async () => {
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(typeof result.current.save).toBe("function");
  });

  it("resetSave is a function", async () => {
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(typeof result.current.resetSave).toBe("function");
  });

  it("updates cache after successful save via setQueryData", async () => {
    const spy = vi.spyOn(agentRuntimeService, "updateSystemSettings");
    const { result } = renderHook(() => useSystemSettings(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.settings).toEqual(mockSettings));

    act(() => {
      result.current.save({
        userHarnessPrompt: "Updated prompt",
        isBuiltInHarnessEnabled: false,
      });
    });

    await waitFor(() => expect(result.current.isSaveSuccess).toBe(true));
    expect(spy).toHaveBeenCalledWith({
      userHarnessPrompt: "Updated prompt",
      isBuiltInHarnessEnabled: false,
    });
    expect(result.current.settings?.userHarnessPrompt).toBe("Updated prompt");
  });
});
