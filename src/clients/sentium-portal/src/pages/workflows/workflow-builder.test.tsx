import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import WorkflowBuilder from "./workflow-builder";
import * as useWorkflowsHook from "../../hooks/useWorkflows";
import * as useAgentsHook from "../../hooks/useAgents";
import type { WorkflowRecord } from "../../types/workflows";
import type { AgentRecord } from "../../types/agents";

const navigate = vi.fn();
let routeParams: { workflowId?: string } = {};
vi.mock("react-router", async (orig) => ({
  ...(await orig<typeof import("react-router")>()),
  useNavigate: () => navigate,
  useParams: () => routeParams,
}));

const agent: AgentRecord = {
  id: "a1",
  name: "Analyzer",
  model: "gemma",
  description: "analyzes",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const existing = {
  id: "wf1",
  name: "Existing",
  description: "desc",
  agents: [{ agentId: "a1", order: 0 }],
} as WorkflowRecord;

const createWorkflow = vi.fn();
const updateWorkflow = vi.fn();

const setWorkflows = (overrides: Record<string, unknown> = {}) =>
  vi.spyOn(useWorkflowsHook, "default").mockReturnValue({
    workflows: [existing],
    isLoading: false,
    createWorkflow,
    isCreatingWorkflow: false,
    isCreateSuccess: false,
    isCreateError: false,
    createWorkflowError: null,
    updateWorkflow,
    isUpdatingWorkflow: false,
    isUpdateSuccess: false,
    isUpdateError: false,
    updateWorkflowError: null,
    deleteWorkflow: vi.fn(),
    ...overrides,
  } as unknown as ReturnType<typeof useWorkflowsHook.default>);

beforeEach(() => {
  navigate.mockReset();
  createWorkflow.mockReset();
  updateWorkflow.mockReset();
  routeParams = {};
  setWorkflows();
  vi.spyOn(useAgentsHook, "default").mockReturnValue({
    agents: [agent],
    isLoading: false,
  } as unknown as ReturnType<typeof useAgentsHook.default>);
});

describe("WorkflowBuilder", () => {
  it("renders the create view with an empty pipeline", () => {
    render(<WorkflowBuilder />);
    expect(screen.getByText("New Workflow")).toBeInTheDocument();
    expect(screen.getByText(/add agents from the list/i)).toBeInTheDocument();
  });

  it("creates a workflow with a name and an added agent", async () => {
    render(<WorkflowBuilder />);
    fireEvent.change(screen.getByPlaceholderText("Workflow name..."), { target: { value: "My Flow" } });
    fireEvent.click(screen.getByText("Analyzer"));
    fireEvent.click(screen.getByRole("button", { name: /create workflow/i }));

    await waitFor(() =>
      expect(createWorkflow).toHaveBeenCalledWith(
        { name: "My Flow", description: "", agents: [{ agentId: "a1", order: 0 }] },
        expect.any(Object),
      ),
    );
  });

  it("renders the edit view for an existing workflow", () => {
    routeParams = { workflowId: "wf1" };
    render(<WorkflowBuilder />);
    expect(screen.getByText("Edit Workflow")).toBeInTheDocument();
    expect(screen.getAllByText("Analyzer").length).toBeGreaterThan(0);
  });

  it("shows a not-found state for an unknown workflow", () => {
    routeParams = { workflowId: "missing" };
    setWorkflows({ workflows: [] });
    render(<WorkflowBuilder />);
    expect(screen.getByText("Workflow not found")).toBeInTheDocument();
  });

  it("shows a loading state", () => {
    routeParams = { workflowId: "wf1" };
    setWorkflows({ workflows: [], isLoading: true });
    render(<WorkflowBuilder />);
    expect(screen.getByText("Loading workflow...")).toBeInTheDocument();
  });

  it("navigates back to the workflow list", () => {
    render(<WorkflowBuilder />);
    fireEvent.click(screen.getByTitle("Back to workflows"));
    expect(navigate).toHaveBeenCalledWith("/workflows");
  });
});
