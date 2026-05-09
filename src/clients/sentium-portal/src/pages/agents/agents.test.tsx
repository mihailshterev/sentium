import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Agents from "./agents";
import * as useAgentsHook from "../../hooks/useAgents";
import * as useModelsHook from "../../hooks/useModels";
import type { AgentRecord } from "../../types/agents";

const mockAgent: AgentRecord = {
  id: "agent-1",
  name: "ForensicsAgent",
  description: "Analyzes forensic data",
  model: "llama3.2",
  createdAt: "2025-01-15T00:00:00Z",
  updatedAt: "2025-01-15T00:00:00Z",
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

const defaultModelsHook = {
  models: ["llama3.2", "gemma3:1b"],
  isLoading: false,
};

const renderAgents = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Agents />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useAgentsHook, "default").mockReturnValue(defaultAgentsHook);
  vi.spyOn(useModelsHook, "default").mockReturnValue(defaultModelsHook);
});

describe("Agents – loading state", () => {
  it("shows loading spinner when loading", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, agents: [], isLoading: true });
    renderAgents();
    expect(screen.getByText(/loading registry/i)).toBeInTheDocument();
  });
});

describe("Agents empty state", () => {
  it("shows 'No agents registered yet' when list is empty", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, agents: [] });
    renderAgents();
    expect(screen.getByText(/no agents registered yet/i)).toBeInTheDocument();
  });
});

describe("Agents list state", () => {
  it("renders the page title", () => {
    renderAgents();
    expect(screen.getByText("Agent Registry")).toBeInTheDocument();
  });

  it("renders agent name", () => {
    renderAgents();
    expect(screen.getByText("ForensicsAgent")).toBeInTheDocument();
  });

  it("renders agent description", () => {
    renderAgents();
    expect(screen.getByText("Analyzes forensic data")).toBeInTheDocument();
  });

  it("renders agent model badge", () => {
    renderAgents();
    expect(screen.getAllByText("llama3.2").length).toBeGreaterThanOrEqual(1);
  });

  it("renders agent count badge", () => {
    renderAgents();
    expect(screen.getAllByText("1 registered").length).toBeGreaterThan(0);
  });

  it("renders 'No description provided.' when agent has no description", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({
      ...defaultAgentsHook,
      agents: [{ ...mockAgent, description: "" }],
    });
    renderAgents();
    expect(screen.getByText("No description provided.")).toBeInTheDocument();
  });

  it("renders model hidden badge when model is empty", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({
      ...defaultAgentsHook,
      agents: [{ ...mockAgent, model: "" }],
    });
    renderAgents();
    const modelOccurrences = screen.queryAllByText("llama3.2");
    expect(
      modelOccurrences.every((el) => el.tagName === "OPTION" || el.role === "option" || el.closest("select") !== null),
    ).toBeTruthy();
  });
});

describe("Agents create form", () => {
  it("renders model select when models are available", () => {
    renderAgents();
    expect(screen.getByRole("combobox")).toBeInTheDocument();
  });

  it("renders model text input when no models are available", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    renderAgents();
    const modelInput = screen.getByPlaceholderText(/e\.g\. gemma3:1b/i);
    expect(modelInput).toBeInTheDocument();
  });

  it("can change model text input value in create form when no models", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    renderAgents();
    const modelInput = screen.getByPlaceholderText(/e\.g\. gemma3:1b/i) as HTMLInputElement;
    fireEvent.change(modelInput, { target: { value: "my-custom-model" } });
    expect(modelInput.value).toBe("my-custom-model");
  });

  it("Register Agent button is disabled when name is empty", () => {
    renderAgents();
    expect(screen.getByRole("button", { name: /register agent/i })).toBeDisabled();
  });

  it("Register Agent button is enabled when name is entered", () => {
    renderAgents();
    fireEvent.change(screen.getByLabelText(/agent name/i), { target: { value: "TestAgent" } });
    expect(screen.getByRole("button", { name: /register agent/i })).not.toBeDisabled();
  });

  it("calls createAgent with correct payload when form is submitted", () => {
    const createAgent = vi.fn();
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, createAgent });
    renderAgents();
    fireEvent.change(screen.getByLabelText(/agent name/i), { target: { value: "MyAgent" } });
    fireEvent.change(screen.getByLabelText(/description/i), { target: { value: "Test desc" } });
    fireEvent.click(screen.getByRole("button", { name: /register agent/i }));
    expect(createAgent).toHaveBeenCalledWith(
      expect.objectContaining({ name: "MyAgent", description: "Test desc" }),
      expect.any(Object),
    );
  });

  it("shows 'Registering...' when isCreatingAgent", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isCreatingAgent: true });
    renderAgents();
    expect(screen.getByText(/registering\.\.\./i)).toBeInTheDocument();
  });

  it("shows success message after agent is created", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isCreateSuccess: true });
    renderAgents();
    expect(screen.getByText(/agent registered successfully/i)).toBeInTheDocument();
  });

  it("shows error message when create fails", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({
      ...defaultAgentsHook,
      isCreateError: true,
      createAgentError: new Error("Duplicate agent name"),
    });
    renderAgents();
    expect(screen.getByText(/duplicate agent name/i)).toBeInTheDocument();
  });

  it("shows 'Unknown error' when createAgentError has no message", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({
      ...defaultAgentsHook,
      isCreateError: true,
      createAgentError: null,
    });
    renderAgents();
    expect(screen.getByText(/unknown error/i)).toBeInTheDocument();
  });

  it("shows character count for description field", () => {
    renderAgents();
    fireEvent.change(screen.getByLabelText(/description/i, { selector: "textarea" }), { target: { value: "Hello" } });
    expect(screen.getByText("5/1000")).toBeInTheDocument();
  });
});

