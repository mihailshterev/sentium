import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useIncidents from "./useIncidents";
import * as watchdogService from "../services/watchdog.service";

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const incident = { id: "i1" } as never;

beforeEach(() => {
  vi.spyOn(watchdogService, "fetchIncidents").mockResolvedValue([incident]);
});

describe("useIncidents", () => {
  it("returns an empty list while loading", () => {
    const { result } = renderHook(() => useIncidents(), { wrapper: createWrapper() });
    expect(result.current.incidents).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("returns incidents after a successful fetch", async () => {
    const { result } = renderHook(() => useIncidents(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.incidents).toEqual([incident]);
  });

  it("falls back to an empty list on error", async () => {
    vi.spyOn(watchdogService, "fetchIncidents").mockRejectedValueOnce(new Error("nope"));
    const { result } = renderHook(() => useIncidents(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.incidents).toEqual([]);
    expect(result.current.error).toBeTruthy();
  });
});
