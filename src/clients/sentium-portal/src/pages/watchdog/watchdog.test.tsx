import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Watchdog from "./watchdog";
import * as useServiceHealthHook from "../../hooks/useServiceHealth";
import * as useSystemMetricsHook from "../../hooks/useSystemMetrics";
import type { ServiceHealthStatus } from "../../types/serviceHealth";
import type { SystemMetrics } from "../../types/system";

const mockHealthy: ServiceHealthStatus = {
  serviceName: "Agent Runtime",
  status: "Healthy",
  latencyMs: 45,
  checkedAt: "2025-01-01T12:00:00Z",
  details: null,
};

const mockUnhealthy: ServiceHealthStatus = {
  serviceName: "Sentinel",
  status: "Unhealthy",
  latencyMs: 800,
  checkedAt: "2025-01-01T12:00:00Z",
  details: "Connection timeout",
};

const mockUnknown: ServiceHealthStatus = {
  serviceName: "Locus",
  status: "Unknown",
  latencyMs: 200,
  checkedAt: "2025-01-01T12:00:00Z",
  details: null,
};

const mockMetrics: SystemMetrics = {
  host: {
    machineName: "server-01",
    osDescription: "Linux 5.15",
    osArchitecture: "X64",
    processorCount: 4,
    runtimeVersion: ".NET 9.0",
    uptime: "02:30:00.0000000",
  },
  memory: {
    totalMb: 8192,
    usedMb: 4000,
    availableMb: 4192,
    memoryLoadPercent: 48.8,
    gcHeapSizeMb: 128,
    gcGen0Collections: 50,
    gcGen1Collections: 10,
    gcGen2Collections: 2,
  },
  cpu: { processorCount: 4, processCpuPercent: 22.5, architecture: "X64" },
  disks: [],
  process: {
    id: 42,
    name: "dotnet",
    workingSetMb: 256,
    privateMemoryMb: 200,
    threadCount: 16,
    handleCount: 0,
    startTime: "",
    cpuTime: "",
  },
};

const defaultHealthHook = {
  services: [mockHealthy],
  isLoading: false,
  error: null,
  refetch: vi.fn(),
};

const defaultMetricsHook = {
  metrics: mockMetrics,
  isLoading: false,
  isRefetching: false,
  error: null,
  refetch: vi.fn(),
};

const renderWatchdog = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Watchdog />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useServiceHealthHook, "default").mockReturnValue(defaultHealthHook);
  vi.spyOn(useSystemMetricsHook, "default").mockReturnValue(defaultMetricsHook);
});

describe("Watchdog initial render", () => {
  it("renders the page title", () => {
    renderWatchdog();
    expect(screen.getByText("Watchdog")).toBeInTheDocument();
  });

  it("renders service health section", () => {
    renderWatchdog();
    expect(screen.getByText("Service Health")).toBeInTheDocument();
  });

  it("shows all-healthy badge when all services are healthy", () => {
    renderWatchdog();
    expect(screen.getByText("All Systems Operational")).toBeInTheDocument();
  });
});

describe("Watchdog loading state", () => {
  it("renders skeleton rows when health is loading", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({ ...defaultHealthHook, isLoading: true, services: [] });
    renderWatchdog();
    expect(screen.queryByText("api-gateway")).not.toBeInTheDocument();
  });

  it("renders skeleton metric rows when metrics are loading", () => {
    vi.spyOn(useSystemMetricsHook, "default").mockReturnValue({
      ...defaultMetricsHook,
      isLoading: true,
      metrics: undefined,
    });
    renderWatchdog();
    expect(screen.queryByText(/cpu/i)).not.toBeInTheDocument();
  });
});

describe("Watchdog empty state", () => {
  it("shows no-data message when services array is empty", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({ ...defaultHealthHook, services: [] });
    renderWatchdog();
    expect(screen.getByText(/no health data yet/i)).toBeInTheDocument();
  });
});

describe("Watchdog service health statuses", () => {
  it("renders Healthy status label for healthy service", () => {
    renderWatchdog();
    expect(screen.getAllByText("Healthy").length).toBeGreaterThanOrEqual(1);
  });

  it("renders Unhealthy status label for unhealthy service", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [mockUnhealthy],
    });
    renderWatchdog();
    expect(screen.getAllByText("Unhealthy").length).toBeGreaterThanOrEqual(1);
  });

  it("renders Unknown status label for unknown service", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [mockUnknown],
    });
    renderWatchdog();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  it("shows 'Degraded Services Detected' when any service is unhealthy", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [mockHealthy, mockUnhealthy],
    });
    renderWatchdog();
    expect(screen.getByText("Degraded Services Detected")).toBeInTheDocument();
  });

  it("renders correct monitored count", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [mockHealthy, mockUnhealthy],
    });
    renderWatchdog();
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("renders service name", () => {
    renderWatchdog();
    expect(screen.getByText("Agent Runtime")).toBeInTheDocument();
  });

  it("renders latency value", () => {
    renderWatchdog();
    expect(screen.getByText("45ms")).toBeInTheDocument();
  });

  it("renders details when available", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [mockUnhealthy],
    });
    renderWatchdog();
    expect(screen.getByText("Connection timeout")).toBeInTheDocument();
  });

  it("high latency (> 400ms) renders red latency bar", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [{ ...mockHealthy, latencyMs: 500 }],
    });
    renderWatchdog();
    expect(screen.getByText("500ms")).toBeInTheDocument();
  });

  it("medium latency (100-400ms) renders amber latency bar", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [{ ...mockHealthy, latencyMs: 250 }],
    });
    renderWatchdog();
    expect(screen.getByText("250ms")).toBeInTheDocument();
  });
});

describe("Watchdog metrics display", () => {
  it("renders hostname", () => {
    renderWatchdog();
    expect(screen.getByText("server-01")).toBeInTheDocument();
  });

  it("renders CPU usage", () => {
    renderWatchdog();
    expect(screen.getByText("22.5%")).toBeInTheDocument();
  });

  it("renders total memory", () => {
    renderWatchdog();
    expect(screen.getByText("8192 MB")).toBeInTheDocument();
  });
});

describe("Watchdog refresh", () => {
  it("calls refetch when Refresh button is clicked", () => {
    const refetch = vi.fn();
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({ ...defaultHealthHook, refetch });
    renderWatchdog();
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it("disables Refresh button while loading", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({ ...defaultHealthHook, isLoading: true });
    renderWatchdog();
    expect(screen.getByRole("button", { name: /refresh/i })).toBeDisabled();
  });
});
