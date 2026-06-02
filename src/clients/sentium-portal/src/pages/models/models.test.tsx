import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Models from "./models";
import * as useOllamaModelsHook from "../../hooks/useOllamaModels";
import type { PullState } from "../../hooks/useOllamaModels";
import type { OllamaModel, DeleteModelResult } from "../../types/models";

const mockModel: OllamaModel = {
  name: "llama3.2",
  modified_at: "2025-01-01T00:00:00Z",
  size: 2_000_000_000,
  digest: "sha256:abc123",
  details: {
    format: "gguf",
    family: "llama",
    parameter_size: "3.2B",
    quantization_level: "Q4_K_M",
  },
};

const defaultHook = {
  models: [mockModel],
  isLoading: false,
  error: null,
  refetch: vi.fn(),
  pullState: null as PullState | null,
  pull: vi.fn(),
  cancelPull: vi.fn(),
  resetPull: vi.fn(),
  deletingModel: null as string | null,
  deleteModel: vi.fn(),
  deleteResult: null as DeleteModelResult | null,
  clearDeleteResult: vi.fn(),
};

const renderModels = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Models />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useOllamaModelsHook, "default").mockReturnValue(defaultHook);
});

describe("Models loading state", () => {
  it("shows 'Loading models…' when isLoading", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, models: [], isLoading: true });
    renderModels();
    expect(screen.getByText(/loading models/i)).toBeInTheDocument();
  });
});

describe("Models empty state", () => {
  it("shows 'No models installed' when list is empty", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, models: [] });
    renderModels();
    expect(screen.getByText(/no models installed/i)).toBeInTheDocument();
  });
});

describe("Models success state", () => {
  it("renders the page title", () => {
    renderModels();
    expect(screen.getByText("Model Management")).toBeInTheDocument();
  });

  it("renders model name", () => {
    renderModels();
    expect(screen.getByText("llama3.2")).toBeInTheDocument();
  });

  it("renders model size in GB", () => {
    renderModels();
    expect(screen.getByText(/1\.9 GB/)).toBeInTheDocument();
  });

  it("renders model parameter size badge", () => {
    renderModels();
    expect(screen.getByText("3.2B")).toBeInTheDocument();
  });

  it("renders quantization level badge", () => {
    renderModels();
    expect(screen.getByText("Q4_K_M")).toBeInTheDocument();
  });

  it("renders family badge", () => {
    renderModels();
    expect(screen.getByText("llama")).toBeInTheDocument();
  });

  it("renders model count in header badge", () => {
    renderModels();
    expect(screen.getByText("1 installed")).toBeInTheDocument();
  });
});

describe("Models delete flow", () => {
  it("calls deleteModel when delete button is clicked and confirmed", async () => {
    const deleteModel = vi.fn();
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, deleteModel });
    renderModels();
    fireEvent.click(screen.getByTitle("Delete llama3.2"));
    const dialog = await screen.findByRole("dialog");
    const confirmInput = within(dialog).getByRole("textbox");
    fireEvent.change(confirmInput, { target: { value: "llama3.2" } });
    const confirmBtn = within(dialog).getByRole("button", { name: /delete model/i });
    fireEvent.click(confirmBtn);
    await waitFor(() => expect(deleteModel).toHaveBeenCalledWith("llama3.2"));
  });

  it("does NOT call deleteModel when confirmation is cancelled", () => {
    const deleteModel = vi.fn();
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, deleteModel });
    renderModels();
    fireEvent.click(screen.getByTitle("Delete llama3.2"));
    expect(deleteModel).not.toHaveBeenCalled();
  });

  it("shows delete result notice with agents reset info", () => {
    const deleteResult: DeleteModelResult = {
      deletedModel: "llama3.2",
      agentsReset: 2,
      defaultModel: "gemma3:1b",
    };
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, deleteResult });
    renderModels();
    expect(screen.getByText(/2 agents reset to gemma3:1b/)).toBeInTheDocument();
  });

  it("shows delete result notice with no agents affected when agentsReset is 0", () => {
    const deleteResult: DeleteModelResult = {
      deletedModel: "llama3.2",
      agentsReset: 0,
      defaultModel: "gemma3:1b",
    };
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, deleteResult });
    renderModels();
    expect(screen.getByText(/no agents were affected/i)).toBeInTheDocument();
  });

  it("calls clearDeleteResult when dismiss button is clicked", () => {
    const clearDeleteResult = vi.fn();
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      deleteResult: { deletedModel: "llama3.2", agentsReset: 0, defaultModel: "" },
      clearDeleteResult,
    });
    renderModels();
    fireEvent.click(screen.getByLabelText("Dismiss"));
    expect(clearDeleteResult).toHaveBeenCalled();
  });
});

