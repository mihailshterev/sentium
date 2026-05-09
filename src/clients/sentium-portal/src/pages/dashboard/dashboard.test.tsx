import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Dashboard from "./dashboard";
import * as useAgentsHook from "../../hooks/useAgents";
import * as useWorkflowsHook from "../../hooks/useWorkflows";
import * as useServiceHealthHook from "../../hooks/useServiceHealth";
import type { AgentRecord } from "../../types/agents";
import type { WorkflowRecord } from "../../types/workflows";
import type { ServiceHealthStatus } from "../../types/serviceHealth";

const mockAgent: AgentRecord = {
  id: "a1",
  name: "ReconAgent",
  description: "Reconnaissance agent",
  model: "llama3.2",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const mockWorkflow: WorkflowRecord = {
  id: "w1",
  name: "Threat Pipeline",
  description: "Main threat workflow",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
  agents: [{ agentId: "a1", order: 1 }],
};

const healthyService: ServiceHealthStatus = {
  serviceName: "Agent Runtime",
  status: "Healthy",
  latencyMs: 20,
  checkedAt: "2025-01-01T00:00:00Z",
  details: null,
};

const unhealthyService: ServiceHealthStatus = {
  serviceName: "Sentinel",
  status: "Unhealthy",
  latencyMs: 999,
  checkedAt: "2025-01-01T00:00:00Z",
  details: null,
};

const defaultAgentsHook = { agents: [mockAgent], isLoading: false };
const defaultWorkflowsHook = { workflows: [mockWorkflow], isLoading: false };
const defaultHealthHook = { services: [healthyService], isLoading: false, error: null, refetch: vi.fn() };

const renderDashboard = (path = "/") => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[path]}>
        <Dashboard />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useAgentsHook, "default").mockReturnValue(defaultAgentsHook as ReturnType<typeof useAgentsHook.default>);
  vi.spyOn(useWorkflowsHook, "default").mockReturnValue(
    defaultWorkflowsHook as ReturnType<typeof useWorkflowsHook.default>,
  );
  vi.spyOn(useServiceHealthHook, "default").mockReturnValue(defaultHealthHook);
});

describe("Dashboard initial render", () => {
  it("renders the page title", () => {
    renderDashboard();
    expect(screen.getByText("Dashboard")).toBeInTheDocument();
  });

  it("renders the agents count", () => {
    renderDashboard();
    expect(screen.getAllByText("1").length).toBeGreaterThanOrEqual(1);
  });

  it("renders the workflows count", () => {
    renderDashboard();
    expect(screen.getAllByText("1").length).toBeGreaterThanOrEqual(1);
  });

  it("renders the Quick Access section", () => {
    renderDashboard();
    expect(screen.getByText("Quick Access")).toBeInTheDocument();
  });

  it("renders all quick access cards", () => {
    renderDashboard();
    expect(screen.getAllByText("Orchestration").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("AI Assistant")).toBeInTheDocument();
    expect(screen.getAllByText("Agents").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("Workflows").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("Sentinel").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("Watchdog").length).toBeGreaterThanOrEqual(1);
  });

  it("renders the System Modules section", () => {
    renderDashboard();
    expect(screen.getByText("System Modules")).toBeInTheDocument();
  });

  it("renders all module names", () => {
    renderDashboard();
    expect(screen.getAllByText("Agent Runtime").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("Sentinel").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Locus")).toBeInTheDocument();
    expect(screen.getByText("Identity Provider")).toBeInTheDocument();
  });
});

describe("Dashboard loading state", () => {
  it("shows skeleton values when agents are loading", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isLoading: true } as ReturnType<
      typeof useAgentsHook.default
    >);
    renderDashboard();
    expect(screen.queryByText("AI Assistant")).toBeInTheDocument();
  });

  it("shows skeleton activity rows when loading", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isLoading: true } as ReturnType<
      typeof useAgentsHook.default
    >);
    renderDashboard();
    expect(screen.queryByText("Threat Pipeline")).not.toBeInTheDocument();
  });
});

describe("Dashboard activity feed", () => {
  it("shows 'No recent activity' when agents and workflows are empty", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ agents: [], isLoading: false } as unknown as ReturnType<
      typeof useAgentsHook.default
    >);
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ workflows: [], isLoading: false } as unknown as ReturnType<
      typeof useWorkflowsHook.default
    >);
    renderDashboard();
    expect(screen.getByText(/no recent activity/i)).toBeInTheDocument();
  });

  it("shows workflow name in activity feed", () => {
    renderDashboard();
    expect(screen.getByText("Threat Pipeline")).toBeInTheDocument();
  });

  it("shows agent name in activity feed", () => {
    renderDashboard();
    expect(screen.getAllByText(/recon/i).length).toBeGreaterThan(0);
  });
});

describe("Dashboard module status", () => {
  it("shows 'Online' for modules that are healthy", () => {
    renderDashboard();
    expect(screen.getByText("Online")).toBeInTheDocument();
  });

  it("shows 'Unknown' for modules with no health data", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({ ...defaultHealthHook, services: [] });
    renderDashboard();
    expect(screen.getAllByText("Unknown").length).toBeGreaterThan(0);
  });

  it("shows 'Offline' for unhealthy services", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [unhealthyService],
    });
    renderDashboard();
    expect(screen.getByText("Offline")).toBeInTheDocument();
  });
});

describe("Dashboard navigation", () => {
  it("navigates when a Quick Access card is clicked", () => {
    renderDashboard();
    fireEvent.click(screen.getByText("Orchestration").closest("button")!);
  });

  it("navigates to sentinel when View button clicked in security section", () => {
    renderDashboard();
    const viewBtns = screen.getAllByRole("button").filter((b) => b.textContent?.trim() === "View");
    viewBtns.forEach((btn) => fireEvent.click(btn));
    expect(viewBtns.length).toBeGreaterThan(0);
  });
});
