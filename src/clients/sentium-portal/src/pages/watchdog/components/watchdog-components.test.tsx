import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import IncidentTimeline from "./incident-timeline";
import SeverityBadge from "./severity-badge";
import SubcheckList from "./subcheck-list";
import StatusIcon from "./status-icon";
import ServiceRow from "./service-row";
import WatchdogConfigPanel from "./watchdog-config-panel";
import * as useWatchdogConfigHook from "../../../hooks/useWatchdogConfig";
import type { Incident, HealthCheckEntry, ServiceHealthStatus, ServiceStatus } from "../../../types/serviceHealth";

describe("IncidentTimeline", () => {
  it("renders an all-clear message when there are no incidents", () => {
    render(<IncidentTimeline incidents={[]} />);
    expect(screen.getByText(/no incidents/i)).toBeInTheDocument();
  });

  it("renders open and resolved incidents with durations", () => {
    const incidents = [
      {
        id: "1",
        target: "Gateway",
        severity: "Critical",
        status: "Open",
        openedAt: "2025-01-01T00:00:00Z",
        durationMs: 45_000,
        description: "down",
      },
      {
        id: "2",
        target: "Redis",
        severity: "Warning",
        status: "Resolved",
        openedAt: "2025-01-01T00:00:00Z",
        resolvedAt: "2025-01-01T01:00:00Z",
        durationMs: 3_700_000,
      },
    ] as unknown as Incident[];
    render(<IncidentTimeline incidents={incidents} />);
    expect(screen.getByText("Gateway")).toBeInTheDocument();
    expect(screen.getByText("Redis")).toBeInTheDocument();
    expect(screen.getByText("down")).toBeInTheDocument();
  });
});

describe("SeverityBadge", () => {
  it("shows Resolved when the incident is resolved", () => {
    render(<SeverityBadge severity={"Critical" as never} status={"Resolved" as never} />);
    expect(screen.getByText("Resolved")).toBeInTheDocument();
  });

  it("shows the severity when open", () => {
    render(<SeverityBadge severity={"Warning" as never} status={"Open" as never} />);
    expect(screen.getByText("Warning")).toBeInTheDocument();
  });
});

describe("SubcheckList", () => {
  it("renders each health check with its status", () => {
    const checks = [
      { name: "db", status: "Healthy", description: "ok", durationMs: 3 },
      { name: "cache", status: "Degraded", durationMs: 9 },
    ] as unknown as HealthCheckEntry[];
    render(<SubcheckList checks={checks} />);
    expect(screen.getByText("db")).toBeInTheDocument();
    expect(screen.getByText("Degraded")).toBeInTheDocument();
  });
});

describe("StatusIcon", () => {
  it.each(["Healthy", "Degraded", "Unhealthy", "Unknown"] as ServiceStatus[])("renders an icon for %s", (status) => {
    const { container } = render(<StatusIcon status={status} />);
    expect(container.querySelector("svg")).toBeInTheDocument();
  });
});

describe("ServiceRow", () => {
  const service = (overrides: Partial<ServiceHealthStatus> = {}): ServiceHealthStatus =>
    ({
      serviceName: "Gateway",
      status: "Healthy",
      latencyMs: 12,
      uptimePercent: 99.9,
      checkedAt: "2025-01-01T00:00:00Z",
      details: null,
      checks: [],
      ...overrides,
    }) as ServiceHealthStatus;

  it("renders service name, uptime and latency", () => {
    render(<ServiceRow service={service()} />);
    expect(screen.getByText("Gateway")).toBeInTheDocument();
    expect(screen.getByText("99.9% uptime")).toBeInTheDocument();
    expect(screen.getByText("12ms")).toBeInTheDocument();
  });

  it("expands to show sub-checks when present", () => {
    const checks = [{ name: "ping", status: "Healthy", durationMs: 1 }] as unknown as HealthCheckEntry[];
    render(<ServiceRow service={service({ status: "Degraded", checks })} />);
    expect(screen.queryByText("ping")).not.toBeInTheDocument();
    fireEvent.click(screen.getByText("Gateway"));
    expect(screen.getByText("ping")).toBeInTheDocument();
  });
});

describe("WatchdogConfigPanel", () => {
  const config = {
    pollIntervalSeconds: 30,
    probeTimeoutSeconds: 5,
    degradedLatencyMs: 500,
    consecutiveFailuresToOpenIncident: 3,
    sampleHistorySize: 50,
  };
  const saveConfig = vi.fn();

  const setHook = (overrides: Record<string, unknown> = {}) =>
    vi.spyOn(useWatchdogConfigHook, "default").mockReturnValue({
      config,
      isLoading: false,
      error: null,
      saveConfig,
      isSaving: false,
      isSaveSuccess: false,
      isSaveError: false,
      saveError: null,
      resetSave: vi.fn(),
      ...overrides,
    } as unknown as ReturnType<typeof useWatchdogConfigHook.default>);

  beforeEach(() => {
    saveConfig.mockReset();
    setHook();
  });

  it("shows a loading state while config loads", () => {
    setHook({ config: undefined, isLoading: true });
    render(<WatchdogConfigPanel />);
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("renders the config fields and saves edits", () => {
    render(<WatchdogConfigPanel />);
    const pollInput = screen.getByDisplayValue("30");
    fireEvent.change(pollInput, { target: { value: "45" } });
    fireEvent.click(screen.getByRole("button", { name: /save/i }));
    expect(saveConfig).toHaveBeenCalled();
  });

  it("shows a success indicator after saving", () => {
    setHook({ isSaveSuccess: true });
    render(<WatchdogConfigPanel />);
    expect(screen.getByText("Saved")).toBeInTheDocument();
  });
});