describe("Agents delete", () => {
  it("calls deleteAgent when confirmed", () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true),
    );
    const deleteAgent = vi.fn();
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, deleteAgent });
    renderAgents();
    fireEvent.click(screen.getByTitle("Delete agent"));
    expect(deleteAgent).toHaveBeenCalledWith("agent-1", expect.any(Object));
  });

  it("does not call deleteAgent when confirmation cancelled", () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => false),
    );
    const deleteAgent = vi.fn();
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, deleteAgent });
    renderAgents();
    fireEvent.click(screen.getByTitle("Delete agent"));
    expect(deleteAgent).not.toHaveBeenCalled();
  });

  it("calls alert when deleteAgent fails", () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true),
    );
    vi.stubGlobal("alert", vi.fn());
    const deleteAgent = vi.fn().mockImplementation((_id, { onError } = {}) => onError?.(new Error("Delete failed")));
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, deleteAgent });
    renderAgents();
    fireEvent.click(screen.getByTitle("Delete agent"));
    expect(vi.mocked(window.alert)).toHaveBeenCalledWith("Delete failed");
  });
});

describe("Agents edit modal", () => {
  it("opens edit modal when Edit button is clicked", () => {
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect(screen.getByText("Edit Agent")).toBeInTheDocument();
  });

  it("pre-fills the edit form with agent data", () => {
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect((screen.getByLabelText(/agent name/i, { selector: "#edit-name" }) as HTMLInputElement).value).toBe(
      "ForensicsAgent",
    );
  });

  it("closes the edit modal when X is clicked", async () => {
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect(screen.getByText("Edit Agent")).toBeInTheDocument();
    const overlay = document.querySelector("[class*='modalOverlay']") as HTMLElement;
    if (overlay) {
      fireEvent.click(overlay);
      await waitFor(() => expect(screen.queryByText("Edit Agent")).not.toBeInTheDocument());
    } else {
      fireEvent.keyDown(document, { key: "Escape" });
    }
  });

  it("closes modal when overlay is clicked", () => {
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect(screen.getByText("Edit Agent")).toBeInTheDocument();
    const modalEl = document.querySelector("[class*='modal']") as HTMLElement;
    if (modalEl) {
      fireEvent.click(modalEl);
    }
  });

  it("calls updateAgent when edit form is submitted", async () => {
    const updateAgent = vi.fn();
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, updateAgent });
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    fireEvent.change(screen.getByLabelText(/agent name/i, { selector: "#edit-name" }), {
      target: { value: "UpdatedAgent" },
    });
    fireEvent.submit(document.querySelectorAll("form")[1]!);
    await waitFor(() =>
      expect(updateAgent).toHaveBeenCalledWith(
        expect.objectContaining({ id: "agent-1", name: "UpdatedAgent" }),
        expect.any(Object),
      ),
    );
  });

  it("closes modal after updateAgent onSuccess", async () => {
    const updateAgent = vi.fn().mockImplementation((_payload, { onSuccess } = {}) => onSuccess?.());
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, updateAgent });
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    fireEvent.submit(document.querySelectorAll("form")[1]!);
    await waitFor(() => expect(updateAgent).toHaveBeenCalled());
    expect(updateAgent).toHaveBeenCalled();
  });

  it("shows 'Saving...' in modal submit button when isUpdatingAgent", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isUpdatingAgent: true });
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect(screen.getByText(/saving\.\.\./i)).toBeInTheDocument();
  });

  it("shows 'Saved' in modal submit button when isUpdateSuccess", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isUpdateSuccess: true });
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect(screen.getByText("Saved")).toBeInTheDocument();
  });

  it("shows update error in modal", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({
      ...defaultAgentsHook,
      isUpdateError: true,
      updateAgentError: new Error("Update conflict"),
    });
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect(screen.getByText(/update conflict/i)).toBeInTheDocument();
  });

  it("renders model select in edit modal when models are available", () => {
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    expect(screen.getByLabelText(/model/i, { selector: "#edit-model" })).toBeInTheDocument();
  });

  it("renders model text input in edit modal when no models", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    const modelInput = document.getElementById("edit-model");
    expect(modelInput?.tagName.toLowerCase()).toBe("input");
  });

  it("can change description in edit modal", () => {
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    const desc = document.getElementById("edit-description") as HTMLTextAreaElement;
    fireEvent.change(desc, { target: { value: "Updated description" } });
    expect(desc.value).toBe("Updated description");
  });

  it("can change model in edit modal", () => {
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    const modelSelect = document.getElementById("edit-model") as HTMLSelectElement;
    fireEvent.change(modelSelect, { target: { value: "gemma3:1b" } });
    expect(modelSelect.value).toBe("gemma3:1b");
  });

  it("can change model text input in edit modal when no models", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    renderAgents();
    fireEvent.click(screen.getByTitle("Edit agent"));
    const modelInput = document.getElementById("edit-model") as HTMLInputElement;
    fireEvent.change(modelInput, { target: { value: "custom:latest" } });
    expect(modelInput.value).toBe("custom:latest");
  });
});

