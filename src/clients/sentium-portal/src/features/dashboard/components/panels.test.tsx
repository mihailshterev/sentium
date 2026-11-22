import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { AttentionPanel } from "./attention-panel";
import { ContinuePanel } from "./continue-panel";
import { KpiGrid } from "./kpi-grid";
import { StatusStrip } from "./status-strip";
import { WorkflowRunsFeed } from "./workflow-runs-feed";
import type { AttentionItem } from "../hooks/useAttention";
import type { ConversationSummary } from "@/features/assistant/types/assistant";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";
import type { SystemMetrics } from "@/features/system/types/system";
import type { WorkflowRun } from "@/features/workflows/types/workflows";

const svc = (name: string, status: string, latencyMs = 5): ServiceHealthStatus =>
  ({ serviceName: name, status, latencyMs, uptimePercent: 100, checkedAt: "t", details: null }) as ServiceHealthStatus;

const metrics = (memoryLoadPercent: number): SystemMetrics =>
  ({
    host: { uptime: "01:22:00" },
    memory: { memoryLoadPercent },
    cpu: { processCpuPercent: 0.9 },
    disks: [],
  }) as unknown as SystemMetrics;

const item = (id: string, tone: AttentionItem["tone"], title = `Item ${id}`): AttentionItem => ({
  id,
  tone,
  source: "Vigil",
  title,
  to: "/vigil",
});

const inRouter = (ui: React.ReactNode) => render(<MemoryRouter>{ui}</MemoryRouter>);

describe("StatusStrip", () => {
  it("reports all clear when nothing needs attention", () => {
    render(<StatusStrip items={[]} services={[svc("codex", "Healthy")]} metrics={metrics(40)} loading={false} />);
    expect(screen.getByRole("status")).toHaveTextContent("All systems operational");
    expect(screen.getByText("1/1 services healthy")).toBeInTheDocument();
    expect(screen.getByText("memory 40%")).toBeInTheDocument();
  });

  it("counts warnings, so it never contradicts the attention list", () => {
    render(<StatusStrip items={[item("m", "warning")]} services={[]} loading={false} />);
    expect(screen.getByRole("status")).toHaveTextContent("Operational with 1 warning");
  });

  it("leads with critical issues", () => {
    render(<StatusStrip items={[item("a", "critical"), item("b", "warning")]} services={[]} loading={false} />);
    expect(screen.getByRole("status")).toHaveTextContent("1 critical issue need attention");
  });

  it("says it is checking while loading", () => {
    render(<StatusStrip items={[]} services={[]} loading />);
    expect(screen.getByRole("status")).toHaveTextContent("Checking status…");
  });
});

describe("AttentionPanel", () => {
  it("shows an all-clear state", () => {
    inRouter(<AttentionPanel items={[]} loading={false} />);
    expect(screen.getByText("All clear")).toBeInTheDocument();
  });

  it("links each item to where it can be dealt with", () => {
    inRouter(<AttentionPanel items={[item("m", "warning", "Host memory at 96%")]} loading={false} />);
    expect(screen.getByRole("link", { name: /host memory at 96%/i })).toHaveAttribute("href", "/vigil");
  });

  it("caps the list and points to the inbox for the rest", () => {
    const items = Array.from({ length: 8 }, (_, i) => item(String(i), "warning"));
    inRouter(<AttentionPanel items={items} loading={false} />);
    expect(screen.getAllByRole("listitem")).toHaveLength(6);
    expect(screen.getByRole("link", { name: /2 more in the inbox/i })).toHaveAttribute("href", "/inbox");
  });
});

describe("ContinuePanel", () => {
  const conversation = (id: string, title: string): ConversationSummary => ({
    id,
    title,
    model: "gemma",
    createdAt: new Date().toISOString(),
  });

  it("links recent conversations", () => {
    inRouter(<ContinuePanel conversations={[conversation("c1", "Trip plan")]} loading={false} />);
    expect(screen.getByRole("link", { name: /trip plan/i })).toHaveAttribute("href", "/assistant/c1");
  });

  it("shows an empty state", () => {
    inRouter(<ContinuePanel conversations={[]} loading={false} />);
    expect(screen.getByText("No conversations yet")).toBeInTheDocument();
  });
});

describe("KpiGrid", () => {
  const renderGrid = (overrides: Partial<React.ComponentProps<typeof KpiGrid>> = {}) =>
    inRouter(
      <KpiGrid
        services={[svc("codex", "Healthy"), svc("praxis", "Degraded")]}
        metrics={metrics(96)}
        stats={{ total: 35, allowed: 32, denied: 3, alerts: 0, lowRisk: 35, mediumRisk: 0, highRisk: 0, criticalRisk: 0, latestAlignmentScore: null }}
        runs={[]}
        agentCount={2}
        workflowCount={1}
        modelCount={1}
        collectionCount={2}
        jobCount={0}
        loading={false}
        {...overrides}
      />,
    );

  it("gives every figure its context and a link", () => {
    renderGrid();
    expect(screen.getByRole("link", { name: "Services: 1/2, 1 not healthy" })).toHaveAttribute("href", "/vigil");
    expect(screen.getByRole("link", { name: "Tool calls: 35, 3 denied · 0 high risk" })).toHaveAttribute("href", "/sentinel");
    expect(screen.getByRole("link", { name: "Agents: 2, 1 workflow · 1 model" })).toHaveAttribute("href", "/agents");
  });

  it("marks figures that need attention", () => {
    renderGrid();
    expect(screen.getByRole("link", { name: /^host memory: 96%/i })).toHaveAttribute("data-warn", "true");
    expect(screen.getByRole("link", { name: /^tool calls/i })).not.toHaveAttribute("data-warn");
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
    expect(screen.getByText("Untitled run")).toBeInTheDocument();
    expect(screen.getByText(/run r1/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /view/i }));
    expect(onNavigate).toHaveBeenCalledWith("/orchestration/runs/r1");
  });

  it("shows what ran and which workflow ran it", () => {
    const titled = {
      ...run("r2", "Low"),
      title: "Backup audit",
      triggerType: "workflow.custom.workflow",
      triggerPayload: JSON.stringify({ activity: "check backups", workflowName: "Nightly QA" }),
    } as WorkflowRun;
    render(<WorkflowRunsFeed runs={[titled]} loading={false} onNavigate={() => {}} />);
    expect(screen.getByText("Backup audit")).toBeInTheDocument();
    expect(screen.getByText("Nightly QA · run r2")).toBeInTheDocument();
  });
});
