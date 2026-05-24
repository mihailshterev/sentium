import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useServiceHealth from "./useServiceHealth";
import * as watchdogService from "../services/watchdog.service";
import type { ServiceHealthStatus } from "../types/serviceHealth";

vi.mock("../services/watchdog.service", () => ({
  fetchServiceHealth: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const healthy: ServiceHealthStatus = {
  serviceName: "identity",
  status: "Healthy",
  latencyMs: 4,
  checkedAt: "2025-01-01T00:00:00Z",
  details: null,
};

const unhealthy: ServiceHealthStatus = {
  serviceName: "agentruntime",
  status: "Unhealthy",
  latencyMs: 9999,
  checkedAt: "2025-01-01T00:00:00Z",
  details: "Connection refused",
};

beforeEach(() => {
  vi.mocked(watchdogService.fetchServiceHealth).mockResolvedValue([healthy]);
});

describe("useServiceHealth fetching", () => {
  it("starts with an empty services array while loading", () => {
    const { result } = renderHook(() => useServiceHealth(), { wrapper: createWrapper() });
    expect(result.current.services).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("populates services after fetch resolves", async () => {
    const { result } = renderHook(() => useServiceHealth(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.services).toEqual([healthy]);
  });

  it("calls fetchServiceHealth exactly once on mount", async () => {
    const spy = vi.spyOn(watchdogService, "fetchServiceHealth");
    const { result } = renderHook(() => useServiceHealth(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it("exposes error when fetch fails", async () => {
    vi.mocked(watchdogService.fetchServiceHealth).mockRejectedValueOnce(new Error("Watchdog down"));
    const { result } = renderHook(() => useServiceHealth(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.services).toEqual([]);
  });

  it("exposes multiple service statuses including unhealthy ones", async () => {
    vi.mocked(watchdogService.fetchServiceHealth).mockResolvedValueOnce([healthy, unhealthy]);
    const { result } = renderHook(() => useServiceHealth(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.services).toHaveLength(2);
    expect(result.current.services.find((s) => s.status === "Unhealthy")?.serviceName).toBe("agentruntime");
  });

  it("exposes a refetch function", () => {
    const { result } = renderHook(() => useServiceHealth(), { wrapper: createWrapper() });
    expect(typeof result.current.refetch).toBe("function");
  });
});
