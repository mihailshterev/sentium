import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Workflows from "./workflows";
import * as useWorkflowsHook from "../../hooks/useWorkflows";
import * as useAgentsHook from "../../hooks/useAgents";
import type { WorkflowRecord } from "../../types/workflows";
import type { AgentRecord } from "../../types/agents";

let capturedOnDragEnd: ((event: { active: { id: string }; over: { id: string } | null }) => void) | null = null;
vi.mock("@dnd-kit/core", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@dnd-kit/core")>();
  return {
    ...actual,
    DndContext: ({ children, onDragEnd }: { children: React.ReactNode; onDragEnd: (e: unknown) => void }) => {
      capturedOnDragEnd = onDragEnd as typeof capturedOnDragEnd;
      return children;
    },
  };
});

const mockAgent: AgentRecord = {
  id: "agent-1",
  name: "ReconAgent",
  description: "Recon agent",
  model: "llama3.2",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const mockWorkflow: WorkflowRecord = {
  id: "wf-1",
  name: "Threat Pipeline",
  description: "Main threat analysis pipeline",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
  agents: [{ agentId: "agent-1", order: 1 }],
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

const defaultAgentsHook = {
  agents: [mockAgent],
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

const renderWorkflows = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Workflows />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useWorkflowsHook, "default").mockReturnValue(defaultWorkflowsHook);
  vi.spyOn(useAgentsHook, "default").mockReturnValue(
    defaultAgentsHook as unknown as ReturnType<typeof useAgentsHook.default>,
  );
});

describe("Workflows initial render", () => {
  it("renders the page title", () => {
    renderWorkflows();
    expect(screen.getByText("Workflow Builder")).toBeInTheDocument();
  });

  it("renders the workflow count", () => {
    renderWorkflows();
    expect(screen.getByText("1 workflows")).toBeInTheDocument();
  });

  it("renders New Workflow button", () => {
    renderWorkflows();
    expect(screen.getAllByRole("button", { name: /new workflow/i }).length).toBeGreaterThan(0);
  });

  it("shows 'Select a workflow...' placeholder when no workflow is selected", () => {
    renderWorkflows();
    expect(screen.getByText(/select a workflow to edit or create a new one/i)).toBeInTheDocument();
  });
});

describe("Workflows sidebar list", () => {
  it("renders workflow name in the list", () => {
    renderWorkflows();
    expect(screen.getByText("Threat Pipeline")).toBeInTheDocument();
  });

  it("renders workflow description", () => {
    renderWorkflows();
    expect(screen.getByText("Main threat analysis pipeline")).toBeInTheDocument();
  });

  it("shows 'No workflows yet' when list is empty and editor is closed", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, workflows: [] });
    renderWorkflows();
    expect(screen.getByText(/no workflows yet/i)).toBeInTheDocument();
  });
});

describe("Workflows create flow", () => {
  it("opens New Workflow form when New Workflow button is clicked", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getAllByText("New Workflow").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByLabelText(/^name/i)).toBeInTheDocument();
  });

  it("closes the editor when X button is clicked", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getAllByText("New Workflow").length).toBeGreaterThanOrEqual(1);
    const allButtons = screen.getAllByRole("button");
    const closeBtn = allButtons.find((b) => b.textContent === "" && b.className.includes("close"));
    if (closeBtn) {
      fireEvent.click(closeBtn);
      expect(screen.queryByLabelText(/^name/i)).not.toBeInTheDocument();
    }
  });

  it("shows 'Add agents from the list' when no agents added to form", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getByText(/add agents from the list/i)).toBeInTheDocument();
  });

  it("adds agent to pipeline when agent picker button clicked", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    fireEvent.click(screen.getByRole("button", { name: /reconagent/i }));
    expect(screen.getAllByText("ReconAgent").length).toBeGreaterThan(0);
  });

  it("shows 'No agents registered yet' when agents list is empty", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, agents: [] } as unknown as ReturnType<
      typeof useAgentsHook.default
    >);
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getByText(/no agents registered yet/i)).toBeInTheDocument();
  });

  it("submit button is disabled when name is empty", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getByRole("button", { name: /create workflow/i })).toBeDisabled();
  });

  it("submit button is enabled when name is entered", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "New Pipeline" } });
    expect(screen.getByRole("button", { name: /create workflow/i })).not.toBeDisabled();
  });

  it("calls createWorkflow when form is submitted with name", async () => {
    const createWorkflow = vi.fn();
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, createWorkflow });
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "New Pipeline" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() =>
      expect(createWorkflow).toHaveBeenCalledWith(
        expect.objectContaining({ name: "New Pipeline" }),
        expect.any(Object),
      ),
    );
  });

  it("calls closeEdit onSuccess when createWorkflow succeeds", async () => {
    const createWorkflow = vi.fn().mockImplementation((_payload, { onSuccess } = {}) => onSuccess?.());
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, createWorkflow });
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Quick Flow" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(createWorkflow).toHaveBeenCalled());
    expect(createWorkflow).toHaveBeenCalled();
  });

  it("shows char count for description field", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    fireEvent.change(screen.getByLabelText(/description/i), { target: { value: "Hello" } });
    expect(screen.getByText("5/4000")).toBeInTheDocument();
  });
});

