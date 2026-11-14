import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { IncidentTimeline } from "./incident-timeline";
import { SeverityBadge } from "./severity-badge";
import { SubcheckList } from "./subcheck-list";
import { StatusIcon } from "./status-icon";
import { ServiceRow } from "./service-row";
import type {
  Incident,
  HealthCheckEntry,
  ServiceHealthStatus,
  ServiceStatus,
} from "@/features/vigil/types/serviceHealth";

describe("IncidentTimeline", () => {
  it("renders an all-clear message when there are no incidents", () => {
    render(<IncidentTimeline incidents={[]} />);
    expect(screen.getByText(/no incidents/i)).toBeInTheDocument();
  });

  it("renders open and resolved incidents with durations", () => {
    const incidents = [
      {
        id: "1",
        target: "Praxis",
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
    expect(screen.getByText("Praxis")).toBeInTheDocument();
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
      serviceName: "Praxis",
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
    expect(screen.getByText("Praxis")).toBeInTheDocument();
    expect(screen.getByText("99.9% uptime")).toBeInTheDocument();
    expect(screen.getByText("12ms")).toBeInTheDocument();
  });

  it("expands to show sub-checks when present", () => {
    const checks = [{ name: "ping", status: "Healthy", durationMs: 1 }] as unknown as HealthCheckEntry[];
    render(<ServiceRow service={service({ status: "Degraded", checks })} />);
    expect(screen.queryByText("ping")).not.toBeInTheDocument();
    fireEvent.click(screen.getByText("Praxis"));
    expect(screen.getByText("ping")).toBeInTheDocument();
  });
});
