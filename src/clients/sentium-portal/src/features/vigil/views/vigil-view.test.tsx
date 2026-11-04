import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { VigilView as Vigil } from "./vigil-view";
import * as useServiceHealthHook from "@/features/vigil/hooks/useServiceHealth";
import * as useSystemMetricsHook from "@/features/vigil/hooks/useSystemMetrics";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";
import type { SystemMetrics } from "@/features/system/types/system";

vi.mock("@/features/vigil/hooks/useSystemOverview", () => ({
  useSystemOverview: () => ({ overview: undefined, isLoading: false, error: null, refetch: vi.fn() }),
}));
vi.mock("@/features/vigil/hooks/useIncidents", () => ({
  useIncidents: () => ({ incidents: [], isLoading: false, error: null, refetch: vi.fn() }),
}));
vi.mock("@/features/vigil/hooks/useVigilStream", () => ({
  useVigilStream: () => ({ isLive: true }),
}));
vi.mock("@/shared/auth/hooks/useRole", () => ({
  useRole: () => ({
    isSovereign: false,
    roles: [],
    isAuthenticated: true,
    isMemberOrAbove: false,
    hasRole: () => false,
  }),
}));

const mockHealthy: ServiceHealthStatus = {
  serviceName: "Agent Runtime",
  kind: "Service",
  status: "Healthy",
  latencyMs: 45,
  checkedAt: "2025-01-01T12:00:00Z",
  details: null,
  checks: [],
  uptimePercent: 99.9,
  lastStateChange: "2025-01-01T12:00:00Z",
  consecutiveFailures: 0,
};

const mockUnhealthy: ServiceHealthStatus = {
  serviceName: "Sentinel",
  kind: "Service",
  status: "Unhealthy",
  latencyMs: 800,
  checkedAt: "2025-01-01T12:00:00Z",
  details: "Connection timeout",
  checks: [],
  uptimePercent: 40,
  lastStateChange: "2025-01-01T12:00:00Z",
  consecutiveFailures: 5,
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

const renderVigil = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Vigil />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue(defaultHealthHook);
  vi.spyOn(useSystemMetricsHook, "useSystemMetrics").mockReturnValue(defaultMetricsHook);
});

describe("Vigil initial render", () => {
  it("renders the page title", () => {
    renderVigil();
    expect(screen.getByText("Vigil")).toBeInTheDocument();
  });

  it("renders the Services and Infrastructure sections", () => {
    renderVigil();
    expect(screen.getByText("Services")).toBeInTheDocument();
    expect(screen.getByText("Infrastructure")).toBeInTheDocument();
  });

  it("shows all-operational badge when all targets are healthy", () => {
    renderVigil();
    expect(screen.getByText("All Systems Operational")).toBeInTheDocument();
  });
});

describe("Vigil loading state", () => {
  it("renders skeleton metric rows when metrics are loading", () => {
    vi.spyOn(useSystemMetricsHook, "useSystemMetrics").mockReturnValue({
      ...defaultMetricsHook,
      isLoading: true,
      metrics: undefined,
    });
    renderVigil();
    expect(screen.queryByText(/cpu usage/i)).not.toBeInTheDocument();
  });
});

describe("Vigil empty state", () => {
  it("shows no-data message when services array is empty", () => {
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({ ...defaultHealthHook, services: [] });
    renderVigil();
    expect(screen.getAllByText(/no data yet/i).length).toBeGreaterThanOrEqual(1);
  });
});

describe("Vigil service health statuses", () => {
  it("renders Healthy status label for healthy service", () => {
    renderVigil();
    expect(screen.getAllByText("Healthy").length).toBeGreaterThanOrEqual(1);
  });

  it("renders Unhealthy status label for unhealthy service", () => {
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({
      ...defaultHealthHook,
      services: [mockUnhealthy],
    });
    renderVigil();
    expect(screen.getAllByText("Unhealthy").length).toBeGreaterThanOrEqual(1);
  });

  it("shows 'Outage Detected' when any service is unhealthy", () => {
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({
      ...defaultHealthHook,
      services: [mockHealthy, mockUnhealthy],
    });
    renderVigil();
    expect(screen.getByText("Outage Detected")).toBeInTheDocument();
  });

  it("renders correct monitored count", () => {
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({
      ...defaultHealthHook,
      services: [mockHealthy, mockUnhealthy],
    });
    renderVigil();
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("renders service name", () => {
    renderVigil();
    expect(screen.getByText("Agent Runtime")).toBeInTheDocument();
  });

  it("renders latency value", () => {
    renderVigil();
    expect(screen.getByText("45ms")).toBeInTheDocument();
  });

  it("renders uptime percentage", () => {
    renderVigil();
    expect(screen.getByText("99.9% uptime")).toBeInTheDocument();
  });

  it("renders details when available", () => {
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({
      ...defaultHealthHook,
      services: [mockUnhealthy],
    });
    renderVigil();
    expect(screen.getByText("Connection timeout")).toBeInTheDocument();
  });
});

describe("Vigil metrics display", () => {
  it("renders hostname", () => {
    renderVigil();
    expect(screen.getByText("server-01")).toBeInTheDocument();
  });

  it("renders CPU usage", () => {
    renderVigil();
    expect(screen.getByText("22.5%")).toBeInTheDocument();
  });
});

describe("Vigil refresh", () => {
  it("calls refetch when Refresh button is clicked", () => {
    const refetch = vi.fn();
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({ ...defaultHealthHook, refetch });
    renderVigil();
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it("disables Refresh button while loading", () => {
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({ ...defaultHealthHook, isLoading: true });
    renderVigil();
    expect(screen.getByRole("button", { name: /refresh/i })).toBeDisabled();
  });
});
