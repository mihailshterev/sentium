import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, cleanup, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import KnowledgeBase from "./knowledge-base";
import * as useKnowledgeBaseStatsHook from "../../hooks/useKnowledgeBaseStats";
import * as useAgentLearningsHook from "../../hooks/useAgentLearnings";
import type { KnowledgeBaseCollectionStats, AgentLearning, AgentLearningStats } from "../../types/agentConfig";

const mockCollection: KnowledgeBaseCollectionStats = {
  collectionName: "documents",
  pointCount: 1500,
  vectorSize: 384,
  distanceMetric: "Cosine",
};

const mockStats: AgentLearningStats = {
  totalLearnings: 42,
  pendingIngestion: 3,
  learningsByAgent: { ReconAgent: 20, AnalysisAgent: 22 },
};

const mockLearning: AgentLearning = {
  id: "learn-1",
  agentName: "ReconAgent",
  content: "Important security pattern detected",
  tags: "security,recon",
  conversationId: null,
  capturedAt: "2025-01-01T10:00:00Z",
  isIngested: true,
};

const defaultKbHook = {
  collections: [mockCollection],
  isLoading: false,
  error: null,
  refetch: vi.fn(),
  deleteCollection: vi.fn(),
  isDeleting: false,
};

const defaultLearningsHook = {
  learnings: [mockLearning],
  isLoading: false,
  error: null,
  stats: mockStats,
  isStatsLoading: false,
  capture: vi.fn(),
  isCapturing: false,
  updateLearning: vi.fn(),
  isUpdating: false,
  updatingId: null as string | null,
  deleteLearning: vi.fn(),
  isDeleting: false,
};

const renderKb = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <KnowledgeBase />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue(defaultKbHook);
  vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue(defaultLearningsHook);
});

describe("KnowledgeBase initial render", () => {
  it("renders the page title", () => {
    renderKb();
    expect(screen.getByText("Knowledge Base")).toBeInTheDocument();
  });

  it("renders Global Context and Agent Learnings tabs", () => {
    renderKb();
    expect(screen.getByRole("button", { name: /global context/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /agent learnings/i })).toBeInTheDocument();
  });

  it("shows Global Context tab by default", () => {
    renderKb();
    expect(screen.getByText("Knowledge Base Overview")).toBeInTheDocument();
  });
});

describe("KnowledgeBase tab switching", () => {
  it("switches to Agent Learnings tab when clicked", () => {
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    expect(screen.getByText("Captured Learnings")).toBeInTheDocument();
  });

  it("switches back to Global Context tab", () => {
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    fireEvent.click(screen.getByRole("button", { name: /global context/i }));
    expect(screen.getByText("Knowledge Base Overview")).toBeInTheDocument();
  });
});