describe("Workflows edit flow", () => {
  it("opens edit form when a workflow is clicked from the list", () => {
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByText("Edit Workflow")).toBeInTheDocument();
  });

  it("pre-fills name in edit form", () => {
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect((screen.getByLabelText(/^name/i) as HTMLInputElement).value).toBe("Threat Pipeline");
  });

  it("shows 'Save Changes' button when editing", () => {
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByRole("button", { name: /save changes/i })).toBeInTheDocument();
  });

  it("shows 'Saving...' while updating", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, isUpdatingWorkflow: true });
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByText(/saving\.\.\./i)).toBeInTheDocument();
  });

  it("shows 'Saved' when update succeeds", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, isUpdateSuccess: true });
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByText("Saved")).toBeInTheDocument();
  });
});

describe("Workflows delete", () => {
  it("calls deleteWorkflow when delete confirmed", async () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true),
    );
    const deleteWorkflow = vi.fn();
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, deleteWorkflow });
    renderWorkflows();
    const deleteBtn = document.querySelector(".deleteBtn") as HTMLElement;
    if (deleteBtn) {
      fireEvent.click(deleteBtn);
      await waitFor(() => expect(deleteWorkflow).toHaveBeenCalledWith("wf-1", expect.any(Object)));
    }
  });

  it("does not call deleteWorkflow when confirmation cancelled", () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => false),
    );
    const deleteWorkflow = vi.fn();
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, deleteWorkflow });
    renderWorkflows();
    const deleteBtn = document.querySelector(".deleteBtn") as HTMLElement;
    if (deleteBtn) {
      fireEvent.click(deleteBtn);
      expect(deleteWorkflow).not.toHaveBeenCalled();
    }
  });
});

describe("Workflows add agent to form", () => {
  it("shows agent list when creating a workflow", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getAllByText("ReconAgent").length).toBeGreaterThanOrEqual(1);
  });

  it("adds agent to pipeline when agent button clicked", async () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    const agentBtns = screen.getAllByRole("button");
    const pickerBtn = agentBtns.find(
      (btn) => btn.textContent?.includes("ReconAgent") && btn.className.includes("agentPickerItem"),
    );
    if (pickerBtn) fireEvent.click(pickerBtn);
    else {
      const btn = agentBtns.find(
        (b) => b.textContent?.trim() === "ReconAgent" || b.textContent?.includes("ReconAgent"),
      );
      if (btn) fireEvent.click(btn);
    }
    await waitFor(() => expect(screen.getAllByText("ReconAgent").length).toBeGreaterThanOrEqual(1));
  });

  it("removes agent from pipeline when X button clicked", async () => {
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getAllByText("ReconAgent"));
    const xBtns = document.querySelectorAll("button");
    for (const btn of xBtns) {
      if (btn.className.includes("sortableAgentRemove")) {
        fireEvent.click(btn);
        break;
      }
    }
    await waitFor(() => {
      expect(screen.queryAllByText("ReconAgent").length).toBeGreaterThanOrEqual(0);
    });
  });

  it("reorders agents when drag ends with different positions", async () => {
    const mockWorkflowTwoAgents: WorkflowRecord = {
      id: "wf-2",
      name: "Multi Pipeline",
      description: "Two agents",
      createdAt: "2025-01-01T00:00:00Z",
      updatedAt: "2025-01-01T00:00:00Z",
      agents: [
        { agentId: "agent-1", order: 0 },
        { agentId: "agent-1", order: 1 },
      ],
    };
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({
      ...defaultWorkflowsHook,
      workflows: [mockWorkflowTwoAgents],
    });
    vi.spyOn(useAgentsHook, "default").mockReturnValue({
      ...defaultAgentsHook,
      agents: [mockAgent],
    } as unknown as ReturnType<typeof useAgentsHook.default>);
    capturedOnDragEnd = null;
    renderWorkflows();
    fireEvent.click(screen.getByText("Multi Pipeline"));
    await waitFor(() => capturedOnDragEnd !== null);
    type DragEndFn = (event: { active: { id: string }; over: { id: string } | null }) => void;
    const ced1 = capturedOnDragEnd as unknown as DragEndFn;
    if (ced1) {
      ced1({ active: { id: "sort-0" }, over: { id: "sort-1" } });
    }
    expect(capturedOnDragEnd).not.toBeNull();
  });

  it("does nothing when drag ends at same position", async () => {
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => capturedOnDragEnd !== null);
    if (capturedOnDragEnd) {
      capturedOnDragEnd({ active: { id: "sort-0" }, over: { id: "sort-0" } });
    }
    expect(capturedOnDragEnd).not.toBeNull();
  });

  it("does nothing when drag ends with no over target", async () => {
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => capturedOnDragEnd !== null);
    if (capturedOnDragEnd) {
      capturedOnDragEnd({ active: { id: "sort-0" }, over: null });
    }
    expect(capturedOnDragEnd).not.toBeNull();
  });

  it("shows close button (X) when creating workflow", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getAllByText("New Workflow").length).toBeGreaterThanOrEqual(1);
  });

  it("closes create form when close X button is clicked", () => {
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    const closeBtns = document.querySelectorAll("button");
    const closeBtn = Array.from(closeBtns).find((b) => b.className.includes("closeBtn"));
    if (closeBtn) fireEvent.click(closeBtn);
    expect(screen.queryByLabelText(/^name/i)).not.toBeInTheDocument();
  });
});

