import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSentinelSettings } from "./useSentinelSettings";
import * as sentinelService from "../services/sentinel.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const settings = { enabled: true } as never;

beforeEach(() => {
  vi.spyOn(sentinelService, "fetchPdpSettings").mockResolvedValue(settings);
  vi.spyOn(sentinelService, "updatePdpSettings").mockResolvedValue({ enabled: false } as never);
});

describe("useSentinelSettings", () => {
  it("loads the PDP settings", async () => {
    const { result } = renderHook(() => useSentinelSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.settings).toEqual(settings);
  });

  it("updates the settings and writes them into the cache", async () => {
    const { result } = renderHook(() => useSentinelSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.updateSettings({ enabled: false } as never);
    });

    await waitFor(() => expect(result.current.settings).toEqual({ enabled: false }));
    expect(sentinelService.updatePdpSettings).toHaveBeenCalled();
  });
});
