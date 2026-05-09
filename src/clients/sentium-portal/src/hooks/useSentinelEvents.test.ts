import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useSentinelEvents from "./useSentinelEvents";
import * as sentinelService from "../services/sentinel.service";
import type { NetworkEvent } from "../types/sentinel";

vi.mock("../services/sentinel.service", () => ({
  fetchNetworkEvents: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockEvent: NetworkEvent = {
  id: "ev-1",
  source: "firewall",
  action: "block",
  timestamp: "2025-01-01T12:00:00Z",
  origH: "10.0.0.1",
  respH: "8.8.8.8",
  proto: "tcp",
  service: "http",
  mlScore: "0.97",
};

beforeEach(() => {
  vi.mocked(sentinelService.fetchNetworkEvents).mockResolvedValue([mockEvent]);
});

describe("useSentinelEvents fetching", () => {
  it("starts with an empty events array while loading", () => {
    const { result } = renderHook(() => useSentinelEvents(), { wrapper: createWrapper() });
    expect(result.current.events).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("populates events after fetch resolves", async () => {
    const { result } = renderHook(() => useSentinelEvents(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.events).toEqual([mockEvent]);
  });

  it("calls fetchNetworkEvents with default count of 100", async () => {
    const spy = vi.spyOn(sentinelService, "fetchNetworkEvents");
    const { result } = renderHook(() => useSentinelEvents(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(100);
  });

  it("calls fetchNetworkEvents with a custom count", async () => {
    const spy = vi.spyOn(sentinelService, "fetchNetworkEvents");
    const { result } = renderHook(() => useSentinelEvents(25), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(25);
  });

  it("exposes error when fetch fails and falls back to empty array", async () => {
    vi.mocked(sentinelService.fetchNetworkEvents).mockRejectedValueOnce(new Error("Sentinel offline"));
    const { result } = renderHook(() => useSentinelEvents(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.events).toEqual([]);
  });

  it("exposes a refetch function", () => {
    const { result } = renderHook(() => useSentinelEvents(), { wrapper: createWrapper() });
    expect(typeof result.current.refetch).toBe("function");
  });

  it("exposes isRefetching as a boolean", () => {
    const { result } = renderHook(() => useSentinelEvents(), { wrapper: createWrapper() });
    expect(typeof result.current.isRefetching).toBe("boolean");
  });
});
