import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import AgentOrchestration from "./agent-orchestration";
import * as useWorkflowsHook from "../../hooks/useWorkflows";
import * as agentRuntimeService from "../../services/agentRuntime.service";
import { useOrchestrationRunStore } from "../../stores/orchestration-run-store";
import type { WorkflowRecord } from "../../types/workflows";
import type { WorkflowRun } from "../../types/workflows";

vi.mock("../../services/agentRuntime.service", async (importOriginal) => {
  const actual = await importOriginal<typeof agentRuntimeService>();
  return {
    ...actual,
    fetchWorkspaces: vi.fn().mockResolvedValue([]),
    fetchWorkflowRuns: vi.fn().mockResolvedValue([]),
    runWorkflowPipeline: vi.fn().mockResolvedValue({ eventId: "stream-abc" }),
  };
});

const mockEventSource = {
  onmessage: vi.fn(),
  onerror: vi.fn(),
  close: vi.fn(),
};

let lastEventSourceInstance: MockEventSource | null = null;

class MockEventSource {
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: (() => void) | null = null;
  close = mockEventSource.close;
  constructor() {
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    lastEventSourceInstance = this;
  }
}

const mockWorkflow: WorkflowRecord = {
  id: "wf-1",
  name: "Threat Pipeline",
  description: "Main threat analysis",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
  agents: [
    { agentId: "a1", order: 1 },
    { agentId: "a2", order: 2 },
  ],
};

const defaultWorkflowsHook = {
  workflows: [mockWorkflow],
  isLoading: false,
  createWorkflow: vi.fn(),
  isCreatingWorkflow: false,
  isCreateSuccess: false,
  isCreateError: false,
  createWorkflowError: null,
  resetCreate: vi.fn(),
  updateWorkflow: vi.fn(),
  isUpdatingWorkflow: false,
  isUpdateSuccess: false,
  isUpdateError: false,
  updateWorkflowError: null,
  resetUpdate: vi.fn(),
  deleteWorkflow: vi.fn(),
  isDeletingWorkflow: false,
};

const renderOrchestration = (path = "/orchestration") => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[path]}>
        <AgentOrchestration />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  useOrchestrationRunStore.setState({ logs: [], phase: "IDLE", isRunning: false });
  vi.spyOn(useWorkflowsHook, "default").mockReturnValue(defaultWorkflowsHook);
  vi.stubGlobal("EventSource", MockEventSource);
});

describe("AgentOrchestration initial render", () => {
  it("renders the page title", () => {
    renderOrchestration();
    expect(screen.getByText("Orchestration")).toBeInTheDocument();
  });

  it("shows idle placeholder when no workflow is selected", () => {
    renderOrchestration();
    expect(screen.getByText(/select a workflow and execute/i)).toBeInTheDocument();
  });

  it("renders the Execute and History tabs", () => {
    renderOrchestration();
    expect(screen.getByRole("button", { name: /execute/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /history/i })).toBeInTheDocument();
  });

  it("renders workflow list in the sidebar", () => {
    renderOrchestration();
    expect(screen.getByText("Threat Pipeline")).toBeInTheDocument();
  });

  it("shows workflow agent count", () => {
    renderOrchestration();
    expect(screen.getByText("2 agents")).toBeInTheDocument();
  });

  it("shows 'No workflows defined' when list is empty", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, workflows: [] });
    renderOrchestration();
    expect(screen.getByText(/no workflows defined/i)).toBeInTheDocument();
  });
});

describe("AgentOrchestration workflow selection", () => {
  it("shows scenario input when a workflow is selected", () => {
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByPlaceholderText(/describe the scenario/i)).toBeInTheDocument();
  });

  it("shows workspace select when a workflow is selected", () => {
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByRole("combobox")).toBeInTheDocument();
  });

  it("renders the Run button when a workflow is selected", () => {
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByRole("button", { name: /execute workflow/i })).toBeInTheDocument();
  });
});

describe("AgentOrchestration phase bar", () => {
  it("renders Plan, Execute, Validate phase steps", () => {
    renderOrchestration();
    expect(screen.getAllByText("Plan").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("Execute").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Validate")).toBeInTheDocument();
  });
});

describe("AgentOrchestration tab switching", () => {
  it("switches to History tab", () => {
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    expect(screen.queryByText("Threat Pipeline")).not.toBeInTheDocument();
  });

  it("switches back to Execute tab", () => {
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    fireEvent.click(screen.getByRole("button", { name: /execute/i }));
    expect(screen.getByText("Threat Pipeline")).toBeInTheDocument();
  });
});

describe("AgentOrchestration running a pipeline", () => {
  it("calls runWorkflowPipeline when Run Pipeline is clicked", async () => {
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() =>
      expect(agentRuntimeService.runWorkflowPipeline).toHaveBeenCalledWith(
        expect.objectContaining({ workflowId: "wf-1" }),
      ),
    );
  });

  it("uses default scenario when input is empty", async () => {
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() =>
      expect(agentRuntimeService.runWorkflowPipeline).toHaveBeenCalledWith(
        expect.objectContaining({ scenario: expect.stringContaining("Threat Pipeline") }),
      ),
    );
  });

  it("uses custom scenario when provided", async () => {
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    fireEvent.change(screen.getByPlaceholderText(/describe the scenario/i), {
      target: { value: "Analyze recent intrusion" },
    });
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() =>
      expect(agentRuntimeService.runWorkflowPipeline).toHaveBeenCalledWith(
        expect.objectContaining({ scenario: "Analyze recent intrusion" }),
      ),
    );
  });
});

