import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import WorkflowsList from "./workflows";
import * as useWorkflowsHook from "../../hooks/useWorkflows";
import type { WorkflowRecord } from "../../types/workflows";

const navigate = vi.fn();
vi.mock("react-router", async (orig) => ({
  ...(await orig<typeof import("react-router")>()),
  useNavigate: () => navigate,
}));

const workflow = { id: "wf1", name: "Pipeline", description: "d", agents: [{ id: "a1" }] } as unknown as WorkflowRecord;
const deleteWorkflow = vi.fn();

const setHook = (overrides: Record<string, unknown> = {}) =>
  vi.spyOn(useWorkflowsHook, "default").mockReturnValue({
    workflows: [workflow],
    isLoading: false,
    isError: false,
    deleteWorkflow,
    createWorkflow: vi.fn(),
    updateWorkflow: vi.fn(),
    ...overrides,
  } as unknown as ReturnType<typeof useWorkflowsHook.default>);

beforeEach(() => {
  navigate.mockReset();
  deleteWorkflow.mockReset();
  setHook();
});

describe("WorkflowsList", () => {
  it("shows a loading state", () => {
    setHook({ workflows: [], isLoading: true });
    render(<WorkflowsList />);
    expect(screen.getByText(/loading workflows/i)).toBeInTheDocument();
  });

  it("shows an empty state with a create action", () => {
    setHook({ workflows: [] });
    render(<WorkflowsList />);
    expect(screen.getByText("No workflows yet")).toBeInTheDocument();
  });

  it("renders workflow cards and navigates on select", () => {
    render(<WorkflowsList />);
    expect(screen.getByText("Pipeline")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Pipeline"));
    expect(navigate).toHaveBeenCalledWith("/workflows/wf1");
  });

  it("navigates to the new-workflow route", () => {
    render(<WorkflowsList />);
    fireEvent.click(screen.getByRole("button", { name: /new workflow/i }));
    expect(navigate).toHaveBeenCalledWith("/workflows/new");
  });

  it("confirms and deletes a workflow", async () => {
    render(<WorkflowsList />);
    fireEvent.click(screen.getByTestId("workflow-delete-Pipeline"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByTestId("confirm-dialog-confirm"));
    expect(deleteWorkflow).toHaveBeenCalledWith("wf1", expect.any(Object));
  });
});
