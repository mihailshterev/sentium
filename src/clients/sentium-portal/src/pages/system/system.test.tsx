import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import System from "./system";
import * as useSystemMetricsHook from "../../hooks/useSystemMetrics";
import type { SystemMetrics } from "../../types/system";

const mockMetrics: SystemMetrics = {
  host: {
    machineName: "server-01",
    osDescription: "Linux 5.15",
    osArchitecture: "X64",
    processorCount: 8,
    runtimeVersion: ".NET 9.0",
    uptime: "1.02:30:00.0000000",
  },
  memory: {
    totalMb: 16384,
    usedMb: 8000,
    availableMb: 8384,
    memoryLoadPercent: 48.8,
    gcHeapSizeMb: 256,
    gcGen0Collections: 100,
    gcGen1Collections: 20,
    gcGen2Collections: 5,
  },
  cpu: {
    processorCount: 8,
    processCpuPercent: 35.5,
    architecture: "X64",
  },
  disks: [
    {
      name: "C:\\",
      label: "System",
      fileSystem: "NTFS",
      totalGb: 500,
      availableGb: 200,
      usedGb: 300,
      usagePercent: 60,
    },
  ],
  process: {
    id: 1234,
    name: "dotnet",
    workingSetMb: 512,
    privateMemoryMb: 400,
    threadCount: 32,
    handleCount: 0,
    startTime: "",
    cpuTime: "",
  },
};

const defaultHook = {
  metrics: mockMetrics,
  isLoading: false,
  isRefetching: false,
  error: null,
  refetch: vi.fn().mockResolvedValue(undefined),
};

const renderSystem = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <System />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useSystemMetricsHook, "default").mockReturnValue(defaultHook);
});

describe("System error state", () => {
  it("shows error state when error is set and metrics is undefined", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: undefined,
      error: new Error("Connection refused"),
    });
    renderSystem();
    expect(screen.getByText(/unable to load system metrics/i)).toBeInTheDocument();
    expect(screen.getByText(/connection refused/i)).toBeInTheDocument();
  });

  it("shows 'Unknown error' when error is not an Error instance", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: undefined,
      error: "string error" as unknown as Error,
    });
    renderSystem();
    expect(screen.getByText(/unknown error/i)).toBeInTheDocument();
  });

  it("retries when Retry button is clicked in error state", async () => {
    const refetch = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: undefined,
      error: new Error("err"),
      refetch,
    });
    renderSystem();
    fireEvent.click(screen.getByRole("button", { name: /retry/i }));
    await waitFor(() => expect(refetch).toHaveBeenCalled());
  });
});

describe("System loading state", () => {
  it("renders skeleton cards when isLoading", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: undefined,
      isLoading: true,
    });
    renderSystem();
    expect(screen.queryByText("35.5%")).not.toBeInTheDocument();
  });
});

describe("System success state", () => {
  it("renders the page title", () => {
    renderSystem();
    expect(screen.getAllByText("System").length).toBeGreaterThanOrEqual(1);
  });

  it("renders CPU percentage in the stats row", () => {
    renderSystem();
    expect(screen.getAllByText("35.5%").length).toBeGreaterThanOrEqual(1);
  });

  it("renders memory load percentage", () => {
    renderSystem();
    expect(screen.getAllByText("48.8%").length).toBeGreaterThan(0);
  });

  it("renders the logical core count", () => {
    renderSystem();
    expect(screen.getByText("8")).toBeInTheDocument();
  });

  it("renders the formatted system uptime with days", () => {
    renderSystem();
    expect(screen.getByText(/1d/)).toBeInTheDocument();
  });

  it("renders the process PID", () => {
    renderSystem();
    expect(screen.getByText(/PID 1234/)).toBeInTheDocument();
  });

  it("renders the disk name", () => {
    renderSystem();
    expect(screen.getByText(/C:\\/)).toBeInTheDocument();
  });

  it("renders formatMb as GB when >= 1024 MB", () => {
    renderSystem();
    expect(screen.getByText(/16\.0 GB/)).toBeInTheDocument();
  });

  it("renders formatGb as TB when >= 1024 GB", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: {
        ...mockMetrics,
        disks: [{ ...mockMetrics.disks[0], totalGb: 2048, usedGb: 1024, availableGb: 1024, usagePercent: 50 }],
      },
    });
    renderSystem();
    expect(screen.getByText(/2\.0 TB/)).toBeInTheDocument();
  });

  it("calls refetch when Refresh button is clicked", async () => {
    const refetch = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({ ...defaultHook, refetch });
    renderSystem();
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    await waitFor(() => expect(refetch).toHaveBeenCalled());
  });
});

describe("System uptime formatting edge cases", () => {
  it("formats uptime without days (hours:minutes:seconds format)", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: {
        ...mockMetrics,
        host: { ...mockMetrics.host, uptime: "02:45:00.0000000" },
      },
    });
    renderSystem();
    expect(screen.getByText(/2h 45m/)).toBeInTheDocument();
  });

  it("formats uptime with only minutes when hours is 0", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: {
        ...mockMetrics,
        host: { ...mockMetrics.host, uptime: "00:05:00.0000000" },
      },
    });
    renderSystem();
    expect(screen.getByText(/5m/)).toBeInTheDocument();
  });

  it("returns raw string when uptime has wrong format", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: {
        ...mockMetrics,
        host: { ...mockMetrics.host, uptime: "invalid" },
      },
    });
    renderSystem();
    expect(screen.getByText("invalid")).toBeInTheDocument();
  });

  it("renders disk usage at high percent (>= 75)", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: {
        ...mockMetrics,
        disks: [{ ...mockMetrics.disks[0], usagePercent: 80 }],
      },
    });
    renderSystem();
    expect(screen.getByText("80.0%")).toBeInTheDocument();
  });

  it("renders memory GC heap with zero totalMb", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultHook,
      metrics: {
        ...mockMetrics,
        memory: { ...mockMetrics.memory, totalMb: 0 },
      },
    });
    renderSystem();
    expect(screen.getAllByText("Gen 0").length).toBeGreaterThan(0);
  });
});
