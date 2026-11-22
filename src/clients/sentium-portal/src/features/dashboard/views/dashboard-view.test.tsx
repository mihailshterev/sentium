import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { DashboardView as Dashboard } from "./dashboard-view";
import * as useAgentsHook from "@/features/agents/hooks/useAgents";
import * as useWorkflowsHook from "@/features/workflows/hooks/useWorkflows";
import * as useServiceHealthHook from "@/features/vigil/hooks/useServiceHealth";
import * as useWorkflowRunsHook from "@/features/workflows/hooks/useWorkflowRuns";
import * as useSystemMetricsHook from "@/features/vigil/hooks/useSystemMetrics";
import * as useSentinelAuditHook from "@/features/sentinel/hooks/useSentinelAudit";
import * as useModelCatalogHook from "@/features/models/hooks/useModelCatalog";
import * as useSchedulerHook from "@/features/scheduler/hooks/useScheduler";
import * as useKnowledgeBaseStatsHook from "@/features/knowledge-base/hooks/useKnowledgeBaseStats";
import * as useIncidentsHook from "@/features/vigil/hooks/useIncidents";
import * as useInboxHook from "@/features/notifications/hooks/useInbox";
import * as useConversationsHook from "@/features/assistant/hooks/useConversations";
import * as useProfileHook from "@/features/profile/hooks/useProfile";
import type { AgentRecord } from "@/features/agents/types/agents";
import type { WorkflowRecord, WorkflowRun } from "@/features/workflows/types/workflows";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";

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
  kind: "Service",
  status: "Healthy",
  latencyMs: 20,
  checkedAt: "2025-01-01T00:00:00Z",
  details: null,
  checks: [],
  uptimePercent: 100,
  lastStateChange: "2025-01-01T00:00:00Z",
  consecutiveFailures: 0,
};

const unhealthyService: ServiceHealthStatus = {
  serviceName: "Sentinel",
  kind: "Service",
  status: "Unhealthy",
  latencyMs: 999,
  checkedAt: "2025-01-01T00:00:00Z",
  details: null,
  checks: [],
  uptimePercent: 50,
  lastStateChange: "2025-01-01T00:00:00Z",
  consecutiveFailures: 2,
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
  vi.spyOn(useAgentsHook, "useAgents").mockReturnValue(defaultAgentsHook as ReturnType<typeof useAgentsHook.useAgents>);
  vi.spyOn(useWorkflowsHook, "useWorkflows").mockReturnValue(
    defaultWorkflowsHook as ReturnType<typeof useWorkflowsHook.useWorkflows>,
  );
  vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue(defaultHealthHook);
  vi.spyOn(useWorkflowRunsHook, "useWorkflowRuns").mockReturnValue(defaultRunsHook);
  vi.spyOn(useSystemMetricsHook, "useSystemMetrics").mockReturnValue(
    defaultMetricsHook as unknown as ReturnType<typeof useSystemMetricsHook.useSystemMetrics>,
  );
  vi.spyOn(useSentinelAuditHook, "useSentinelStats").mockReturnValue(
    defaultSentinelHook as unknown as ReturnType<typeof useSentinelAuditHook.useSentinelStats>,
  );
  vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(
    defaultModelsHook as unknown as ReturnType<typeof useModelCatalogHook.useModelCatalog>,
  );
  vi.spyOn(useSchedulerHook, "useSchedulerJobs").mockReturnValue(
    defaultSchedulerHook as unknown as ReturnType<typeof useSchedulerHook.useSchedulerJobs>,
  );
  vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue(
    defaultKbHook as ReturnType<typeof useKnowledgeBaseStatsHook.useKnowledgeBaseStats>,
  );
  vi.spyOn(useIncidentsHook, "useIncidents").mockReturnValue({
    incidents: [],
    isLoading: false,
    error: null,
    refetch: vi.fn(),
  } as unknown as ReturnType<typeof useIncidentsHook.useIncidents>);
  vi.spyOn(useInboxHook, "useNotifications").mockReturnValue({ notifications: [], isLoading: false, error: null });
  vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
    conversations: [{ id: "c1", title: "Trip plan", model: "gemma", createdAt: new Date().toISOString() }],
    isLoading: false,
  } as unknown as ReturnType<typeof useConversationsHook.useConversations>);
  vi.spyOn(useProfileHook, "useProfile").mockReturnValue({
    profile: { firstName: "Ada" },
  } as unknown as ReturnType<typeof useProfileHook.useProfile>);
});

describe("Home", () => {
  it("is titled Home and greets the user by first name", () => {
    renderDashboard();
    expect(screen.getByRole("heading", { name: "Home" })).toBeInTheDocument();
    expect(screen.getByText(/good (morning|afternoon|evening), ada/i)).toBeInTheDocument();
  });

  it("offers the two main actions", () => {
    renderDashboard();
    expect(screen.getByRole("button", { name: /new conversation/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /orchestrate a goal/i })).toBeInTheDocument();
  });

  it("shows where the user left off", () => {
    renderDashboard();
    expect(screen.getByRole("link", { name: /trip plan/i })).toHaveAttribute("href", "/assistant/c1");
  });

  it("links each key figure to its page", () => {
    renderDashboard();
    expect(screen.getByRole("link", { name: /^agents: 1,/i })).toHaveAttribute("href", "/agents");
    expect(screen.getByRole("link", { name: "Services: 1/1, all healthy" })).toHaveAttribute("href", "/vigil");
  });
});

describe("Home attention", () => {
  it("is all clear when every service is healthy", () => {
    renderDashboard();
    expect(screen.getByRole("status")).toHaveTextContent("All systems operational");
    expect(screen.getByText("All clear")).toBeInTheDocument();
  });

  it("lists an unhealthy service and says so in the status strip", () => {
    vi.spyOn(useServiceHealthHook, "useServiceHealth").mockReturnValue({
      ...defaultHealthHook,
      services: [healthyService, unhealthyService],
    });
    renderDashboard();
    expect(screen.getByRole("status")).toHaveTextContent("1 critical issue need attention");
    expect(screen.getByRole("link", { name: /sentinel is unhealthy/i })).toHaveAttribute("href", "/vigil");
  });

  it("flags high host memory instead of claiming everything is fine", () => {
    vi.spyOn(useSystemMetricsHook, "useSystemMetrics").mockReturnValue({
      ...defaultMetricsHook,
      metrics: {
        host: { uptime: "01:00:00" },
        memory: { memoryLoadPercent: 96, availableMb: 1024, totalMb: 32768 },
        cpu: { processCpuPercent: 1 },
        disks: [],
      },
    } as unknown as ReturnType<typeof useSystemMetricsHook.useSystemMetrics>);
    renderDashboard();
    expect(screen.getByRole("status")).toHaveTextContent("Operational with 1 warning");
    expect(screen.getByRole("link", { name: /host memory at 96%/i })).toHaveAttribute("href", "/system");
  });
});

describe("Home recent runs", () => {
  it("shows 'No workflow runs yet' when runs list is empty", () => {
    vi.spyOn(useWorkflowRunsHook, "useWorkflowRuns").mockReturnValue({ runs: [], isLoading: false });
    renderDashboard();
    expect(screen.getByText(/no workflow runs yet/i)).toBeInTheDocument();
  });

  it("shows the run risk badge", () => {
    renderDashboard();
    expect(screen.getAllByText("Low").length).toBeGreaterThanOrEqual(1);
  });
});
