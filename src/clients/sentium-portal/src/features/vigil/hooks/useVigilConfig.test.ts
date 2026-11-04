import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useVigilConfig } from "./useVigilConfig";
import * as vigilService from "@/features/vigil/api/vigil.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const config = { intervalSeconds: 30 } as never;

beforeEach(() => {
  vi.spyOn(vigilService, "fetchVigilConfig").mockResolvedValue(config);
  vi.spyOn(vigilService, "updateVigilConfig").mockResolvedValue({ intervalSeconds: 60 } as never);
});

describe("useVigilConfig", () => {
  it("does not fetch while disabled", () => {
    const spy = vi.spyOn(vigilService, "fetchVigilConfig");
    renderHook(() => useVigilConfig(false), { wrapper: createWrapper() });
    expect(spy).not.toHaveBeenCalled();
  });

  it("loads the config when enabled", async () => {
    const { result } = renderHook(() => useVigilConfig(true), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.config).toEqual(config);
  });

  it("saves the config and updates the cache", async () => {
    const { result } = renderHook(() => useVigilConfig(true), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.saveConfig({ intervalSeconds: 60 } as never);
    });

    await waitFor(() => expect(result.current.isSaveSuccess).toBe(true));
    expect(result.current.config).toEqual({ intervalSeconds: 60 });
  });
});