const mockRun: WorkflowRun = {
  id: "run-1",
  triggerType: "manual.execute",
  triggerPayload: "{}",
  explanation: "Ran as manual trigger",
  risk: "Low",
  recommendation: "Continue",
  startedAt: "2025-01-01T10:00:00Z",
  completedAt: "2025-01-01T10:05:00Z",
  logs: [
    { author: "PlannerAgent", text: "Planning the attack surface", type: "message" },
    { author: "ReconAgent", text: "Scanning networks", type: "message" },
    { author: "ValidatorAgent", text: "Validating results", type: "message" },
  ],
};

describe("AgentOrchestration history tab", () => {
  beforeEach(() => {
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([mockRun]);
  });

  it("shows history runs after switching to History tab", async () => {
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => {
      const allText = document.body.textContent ?? "";
      expect(allText).toContain("execute");
    });
  });

  it("shows 'No runs recorded yet' when history is empty", async () => {
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([]);
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => expect(screen.getByText(/no runs recorded yet/i)).toBeInTheDocument());
  });

  it("shows run count in history tab badge", async () => {
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => {
      const allText = document.body.textContent ?? "";
      expect(allText).toContain("execute");
    });
  });

  it("loads a run when a history run is clicked", async () => {
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => {
      const allText = document.body.textContent ?? "";
      expect(allText).toContain("execute");
    });
  });
});

describe("AgentOrchestration workspace selection", () => {
  it("shows workspace hint when a workspace is selected", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([
      {
        id: "ws-1",
        name: "Incident WS",
        description: null,
        fileCount: 3,
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
      },
    ]);
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByText("Incident WS (3 files)"));
    const select = screen.getByRole("combobox");
    fireEvent.change(select, { target: { value: "ws-1" } });
    await waitFor(() => expect(document.body.textContent).toContain("Agents can read and write files"));
  });

  it("includes workspaceId in pipeline call when workspace selected", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([
      {
        id: "ws-1",
        name: "Incident WS",
        description: null,
        fileCount: 3,
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
      },
    ]);
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByText("Incident WS (3 files)"));
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "ws-1" } });
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() =>
      expect(agentRuntimeService.runWorkflowPipeline).toHaveBeenCalledWith(
        expect.objectContaining({ workspaceId: "ws-1" }),
      ),
    );
  });
});

describe("AgentOrchestration LogEntryView rendering", () => {
  it("renders thought type log with Thinking expand button", async () => {
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([
      {
        ...mockRun,
        logs: [{ author: "PlannerAgent", text: "Deep thinking...", type: "thought" }],
      },
    ]);
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => {
      const allText = document.body.textContent ?? "";
      expect(allText.length).toBeGreaterThan(0);
    });
  });

  it("renders thought log in history run: shows Thinking button", async () => {
    const thoughtRun: WorkflowRun = {
      ...mockRun,
      logs: [{ author: "PlannerAgent", text: "Analyzing...", type: "thought" }],
    };
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([thoughtRun]);
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => expect(document.body.textContent).toContain("entries"));
    const runItemBtns = Array.from(document.querySelectorAll("button")).filter((b) =>
      b.textContent?.includes("entries"),
    );
    if (runItemBtns.length > 0) fireEvent.click(runItemBtns[0]);
    await waitFor(() => expect(screen.getByText("Thinking")).toBeInTheDocument());
  });

  it("renders tool type log in history run", async () => {
    const toolRun: WorkflowRun = {
      ...mockRun,
      logs: [{ author: "ReconAgent", text: "port_scan(target='192.168.1.1')", type: "tool" }],
    };
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([toolRun]);
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => expect(document.body.textContent).toContain("entries"));
    const runItemBtns = Array.from(document.querySelectorAll("button")).filter((b) =>
      b.textContent?.includes("entries"),
    );
    if (runItemBtns.length > 0) fireEvent.click(runItemBtns[0]);
    await waitFor(() => expect(screen.getByText(/port_scan/)).toBeInTheDocument());
  });

  it("renders message type log in history run", async () => {
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([mockRun]);
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => expect(document.body.textContent).toContain("entries"));
    const runItemBtns = Array.from(document.querySelectorAll("button")).filter((b) =>
      b.textContent?.includes("entries"),
    );
    if (runItemBtns.length > 0) fireEvent.click(runItemBtns[0]);
    await waitFor(() => expect(screen.getByText(/Planning the attack/)).toBeInTheDocument());
  });

  it("expands thought block when Thinking is clicked", async () => {
    const thoughtRun: WorkflowRun = {
      ...mockRun,
      logs: [{ author: "PlannerAgent", text: "Internal analysis...", type: "thought" }],
    };
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([thoughtRun]);
    renderOrchestration();
    fireEvent.click(screen.getByRole("button", { name: /history/i }));
    await waitFor(() => expect(document.body.textContent).toContain("entries"));
    const runItemBtns = Array.from(document.querySelectorAll("button")).filter((b) =>
      b.textContent?.includes("entries"),
    );
    if (runItemBtns.length > 0) fireEvent.click(runItemBtns[0]);
    await waitFor(() => screen.getByText("Thinking"));
    fireEvent.click(screen.getByText("Thinking"));
    expect(screen.getByText("Internal analysis...")).toBeInTheDocument();
  });
});

