import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useSystemMetrics from "./useSystemMetrics";
import * as watchdogService from "../services/watchdog.service";
import type { SystemMetrics } from "../types/system";

vi.mock("../services/watchdog.service", () => ({
  fetchSystemMetrics: vi.fn(),
  fetchServiceHealth: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockMetrics: SystemMetrics = {
  host: {
    machineName: "server-1",
    osDescription: "Ubuntu 22.04",
    osArchitecture: "x64",
    processorCount: 8,
    runtimeVersion: ".NET 8.0",
    uptime: "2d 4h",
  },
  memory: {
    totalMb: 32000,
    usedMb: 14000,
    availableMb: 18000,
    memoryLoadPercent: 43.75,
    gcHeapSizeMb: 350,
    gcGen0Collections: 500,
    gcGen1Collections: 50,
    gcGen2Collections: 5,
  },
  cpu: { processorCount: 8, processCpuPercent: 22.5, architecture: "x64" },
  disks: [
    {
      name: "/",
      label: "root",
      fileSystem: "ext4",
      totalGb: 500,
      availableGb: 350,
      usedGb: 150,
      usagePercent: 30,
    },
  ],
  process: {
    id: 42,
    name: "dotnet",
    workingSetMb: 512,
    privateMemoryMb: 480,
    threadCount: 30,
    handleCount: 600,
    startTime: "2025-01-01T00:00:00Z",
    cpuTime: "00:05:00",
  },
};

beforeEach(() => {
  vi.mocked(watchdogService.fetchSystemMetrics).mockResolvedValue(mockMetrics);
});

describe("useSystemMetrics fetching", () => {
  it("metrics is undefined before data arrives", () => {
    const { result } = renderHook(() => useSystemMetrics(), { wrapper: createWrapper() });
    expect(result.current.metrics).toBeUndefined();
    expect(result.current.isLoading).toBe(true);
  });

  it("populates metrics after fetch resolves", async () => {
    const { result } = renderHook(() => useSystemMetrics(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.metrics).toEqual(mockMetrics);
  });

  it("exposes cpu, memory and host data from the resolved metrics", async () => {
    const { result } = renderHook(() => useSystemMetrics(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.metrics?.cpu.processorCount).toBe(8);
    expect(result.current.metrics?.memory.memoryLoadPercent).toBe(43.75);
    expect(result.current.metrics?.host.machineName).toBe("server-1");
  });

  it("exposes error when fetch fails", async () => {
    vi.mocked(watchdogService.fetchSystemMetrics).mockRejectedValueOnce(new Error("Watchdog down"));
    const { result } = renderHook(() => useSystemMetrics(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.metrics).toBeUndefined();
  });

  it("exposes isRefetching as a boolean", () => {
    const { result } = renderHook(() => useSystemMetrics(), { wrapper: createWrapper() });
    expect(typeof result.current.isRefetching).toBe("boolean");
  });

  it("exposes a refetch function", () => {
    const { result } = renderHook(() => useSystemMetrics(), { wrapper: createWrapper() });
    expect(typeof result.current.refetch).toBe("function");
  });
});
