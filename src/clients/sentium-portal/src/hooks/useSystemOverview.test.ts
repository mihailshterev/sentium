import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useSystemOverview from "./useSystemOverview";
import * as watchdogService from "../services/watchdog.service";

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const overview = { healthy: 5, total: 7 } as never;

beforeEach(() => {
  vi.spyOn(watchdogService, "fetchSystemOverview").mockResolvedValue(overview);
});

describe("useSystemOverview", () => {
  it("is loading initially with no overview", () => {
    const { result } = renderHook(() => useSystemOverview(), { wrapper: createWrapper() });
    expect(result.current.overview).toBeUndefined();
    expect(result.current.isLoading).toBe(true);
  });

  it("returns the overview after a successful fetch", async () => {
    const { result } = renderHook(() => useSystemOverview(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.overview).toEqual(overview);
  });

  it("surfaces an error and leaves overview undefined", async () => {
    vi.spyOn(watchdogService, "fetchSystemOverview").mockRejectedValueOnce(new Error("x"));
    const { result } = renderHook(() => useSystemOverview(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.overview).toBeUndefined();
    expect(result.current.error).toBeTruthy();
  });
});