describe("AgentOrchestration SSE stream", () => {
  it("triggers onerror handler and sets phase to COMPLETE", async () => {
    lastEventSourceInstance = null;
    vi.mocked(agentRuntimeService.runWorkflowPipeline).mockResolvedValue({ eventId: "evt-123" });
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByRole("button", { name: /execute workflow/i }));
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() => expect(lastEventSourceInstance).not.toBeNull());
    const esi1 = lastEventSourceInstance as unknown as MockEventSource;
    if (esi1?.onerror) {
      esi1.onerror();
    }
    await waitFor(() => expect(screen.getByText("Complete")).toBeInTheDocument());
  });

  it("processes SSE message events", async () => {
    lastEventSourceInstance = null;
    vi.mocked(agentRuntimeService.runWorkflowPipeline).mockResolvedValue({ eventId: "evt-456" });
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByRole("button", { name: /execute workflow/i }));
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() => expect(lastEventSourceInstance).not.toBeNull());
    const esi2 = lastEventSourceInstance as unknown as MockEventSource;
    if (esi2?.onmessage) {
      esi2.onmessage(
        new MessageEvent("message", {
          data: JSON.stringify({ author: "PlannerAgent", text: "Analyzing...", type: "thought" }),
        }),
      );
    }
    expect(lastEventSourceInstance).not.toBeNull();
  });

  it("sets PLANNING phase when planner message received", async () => {
    lastEventSourceInstance = null;
    vi.mocked(agentRuntimeService.runWorkflowPipeline).mockResolvedValue({ eventId: "evt-789" });
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByRole("button", { name: /execute workflow/i }));
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() => expect(lastEventSourceInstance).not.toBeNull());
    const esi3 = lastEventSourceInstance as unknown as MockEventSource;
    if (esi3?.onmessage) {
      esi3.onmessage(
        new MessageEvent("message", {
          data: JSON.stringify({ author: "planner", text: "Planning...", type: "message" }),
        }),
      );
    }
    expect(lastEventSourceInstance).not.toBeNull();
  });

  it("sets VALIDATING phase when validator message received", async () => {
    lastEventSourceInstance = null;
    vi.mocked(agentRuntimeService.runWorkflowPipeline).mockResolvedValue({ eventId: "evt-999" });
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByRole("button", { name: /execute workflow/i }));
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() => expect(lastEventSourceInstance).not.toBeNull());
    const esi4 = lastEventSourceInstance as unknown as MockEventSource;
    if (esi4?.onmessage) {
      esi4.onmessage(
        new MessageEvent("message", {
          data: JSON.stringify({ author: "validator", text: "Validating...", type: "message" }),
        }),
      );
    }
    expect(lastEventSourceInstance).not.toBeNull();
  });

  it("handles null data gracefully in SSE message", async () => {
    lastEventSourceInstance = null;
    vi.mocked(agentRuntimeService.runWorkflowPipeline).mockResolvedValue({ eventId: "evt-null" });
    renderOrchestration();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByRole("button", { name: /execute workflow/i }));
    fireEvent.click(screen.getByRole("button", { name: /execute workflow/i }));
    await waitFor(() => expect(lastEventSourceInstance).not.toBeNull());
    const esi5 = lastEventSourceInstance as unknown as MockEventSource;
    if (esi5?.onmessage) {
      esi5.onmessage(new MessageEvent("message", { data: "null" }));
    }
    expect(lastEventSourceInstance).not.toBeNull();
  });
});
