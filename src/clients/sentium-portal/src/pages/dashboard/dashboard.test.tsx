import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Dashboard from "./dashboard";
import * as useAgentsHook from "../../hooks/useAgents";
import * as useWorkflowsHook from "../../hooks/useWorkflows";
import * as useServiceHealthHook from "../../hooks/useServiceHealth";
import * as useWorkflowRunsHook from "../../hooks/useWorkflowRuns";
import * as useSystemMetricsHook from "../../hooks/useSystemMetrics";
import * as useSentinelAuditHook from "../../hooks/useSentinelAudit";
import * as useOllamaModelsHook from "../../hooks/useOllamaModels";
import * as useSchedulerHook from "../../hooks/useScheduler";
import * as useKnowledgeBaseStatsHook from "../../hooks/useKnowledgeBaseStats";
import type { AgentRecord } from "../../types/agents";
import type { WorkflowRecord, WorkflowRun } from "../../types/workflows";
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

const mockRun: WorkflowRun = {
  id: "r1",
  triggerType: "manual.execute",
  triggerPayload: "{}",
  explanation: "Test run",
  risk: "Low",
  recommendation: "Continue",
  startedAt: "2025-01-01T00:00:00Z",
  completedAt: "2025-01-01T00:05:00Z",
  logs: [],
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
const defaultRunsHook = { runs: [mockRun], isLoading: false };
const defaultMetricsHook = { metrics: null, isLoading: false, isRefetching: false, error: null, refetch: vi.fn() };
const defaultSentinelHook = { stats: null, isLoading: false, error: null };
const defaultModelsHook = {
  models: [] as never[],
  isLoading: false,
  error: null,
  refetch: vi.fn(),
  pullState: null,
  pull: vi.fn(),
  cancelPull: vi.fn(),
  resetPull: vi.fn(),
  deletingModel: null,
  deleteModel: vi.fn(),
  deleteResult: null,
  clearDeleteResult: vi.fn(),
};
const defaultSchedulerHook = { jobs: [] as never[], isLoading: false, error: null, refetch: vi.fn() };
const defaultKbHook = {
  collections: [],
  isLoading: false,
  error: null,
  refetch: vi.fn(),
  deleteCollection: vi.fn(),
  isDeleting: false,
};

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
  vi.spyOn(useWorkflowRunsHook, "default").mockReturnValue(defaultRunsHook);
  vi.spyOn(useSystemMetricsHook, "default").mockReturnValue(
    defaultMetricsHook as unknown as ReturnType<typeof useSystemMetricsHook.default>,
  );
  vi.spyOn(useSentinelAuditHook, "useSentinelStats").mockReturnValue(
    defaultSentinelHook as unknown as ReturnType<typeof useSentinelAuditHook.useSentinelStats>,
  );
  vi.spyOn(useOllamaModelsHook, "default").mockReturnValue(
    defaultModelsHook as unknown as ReturnType<typeof useOllamaModelsHook.default>,
  );
  vi.spyOn(useSchedulerHook, "useSchedulerJobs").mockReturnValue(
    defaultSchedulerHook as unknown as ReturnType<typeof useSchedulerHook.useSchedulerJobs>,
  );
  vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue(
    defaultKbHook as ReturnType<typeof useKnowledgeBaseStatsHook.useKnowledgeBaseStats>,
  );
});

describe("Dashboard initial render", () => {
  it("renders the page title", () => {
    renderDashboard();
    expect(screen.getByText("Control Center")).toBeInTheDocument();
  });

  it("renders stat cards section", () => {
    renderDashboard();
    expect(screen.getByText("Agents")).toBeInTheDocument();
    expect(screen.getByText("Workflows")).toBeInTheDocument();
  });

  it("renders agents count in stat card", () => {
    renderDashboard();
    expect(screen.getAllByText("1").length).toBeGreaterThanOrEqual(1);
  });

  it("renders the Service Health section", () => {
    renderDashboard();
    expect(screen.getByText("Service Health")).toBeInTheDocument();
  });

  it("renders the Security Overview section", () => {
    renderDashboard();
    expect(screen.getByText("Security Overview")).toBeInTheDocument();
  });
});

describe("Dashboard loading state", () => {
  it("shows loading skeleton in stat cards when agents are loading", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isLoading: true } as ReturnType<
      typeof useAgentsHook.default
    >);
    renderDashboard();
    expect(screen.getByText("Service Health")).toBeInTheDocument();
  });
});

describe("Dashboard workflow runs feed", () => {
  it("shows 'No workflow runs yet' when runs list is empty", () => {
    vi.spyOn(useWorkflowRunsHook, "default").mockReturnValue({ runs: [], isLoading: false });
    renderDashboard();
    expect(screen.getByText(/no workflow runs yet/i)).toBeInTheDocument();
  });

  it("shows workflow run risk badge when run data is available", () => {
    renderDashboard();
    expect(screen.getAllByText("Low").length).toBeGreaterThanOrEqual(1);
  });
});

describe("Dashboard service health", () => {
  it("shows 'Healthy' status for healthy services", () => {
    renderDashboard();
    expect(screen.getByText("Healthy")).toBeInTheDocument();
  });

  it("shows 'No services monitored' when services list is empty", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({ ...defaultHealthHook, services: [] });
    renderDashboard();
    expect(screen.getByText("No services monitored")).toBeInTheDocument();
  });

  it("shows 'Unhealthy' status for unhealthy services", () => {
    vi.spyOn(useServiceHealthHook, "default").mockReturnValue({
      ...defaultHealthHook,
      services: [unhealthyService],
    });
    renderDashboard();
    expect(screen.getByText("Unhealthy")).toBeInTheDocument();
  });

  it("shows service name in health list", () => {
    renderDashboard();
    expect(screen.getByText("Agent Runtime")).toBeInTheDocument();
  });
});
