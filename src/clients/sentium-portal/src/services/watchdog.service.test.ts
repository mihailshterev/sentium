import { describe, it, expect, vi, beforeEach } from "vitest";
import {
  fetchSystemMetrics,
  fetchServiceHealth,
  fetchServiceHealthDetail,
  fetchSystemOverview,
  fetchIncidents,
  fetchWatchdogConfig,
  updateWatchdogConfig,
} from "./watchdog.service";
import { client } from "../api/client";

vi.mock("../api/client", () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

beforeEach(() => {
  vi.mocked(client.get).mockReset();
});

describe("watchdog.service fetchSystemMetrics()", () => {
  it("calls client.get with /watchdog/system/metrics", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await fetchSystemMetrics();
    expect(client.get).toHaveBeenCalledWith("/watchdog/system/metrics");
  });

  it("returns the metrics object from the API", async () => {
    const metrics = {
      host: {
        machineName: "server-1",
        osDescription: "Linux",
        osArchitecture: "x64",
        processorCount: 4,
        runtimeVersion: "8.0",
        uptime: "1d",
      },
      memory: {
        totalMb: 16000,
        usedMb: 8000,
        availableMb: 8000,
        memoryLoadPercent: 50,
        gcHeapSizeMb: 200,
        gcGen0Collections: 100,
        gcGen1Collections: 10,
        gcGen2Collections: 1,
      },
      cpu: { processorCount: 4, processCpuPercent: 12.5, architecture: "x64" },
      disks: [],
      process: {
        id: 1234,
        name: "dotnet",
        workingSetMb: 300,
        privateMemoryMb: 250,
        threadCount: 20,
        handleCount: 400,
        startTime: "2025-01-01T00:00:00Z",
        cpuTime: "00:01:00",
      },
    };
    vi.mocked(client.get).mockResolvedValueOnce(metrics);
    const result = await fetchSystemMetrics();
    expect(result).toEqual(metrics);
  });

  it("propagates errors thrown by client.get", async () => {
    vi.mocked(client.get).mockRejectedValueOnce(new Error("Service unavailable"));
    await expect(fetchSystemMetrics()).rejects.toThrow("Service unavailable");
  });
});

describe("watchdog.service fetchServiceHealth()", () => {
  it("calls client.get with /watchdog/status", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await fetchServiceHealth();
    expect(client.get).toHaveBeenCalledWith("/watchdog/status");
  });

  it("returns the status array from the API", async () => {
    const statuses = [
      { serviceName: "identity", status: "Healthy", latencyMs: 5, checkedAt: "2025-01-01T00:00:00Z", details: null },
      {
        serviceName: "agentruntime",
        status: "Unhealthy",
        latencyMs: 9999,
        checkedAt: "2025-01-01T00:00:00Z",
        details: "Timeout",
      },
    ];
    vi.mocked(client.get).mockResolvedValueOnce(statuses);
    const result = await fetchServiceHealth();
    expect(result).toEqual(statuses);
    expect(result).toHaveLength(2);
  });

  it("propagates errors thrown by client.get", async () => {
    vi.mocked(client.get).mockRejectedValueOnce(new Error("Not reachable"));
    await expect(fetchServiceHealth()).rejects.toThrow("Not reachable");
  });
});

describe("watchdog.service remaining reads", () => {
  it("fetchServiceHealthDetail encodes the service name", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await fetchServiceHealthDetail("agent runtime");
    expect(client.get).toHaveBeenCalledWith("/watchdog/status/agent%20runtime");
  });

  it("fetchSystemOverview calls the overview endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await fetchSystemOverview();
    expect(client.get).toHaveBeenCalledWith("/watchdog/status/overview");
  });

  it("fetchIncidents calls the incidents endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await fetchIncidents();
    expect(client.get).toHaveBeenCalledWith("/watchdog/incidents");
  });
});

describe("watchdog.service config", () => {
  it("fetchWatchdogConfig unwraps the settings envelope", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({
      key: "watchdog",
      value: { pollIntervalSeconds: 30 },
      updatedAt: "x",
      updatedBy: null,
    });
    const result = await fetchWatchdogConfig();
    expect(client.get).toHaveBeenCalledWith("/registry/settings/watchdog");
    expect(result).toEqual({ pollIntervalSeconds: 30 });
  });

  it("updateWatchdogConfig writes to the registry and unwraps the value", async () => {
    const payload = { pollIntervalSeconds: 60 } as never;
    vi.mocked(client.put).mockResolvedValueOnce({
      key: "watchdog",
      value: { pollIntervalSeconds: 60 },
      updatedAt: "x",
      updatedBy: null,
    });
    const result = await updateWatchdogConfig(payload);
    expect(client.put).toHaveBeenCalledWith("/registry/settings/watchdog", payload);
    expect(result).toEqual({ pollIntervalSeconds: 60 });
  });
});
