import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ModelsView as Models } from "./models-view";
import * as useModelCatalogHook from "@/features/models/hooks/useModelCatalog";
import * as useRoleHook from "@/shared/auth/hooks/useRole";
import type { InferenceModel } from "@/features/models/types/models";

const loaded: InferenceModel = { id: "gemma-4-e4b", status: "loaded", contextLength: 16384 };
const unloaded: InferenceModel = { id: "gemma-4-e2b", status: "unloaded", contextLength: null };

type CatalogHook = ReturnType<typeof useModelCatalogHook.useModelCatalog>;

const hook = (overrides: Partial<CatalogHook> = {}): CatalogHook =>
  ({
    models: [loaded, unloaded],
    defaultModel: "gemma-4-e4b",
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    load: vi.fn(),
    unload: vi.fn(),
    pendingModel: null,
    actionError: null,
    clearActionError: vi.fn(),
    ...overrides,
  }) as unknown as CatalogHook;

const asRole = (isSovereign: boolean) =>
  vi.spyOn(useRoleHook, "useRole").mockReturnValue({
    isSovereign,
    isMemberOrAbove: true,
    isAuthenticated: true,
    roles: isSovereign ? ["Sovereign"] : ["Member"],
    highestRole: isSovereign ? ("Sovereign" as const) : ("Member" as const),
    hasRole: () => isSovereign,
  });

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
  vi.restoreAllMocks();
  asRole(true);
});

describe("ModelsView", () => {
  it("renders the heading and how many models are loaded", () => {
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(hook());
    renderModels();

    expect(screen.getByRole("heading", { name: "Model Management" })).toBeInTheDocument();
    expect(screen.getByText("1 / 2 loaded")).toBeInTheDocument();
  });

  it("lists each model with its status, context window and the default marker", () => {
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(hook());
    renderModels();

    expect(screen.getByText("gemma-4-e4b")).toBeInTheDocument();
    expect(screen.getByText("gemma-4-e2b")).toBeInTheDocument();
    expect(screen.getByText("loaded")).toBeInTheDocument();
    expect(screen.getByText("unloaded")).toBeInTheDocument();
    expect(screen.getByText("16k ctx")).toBeInTheDocument();
    expect(screen.getAllByText("default")).toHaveLength(1);
  });

  it("lets a Sovereign unload a loaded model and load an unloaded one", () => {
    const state = hook();
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(state);
    renderModels();

    fireEvent.click(screen.getByTitle("Unload gemma-4-e4b from memory"));
    fireEvent.click(screen.getByTitle("Load gemma-4-e2b into memory"));

    expect(state.unload).toHaveBeenCalledWith("gemma-4-e4b");
    expect(state.load).toHaveBeenCalledWith("gemma-4-e2b");
  });

  it("disables loading while a model is already loading", () => {
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(
      hook({ models: [{ id: "gemma-4-e2b", status: "loading", contextLength: null }] }),
    );
    renderModels();

    expect(screen.getByTitle("Load gemma-4-e2b into memory")).toBeDisabled();
  });

  it("hides load and unload from non-Sovereign users", () => {
    asRole(false);
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(hook());
    renderModels();

    expect(screen.queryByTitle(/Unload gemma-4-e4b/)).not.toBeInTheDocument();
    expect(screen.queryByTitle(/Load gemma-4-e2b/)).not.toBeInTheDocument();
    expect(screen.getByText(/requires Sovereign access/i)).toBeInTheDocument();
  });

  it("shows an empty state when the server declares no models", () => {
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(hook({ models: [] }));
    renderModels();

    expect(screen.getByText("No models available")).toBeInTheDocument();
  });

  it("shows an error when the inference server is unavailable", () => {
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(hook({ models: [], isError: true }));
    renderModels();

    expect(screen.getByRole("alert")).toHaveTextContent(/inference server is unavailable/i);
  });

  it("shows a failed action and lets the user dismiss it", () => {
    const state = hook({ actionError: new Error("Model 'gemma-4-e2b' is still changing state; try again shortly.") });
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(state);
    renderModels();

    expect(screen.getByRole("alert")).toHaveTextContent(/still changing state/);
    fireEvent.click(screen.getByLabelText("Dismiss"));
    expect(state.clearActionError).toHaveBeenCalled();
  });

  it("refreshes the model list on demand", () => {
    const state = hook();
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(state);
    renderModels();

    fireEvent.click(screen.getByTitle("Refresh model list"));
    expect(state.refetch).toHaveBeenCalled();
  });

  it("explains that models are declared in deployment configuration", () => {
    vi.spyOn(useModelCatalogHook, "useModelCatalog").mockReturnValue(hook());
    renderModels();

    expect(screen.getByText("Adding Models")).toBeInTheDocument();
    expect(screen.getByText("infra/llm/models.ini")).toBeInTheDocument();
  });
});