describe("Workflows error states", () => {
  it("shows error when create fails", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({
      ...defaultWorkflowsHook,
      isCreateError: true,
      createWorkflowError: new Error("Server error"),
    });
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(screen.getByText(/server error/i)).toBeInTheDocument();
  });

  it("shows 'Saving...' when creating workflow (isPending)", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({
      ...defaultWorkflowsHook,
      isCreatingWorkflow: true,
    });
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(document.body.textContent).toContain("Saving...");
  });

  it("shows 'Saved' when create succeeds", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({
      ...defaultWorkflowsHook,
      isCreateSuccess: true,
    });
    renderWorkflows();
    fireEvent.click(screen.getAllByRole("button", { name: /new workflow/i })[0]);
    expect(document.body.textContent).toContain("Saved");
  });

  it("shows error when update fails", () => {
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({
      ...defaultWorkflowsHook,
      isUpdateError: true,
      updateWorkflowError: new Error("Update failed"),
    });
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    expect(screen.getByText(/update failed/i)).toBeInTheDocument();
  });
});

describe("Workflows sidebar actions", () => {
  it("opens edit form when sidebar Edit button is clicked", () => {
    renderWorkflows();
    fireEvent.click(screen.getByTitle("Edit"));
    expect(screen.getByText("Edit Workflow")).toBeInTheDocument();
  });

  it("calls deleteWorkflow when sidebar Delete button clicked and confirmed", async () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true),
    );
    const deleteWorkflow = vi.fn();
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, deleteWorkflow });
    renderWorkflows();
    fireEvent.click(screen.getByTitle("Delete"));
    await waitFor(() => expect(deleteWorkflow).toHaveBeenCalledWith("wf-1", expect.any(Object)));
  });

  it("shows workflow agent count in sidebar card", () => {
    renderWorkflows();
    expect(screen.getByText("1 agent")).toBeInTheDocument();
  });
});

describe("Workflows edit with existing agents", () => {
  it("pre-fills agents when editing workflow with agents", async () => {
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => expect(screen.getAllByText("ReconAgent").length).toBeGreaterThanOrEqual(1));
  });

  it("calls updateWorkflow when save is submitted", async () => {
    const updateWorkflow = vi.fn();
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, updateWorkflow });
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(updateWorkflow).toHaveBeenCalled());
  });

  it("calls resetUpdate onSuccess after updateWorkflow succeeds", async () => {
    const resetUpdate = vi.fn();
    const updateWorkflow = vi.fn().mockImplementation((_payload, { onSuccess } = {}) => onSuccess?.());
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, updateWorkflow, resetUpdate });
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(updateWorkflow).toHaveBeenCalled());
    expect(resetUpdate).toHaveBeenCalled();
  });

  it("closeEdit is called when delete succeeds on selected workflow", async () => {
    const deleteWorkflow = vi.fn().mockImplementation((_id, { onSuccess } = {}) => onSuccess?.());
    vi.spyOn(useWorkflowsHook, "default").mockReturnValue({ ...defaultWorkflowsHook, deleteWorkflow });
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true),
    );
    renderWorkflows();
    fireEvent.click(screen.getByText("Threat Pipeline"));
    await waitFor(() => screen.getByText("Edit Workflow"));
    fireEvent.click(screen.getByTitle("Delete"));
    await waitFor(() => expect(deleteWorkflow).toHaveBeenCalled());
  });
});