describe("Agents create form", () => {
  it("shows char count for description field", () => {
    renderAgents();
    fireEvent.change(screen.getByLabelText(/description/i, { selector: "#agent-description" }), {
      target: { value: "Hello World" },
    });
    expect(screen.getByText("11/1000")).toBeInTheDocument();
  });

  it("shows 'Registering...' while creating", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isCreatingAgent: true });
    renderAgents();
    expect(screen.getByText(/registering\.\.\./i)).toBeInTheDocument();
  });

  it("shows success message after registration", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, isCreateSuccess: true });
    renderAgents();
    expect(screen.getByText(/registered successfully/i)).toBeInTheDocument();
  });

  it("shows error message when registration fails", () => {
    vi.spyOn(useAgentsHook, "default").mockReturnValue({
      ...defaultAgentsHook,
      isCreateError: true,
      createAgentError: new Error("Name conflict"),
    });
    renderAgents();
    expect(screen.getByText(/name conflict/i)).toBeInTheDocument();
  });

  it("register button is disabled when name is empty", () => {
    renderAgents();
    const registerBtn = screen.getByRole("button", { name: /register agent/i });
    expect(registerBtn).toBeDisabled();
  });

  it("register button is enabled when name is entered", () => {
    renderAgents();
    fireEvent.change(screen.getByLabelText(/agent name/i, { selector: "#agent-name" }), {
      target: { value: "MyAgent" },
    });
    const registerBtn = screen.getByRole("button", { name: /register agent/i });
    expect(registerBtn).not.toBeDisabled();
  });

  it("renders model select when models available", () => {
    renderAgents();
    expect(document.getElementById("agent-model")).toBeInTheDocument();
    expect(document.getElementById("agent-model")?.tagName.toLowerCase()).toBe("select");
  });

  it("renders model text input when no models available", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    renderAgents();
    expect(document.getElementById("agent-model")?.tagName.toLowerCase()).toBe("input");
  });

  it("calls createAgent when form submitted with name", async () => {
    const createAgent = vi.fn();
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, createAgent });
    renderAgents();
    fireEvent.change(screen.getByLabelText(/agent name/i, { selector: "#agent-name" }), {
      target: { value: "SentinelBot" },
    });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() =>
      expect(createAgent).toHaveBeenCalledWith(expect.objectContaining({ name: "SentinelBot" }), expect.any(Object)),
    );
  });

  it("clears form when createAgent onSuccess is called", async () => {
    const createAgent = vi.fn().mockImplementation((_payload, { onSuccess } = {}) => onSuccess?.());
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, createAgent });
    renderAgents();
    const nameInput = screen.getByLabelText(/agent name/i, { selector: "#agent-name" }) as HTMLInputElement;
    fireEvent.change(nameInput, { target: { value: "SentinelBot" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(nameInput.value).toBe(""));
  });

  it("can change model selection in create form", () => {
    renderAgents();
    const modelSelect = document.getElementById("agent-model") as HTMLSelectElement;
    fireEvent.change(modelSelect, { target: { value: "gemma3:1b" } });
    expect(modelSelect.value).toBe("gemma3:1b");
  });
});

describe("Agents delete confirmation", () => {
  it("does not call deleteAgent when confirmation cancelled", () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => false),
    );
    const deleteAgent = vi.fn();
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, deleteAgent });
    renderAgents();
    fireEvent.click(screen.getByTitle("Delete agent"));
    expect(deleteAgent).not.toHaveBeenCalled();
  });

  it("calls deleteAgent when confirmation accepted", async () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true),
    );
    const deleteAgent = vi.fn();
    vi.spyOn(useAgentsHook, "default").mockReturnValue({ ...defaultAgentsHook, deleteAgent });
    renderAgents();
    fireEvent.click(screen.getByTitle("Delete agent"));
    await waitFor(() => expect(deleteAgent).toHaveBeenCalledWith("agent-1", expect.any(Object)));
  });
});