describe("KnowledgeBase Global Context tab", () => {
  it("shows loading state for vector store", () => {
    vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue({
      ...defaultKbHook,
      collections: [],
      isLoading: true,
    });
    renderKb();
    expect(screen.getByText(/querying vector store/i)).toBeInTheDocument();
  });

  it("shows error state when stats fail to load", () => {
    vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue({
      ...defaultKbHook,
      collections: [],
      error: new Error("Qdrant unreachable"),
    });
    renderKb();
    expect(screen.getByText(/qdrant unreachable/i)).toBeInTheDocument();
  });

  it("renders collection stats", () => {
    renderKb();
    expect(screen.getAllByText("1").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/1[,.]?500|1500/).length).toBeGreaterThanOrEqual(1);
  });

  it("renders learnings stats from agent learnings", () => {
    renderKb();
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders collection in the list with its name", () => {
    renderKb();
    expect(screen.getByText("documents")).toBeInTheDocument();
  });

  it("renders Active pill for non-empty collection", () => {
    renderKb();
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("renders Empty pill for zero-point collection", () => {
    vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue({
      ...defaultKbHook,
      collections: [{ ...mockCollection, pointCount: 0 }],
    });
    renderKb();
    expect(screen.getByText("Empty")).toBeInTheDocument();
  });

  it("shows 'No collections found' when list is empty", () => {
    vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue({
      ...defaultKbHook,
      collections: [],
    });
    renderKb();
    expect(screen.getByText(/no collections found/i)).toBeInTheDocument();
  });

  it("renders learnings-by-agent section when stats are available", () => {
    renderKb();
    expect(screen.getByText("Learnings by Agent")).toBeInTheDocument();
    expect(screen.getByText("ReconAgent")).toBeInTheDocument();
    expect(screen.getByText("AnalysisAgent")).toBeInTheDocument();
  });

  it("does not render learnings-by-agent when stats is null", () => {
    vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue({
      ...defaultLearningsHook,
      stats: undefined,
    });
    renderKb();
    expect(screen.queryByText("Learnings by Agent")).not.toBeInTheDocument();
  });

  it("calls refetch when Refresh button is clicked", () => {
    const refetch = vi.fn();
    vi.spyOn(useKnowledgeBaseStatsHook, "useKnowledgeBaseStats").mockReturnValue({
      ...defaultKbHook,
      refetch,
    });
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    expect(refetch).toHaveBeenCalled();
  });
});

describe("KnowledgeBase Agent Learnings tab", () => {
  beforeEach(() => {
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
  });

  it("renders learning content", () => {
    expect(screen.getByText("Important security pattern detected")).toBeInTheDocument();
  });

  it("renders agent name for the learning", () => {
    expect(screen.getByText("ReconAgent")).toBeInTheDocument();
  });

  it("renders total learnings badge", () => {
    expect(screen.getByText("42 total")).toBeInTheDocument();
  });

  it("renders agent filter dropdown", () => {
    expect(screen.getByRole("combobox")).toBeInTheDocument();
  });

  it("filters learnings when agent filter is changed", () => {
    const select = screen.getByRole("combobox") as HTMLSelectElement;
    fireEvent.change(select, { target: { value: "All" } });
    expect(select).toBeInTheDocument();
  });

  it("shows loading text while learnings load", () => {
    cleanup();
    vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue({
      ...defaultLearningsHook,
      learnings: [],
      isLoading: true,
    });
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    expect(screen.getByText(/loading learnings/i)).toBeInTheDocument();
  });

  it("shows empty state when no learnings", () => {
    cleanup();
    vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue({
      ...defaultLearningsHook,
      learnings: [],
      stats: { ...mockStats, learningsByAgent: {} },
    });
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    expect(screen.getByText(/no learnings captured yet/i)).toBeInTheDocument();
  });
});

describe("KnowledgeBase Learning card edit", () => {
  beforeEach(() => {
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
  });

  it("opens edit mode when Edit learning button is clicked", () => {
    fireEvent.click(screen.getByTitle("Edit learning"));
    expect(screen.getByPlaceholderText(/e.g. workflow, memory, agent/i)).toBeInTheDocument();
  });

  it("shows Cancel and Save buttons in edit mode", () => {
    fireEvent.click(screen.getByTitle("Edit learning"));
    expect(screen.getByTitle("Cancel")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /save/i })).toBeInTheDocument();
  });

  it("cancels edit when Cancel is clicked", () => {
    fireEvent.click(screen.getByTitle("Edit learning"));
    fireEvent.click(screen.getByTitle("Cancel"));
    expect(screen.queryByPlaceholderText(/e.g. workflow, memory, agent/i)).not.toBeInTheDocument();
  });

  it("editing content populates textarea with existing content", () => {
    fireEvent.click(screen.getByTitle("Edit learning"));
    const textarea = document.querySelector("textarea") as HTMLTextAreaElement;
    expect(textarea.value).toContain("Important security pattern");
  });

  it("can change content in edit textarea", () => {
    fireEvent.click(screen.getByTitle("Edit learning"));
    const textarea = document.querySelector("textarea") as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: "Updated content" } });
    expect(textarea.value).toBe("Updated content");
  });

  it("can change tags in edit input", () => {
    fireEvent.click(screen.getByTitle("Edit learning"));
    const tagsInput = screen.getByPlaceholderText(/e.g. workflow, memory, agent/i) as HTMLInputElement;
    fireEvent.change(tagsInput, { target: { value: "new, tags" } });
    expect(tagsInput.value).toBe("new, tags");
  });

  it("calls updateLearning when Save is clicked with changed content", () => {
    const updateLearning = vi.fn();
    cleanup();
    vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue({
      ...defaultLearningsHook,
      updateLearning,
    });
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    fireEvent.click(screen.getByTitle("Edit learning"));
    const textarea = document.querySelector("textarea") as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: "Updated content" } });
    fireEvent.click(screen.getByRole("button", { name: /save/i }));
    expect(updateLearning).toHaveBeenCalledWith(expect.objectContaining({ id: "learn-1", content: "Updated content" }));
  });

  it("delete button calls delete learning", async () => {
    const deleteLearning = vi.fn().mockResolvedValue({});
    cleanup();
    vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue({
      ...defaultLearningsHook,
      deleteLearning,
    });
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    fireEvent.click(screen.getByTitle("Delete learning"));
    await waitFor(() => expect(deleteLearning).toHaveBeenCalledWith("learn-1"));
  });

  it("shows Pending badge for non-ingested learning", () => {
    cleanup();
    vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue({
      ...defaultLearningsHook,
      learnings: [{ ...mockLearning, isIngested: false }],
    });
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    expect(screen.getByText("Pending")).toBeInTheDocument();
  });

  it("shows Saving state while update is pending", () => {
    cleanup();
    vi.spyOn(useAgentLearningsHook, "useAgentLearnings").mockReturnValue({
      ...defaultLearningsHook,
      isUpdating: true,
      updatingId: "learn-1",
    });
    renderKb();
    fireEvent.click(screen.getByRole("button", { name: /agent learnings/i }));
    fireEvent.click(screen.getByTitle("Edit learning"));
    expect(screen.getByText(/saving/i)).toBeInTheDocument();
  });
});
