import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router";
import WorkflowsList from "./workflows";
import WorkflowBuilder from "./workflow-builder";

// Mock the hooks
vi.mock("../../hooks/useWorkflows");
vi.mock("../../hooks/useAgents");

import useWorkflows from "../../hooks/useWorkflows";
import useAgents from "../../hooks/useAgents";

const mockUseWorkflows = vi.mocked(useWorkflows);
const mockUseAgents = vi.mocked(useAgents);

const baseWorkflowsReturn = {
  workflows: [],
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

const baseAgentsReturn = {
  agents: [],
  isLoading: false,
  createAgent: vi.fn(),
  isCreatingAgent: false,
  isCreateSuccess: false,
  isCreateError: false,
  createAgentError: null,
  resetCreate: vi.fn(),
  updateAgent: vi.fn(),
  isUpdatingAgent: false,
  isUpdateSuccess: false,
  isUpdateError: false,
  updateAgentError: null,
  resetUpdate: vi.fn(),
  deleteAgent: vi.fn(),
  isDeletingAgent: false,
};

const renderAt = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/workflows" element={<WorkflowsList />} />
        <Route path="/workflows/new" element={<WorkflowBuilder />} />
        <Route path="/workflows/:workflowId" element={<WorkflowBuilder />} />
      </Routes>
    </MemoryRouter>,
  );

describe("Workflows list", () => {
  beforeEach(() => {
    mockUseWorkflows.mockReturnValue(baseWorkflowsReturn);
    mockUseAgents.mockReturnValue(baseAgentsReturn);
  });

  it("renders the page header", () => {
    renderAt("/workflows");
    expect(screen.getByText("Workflows")).toBeInTheDocument();
  });

  it("shows empty state when no workflows", () => {
    renderAt("/workflows");
    expect(screen.getByText("No workflows yet")).toBeInTheDocument();
  });

  it("renders list of workflows", () => {
    const workflows = [
      { id: "1", name: "Test Workflow", description: "Test desc", agents: [], createdAt: "", updatedAt: "" },
    ];
    mockUseWorkflows.mockReturnValue({ ...baseWorkflowsReturn, workflows });
    renderAt("/workflows");
    expect(screen.getByText("Test Workflow")).toBeInTheDocument();
  });
});

describe("Workflow builder", () => {
  beforeEach(() => {
    mockUseWorkflows.mockReturnValue(baseWorkflowsReturn);
    mockUseAgents.mockReturnValue(baseAgentsReturn);
  });

  it("renders the create form on the /new route", () => {
    renderAt("/workflows/new");
    expect(screen.getByText("New Workflow")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Workflow name...")).toBeInTheDocument();
  });

  it("shows not-found when editing a missing workflow", () => {
    renderAt("/workflows/does-not-exist");
    expect(screen.getByText("Workflow not found")).toBeInTheDocument();
  });
});