describe("Models pull flow", () => {
  it("disables Pull Model button when input is empty", () => {
    renderModels();
    expect(screen.getByRole("button", { name: /pull model/i })).toBeDisabled();
  });

  it("enables Pull Model button when model name is entered", () => {
    renderModels();
    fireEvent.change(screen.getByLabelText(/model name/i), { target: { value: "mistral" } });
    expect(screen.getByRole("button", { name: /pull model/i })).not.toBeDisabled();
  });

  it("calls pull when form is submitted with a model name", async () => {
    const pull = vi.fn();
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, pull });
    renderModels();
    fireEvent.change(screen.getByLabelText(/model name/i), { target: { value: "mistral" } });
    fireEvent.click(screen.getByRole("button", { name: /pull model/i }));
    await waitFor(() => expect(pull).toHaveBeenCalledWith("mistral"));
  });

  it("shows Cancel button when pulling is in progress", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      pullState: { status: "Pulling...", done: false },
    });
    renderModels();
    expect(screen.getByRole("button", { name: /cancel/i })).toBeInTheDocument();
  });

  it("shows pull progress status text", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      pullState: { status: "Downloading layer", done: false },
    });
    renderModels();
    expect(screen.getByText("Downloading layer")).toBeInTheDocument();
  });

  it("shows pull progress percent when total and completed are set", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      pullState: { status: "Downloading", total: 1000, completed: 500, done: false },
    });
    renderModels();
    expect(screen.getByText(/50%/)).toBeInTheDocument();
  });

  it("shows success message when pull is done", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      pullState: { status: "success", done: true },
    });
    renderModels();
    expect(screen.getByText(/model pulled successfully/i)).toBeInTheDocument();
  });

  it("shows error when pull fails", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      pullState: { status: "", error: "Model not found", done: true },
    });
    renderModels();
    expect(screen.getByText("Model not found")).toBeInTheDocument();
  });

  it("calls cancelPull when Cancel button is clicked", () => {
    const cancelPull = vi.fn();
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      cancelPull,
      pullState: { status: "Pulling", done: false },
    });
    renderModels();
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(cancelPull).toHaveBeenCalled();
  });

  it("calls refetch when refresh icon button is clicked", () => {
    const refetch = vi.fn();
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, refetch });
    renderModels();
    fireEvent.click(screen.getByTitle("Refresh model list"));
    expect(refetch).toHaveBeenCalled();
  });
});

describe("Models edge cases", () => {
  it("does not pull when model name is empty", () => {
    const pull = vi.fn();
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({ ...defaultHook, pull });
    renderModels();
    fireEvent.submit(document.querySelector("form")!);
    expect(pull).not.toHaveBeenCalled();
  });

  it("shows 0 B for model with 0 bytes", () => {
    vi.spyOn(useOllamaModelsHook, "default").mockReturnValue({
      ...defaultHook,
      models: [
        {
          name: "tiny-model",
          size: 0,
          digest: "abc",
          modified_at: "2025-01-01T00:00:00Z",
          details: { format: "", family: "", parameter_size: "1B", quantization_level: "Q4" },
        },
      ],
    });
    renderModels();
    expect(screen.getByText("0 B")).toBeInTheDocument();
  });
});
