import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import ServiceHealthPanel from "./service-health-panel";
import VitalsStrip from "./vitals-strip";
import SecurityPanel from "./security-panel";
import WorkflowRunsFeed from "./workflow-runs-feed";
import type { ServiceHealthStatus } from "../../../types/serviceHealth";
import type { SystemMetrics } from "../../../types/system";
import type { AuditStats } from "../../../types/sentinel";
import type { WorkflowRun } from "../../../types/workflows";

const svc = (name: string, status: string, latencyMs = 5): ServiceHealthStatus =>
  ({ serviceName: name, status, latencyMs, uptimePercent: 100, checkedAt: "t", details: null }) as ServiceHealthStatus;

describe("ServiceHealthPanel", () => {
  it("shows a placeholder count while loading", () => {
    render(<ServiceHealthPanel services={[]} loading />);
    expect(screen.getByText("…")).toBeInTheDocument();
  });

  it("renders an empty state when there are no services", () => {
    render(<ServiceHealthPanel services={[]} loading={false} />);
    expect(screen.getByText("No services monitored")).toBeInTheDocument();
  });

  it("renders rows across all status kinds", () => {
    render(
      <ServiceHealthPanel
        services={[svc("A", "Healthy"), svc("B", "Degraded"), svc("C", "Unhealthy"), svc("D", "Other")]}
        loading={false}
      />,
    );
    expect(screen.getByText("A")).toBeInTheDocument();
    expect(screen.getByText("Degraded")).toBeInTheDocument();
    expect(screen.getByText("Unhealthy")).toBeInTheDocument();
    expect(screen.getByText("1/4")).toBeInTheDocument();
  });
});

describe("VitalsStrip", () => {
  const metrics = (cpu: number, mem: number): SystemMetrics =>
    ({
      cpu: { processCpuPercent: cpu },
      memory: { memoryLoadPercent: mem },
      host: { uptime: "01:00:00" },
    }) as unknown as SystemMetrics;

  it("shows dashes while loading", () => {
    render(<VitalsStrip metrics={undefined} services={[]} loading />);
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });

  it("renders critical CPU and warning memory percentages", () => {
    render(<VitalsStrip metrics={metrics(95, 80)} services={[svc("A", "Healthy")]} loading={false} />);
    expect(screen.getByText("95.0%")).toBeInTheDocument();
    expect(screen.getByText("80.0%")).toBeInTheDocument();
    expect(screen.getByText("1/1")).toBeInTheDocument();
  });
});

describe("SecurityPanel", () => {
  const stats = {
    total: 10,
    allowed: 8,
    denied: 2,
    alerts: 1,
    lowRisk: 5,
    mediumRisk: 3,
    highRisk: 1,
    criticalRisk: 1,
    latestAlignmentScore: 0.9,
  } as unknown as AuditStats;

  it("shows dashes while loading", () => {
    render(<SecurityPanel stats={undefined} loading onNavigate={() => {}} />);
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });

  it("renders audit counts and the alignment score", () => {
    render(<SecurityPanel stats={stats} loading={false} onNavigate={() => {}} />);
    expect(screen.getByText("Total")).toBeInTheDocument();
    expect(screen.getByText("90%")).toBeInTheDocument();
  });

  it("navigates to the audit log", () => {
    const onNavigate = vi.fn();
    render(<SecurityPanel stats={stats} loading={false} onNavigate={onNavigate} />);
    fireEvent.click(screen.getByRole("button", { name: /view audit log/i }));
    expect(onNavigate).toHaveBeenCalledWith("/sentinel");
  });

  it.each([
    [0.2, "20%"],
    [0.5, "50%"],
    [0.9, "90%"],
  ])("colours the alignment score for %s", (score, label) => {
    render(
      <SecurityPanel
        stats={{ ...stats, latestAlignmentScore: score } as AuditStats}
        loading={false}
        onNavigate={() => {}}
      />,
    );
    expect(screen.getByText(label)).toBeInTheDocument();
  });
});

describe("WorkflowRunsFeed", () => {
  const run = (id: string, risk: string): WorkflowRun =>
    ({ id, risk, explanation: `run ${id}`, recommendation: "", startedAt: new Date().toISOString() }) as WorkflowRun;

  it("renders the feed header while loading", () => {
    render(<WorkflowRunsFeed runs={[]} loading onNavigate={() => {}} />);
    expect(screen.getByText("Recent Workflow Runs")).toBeInTheDocument();
    expect(screen.queryByText("No workflow runs yet")).not.toBeInTheDocument();
  });

  it("renders an empty state when there are no runs", () => {
    render(<WorkflowRunsFeed runs={[]} loading={false} onNavigate={() => {}} />);
    expect(screen.getByText("No workflow runs yet")).toBeInTheDocument();
  });

  it("renders runs and navigates to a run on click", () => {
    const onNavigate = vi.fn();
    render(<WorkflowRunsFeed runs={[run("r1", "High")]} loading={false} onNavigate={onNavigate} />);
    expect(screen.getByText("run r1")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /view/i }));
    expect(onNavigate).toHaveBeenCalledWith("/orchestration/runs/r1");
  });
});
