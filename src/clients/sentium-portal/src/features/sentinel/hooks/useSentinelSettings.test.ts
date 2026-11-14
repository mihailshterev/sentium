import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSentinelSettings } from "./useSentinelSettings";
import * as registryService from "@/features/registry/api/registry.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const envelope = (value: object) => ({ key: "pdp", value, updatedAt: "x", updatedBy: null }) as never;

beforeEach(() => {
  vi.spyOn(registryService, "fetchSettingsGroup").mockResolvedValue(envelope({ lockdownMode: false }));
  vi.spyOn(registryService, "updateSettingsGroup").mockResolvedValue(envelope({ lockdownMode: true }));
});

describe("useSentinelSettings", () => {
  it("loads the PDP settings from the registry pdp group", async () => {
    const { result } = renderHook(() => useSentinelSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(registryService.fetchSettingsGroup).toHaveBeenCalledWith("pdp");
    expect(result.current.settings).toEqual({ lockdownMode: false });
  });

  it("updates the settings and writes them into the shared cache", async () => {
    const { result } = renderHook(() => useSentinelSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.updateSettings({ lockdownMode: true });
    });

    await waitFor(() => expect(result.current.settings).toEqual({ lockdownMode: true }));
    expect(registryService.updateSettingsGroup).toHaveBeenCalledWith("pdp", { lockdownMode: true });
  });
});
