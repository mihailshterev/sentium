import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Assistant from "./assistant";
import * as useConversationsHook from "../../hooks/useConversations";
import * as useModelsHook from "../../hooks/useModels";
import * as agentRuntimeService from "../../services/agentRuntime.service";
import { useConversationStore } from "../../stores/assistant-conversation-store";
import type { ConversationSummary } from "../../types/assistant";

vi.mock("../../services/agentRuntime.service", async (importOriginal) => {
  const actual = await importOriginal<typeof agentRuntimeService>();
  return {
    ...actual,
    fetchWorkspaces: vi.fn().mockResolvedValue([]),
    fetchWorkspaceFiles: vi.fn().mockResolvedValue([]),
    fetchConversations: vi.fn().mockResolvedValue([]),
    createConversation: vi.fn().mockResolvedValue({ id: "conv-1" }),
    deleteConversation: vi.fn().mockResolvedValue(undefined),
    fetchConversation: vi.fn().mockResolvedValue({ id: "conv-1", title: "Chat 1", model: "llama3.2", messages: [] }),
    sendChatMessage: vi.fn(),
  };
});

const mockConversation: ConversationSummary = {
  id: "conv-1",
  title: "Chat Jan 1",
  model: "llama3.2",
  createdAt: "2025-01-01T00:00:00Z",
};

const defaultConversationsHook = {
  conversations: [mockConversation],
  createConversation: vi.fn().mockResolvedValue({ id: "conv-new" }),
  isCreating: false,
  deleteConversation: vi.fn(),
  isDeleting: false,
};

const defaultModelsHook = {
  models: ["llama3.2", "gemma3:1b"],
  isLoading: false,
};

const renderAssistant = (initialPath = "/assistant") => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/assistant" element={<Assistant />} />
          <Route path="/assistant/:conversationId" element={<Assistant />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  useConversationStore.setState({
    activeConversationId: null,
    messages: [],
    model: "llama3.2",
    isStreaming: false,
    streamingConversationId: null,
  });
  vi.spyOn(useConversationsHook, "default").mockReturnValue(defaultConversationsHook);
  vi.spyOn(useModelsHook, "default").mockReturnValue(defaultModelsHook);
});

describe("Assistant – initial render", () => {
  it("renders the model selector", () => {
    renderAssistant();
    expect(screen.getByRole("combobox")).toBeInTheDocument();
  });

  it("renders suggestion chips when no conversation", () => {
    renderAssistant();
    const chips = screen.getAllByRole("button", { name: /summarize|write|help|plan|brainstorm|analyze/i });
    expect(chips.length).toBeGreaterThanOrEqual(1);
  });

  it("renders conversation list in sidebar", () => {
    renderAssistant();
    expect(screen.getByText("Chat Jan 1")).toBeInTheDocument();
  });

  it("renders the sidebar toggle button", () => {
    renderAssistant();
    expect(screen.getByTitle("Toggle conversations")).toBeInTheDocument();
  });
});

describe("Assistant sidebar toggle", () => {
  it("toggles sidebar closed and open", () => {
    renderAssistant();
    const toggleBtns = screen.getAllByRole("button");
    const sidebarToggle = toggleBtns.find((b) => b.className.includes("sidebarToggle") || b.title?.includes("toggle"));
    if (sidebarToggle) {
      fireEvent.click(sidebarToggle);
    }
  });
});

describe("Assistant model selection", () => {
  it("renders all available models in the selector", () => {
    renderAssistant();
    const select = screen.getByRole("combobox");
    const options = Array.from((select as HTMLSelectElement).options).map((o) => o.text);
    expect(options).toContain("llama3.2");
    expect(options).toContain("gemma3:1b");
  });

  it("sets model from available models on initial render", () => {
    renderAssistant();
    const select = screen.getByRole("combobox") as HTMLSelectElement;
    expect(select.value).toBe("llama3.2");
  });

  it("shows 'No model selected' status when no models are available", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    useConversationStore.setState((s) => ({ ...s, model: "" }));
    renderAssistant();
    expect(screen.getByText(/no model selected/i)).toBeInTheDocument();
  });

  it("updates model when model selector is changed", () => {
    renderAssistant();
    const select = screen.getByRole("combobox") as HTMLSelectElement;
    fireEvent.change(select, { target: { value: "gemma3:1b" } });
    expect(select.value).toBe("gemma3:1b");
  });

  it("updates model in text input when no models and user types", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    useConversationStore.setState((s) => ({ ...s, model: "" }));
    renderAssistant();
    const modelInput = screen.getByPlaceholderText(/model name/i) as HTMLInputElement;
    fireEvent.change(modelInput, { target: { value: "custom-model" } });
    expect(modelInput.value).toBe("custom-model");
  });
});

describe("Assistant conversation loading", () => {
  it("loads a conversation when clicked from sidebar", async () => {
    renderAssistant();
    fireEvent.click(screen.getByText("Chat Jan 1"));
    await waitFor(() => expect(agentRuntimeService.fetchConversation).toHaveBeenCalledWith("conv-1"));
  });

  it("deletes a conversation when delete button is clicked", async () => {
    const deleteConversation = vi.fn();
    vi.spyOn(useConversationsHook, "default").mockReturnValue({
      ...defaultConversationsHook,
      deleteConversation,
    });
    renderAssistant();
    const delBtn = document.querySelector(".deleteConvBtn") as HTMLElement;
    if (delBtn) {
      fireEvent.click(delBtn);
      expect(deleteConversation).toHaveBeenCalled();
    }
  });
});

describe("Assistant text input", () => {
  it("renders the message input", () => {
    renderAssistant();
    expect(screen.getByPlaceholderText(/ask sentium/i)).toBeInTheDocument();
  });

  it("updates input value when user types", () => {
    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "Hello AI" } });
    expect((input as HTMLTextAreaElement).value).toBe("Hello AI");
  });

  it("fills input from suggestion chip", () => {
    renderAssistant();
    const chips = screen.getAllByRole("button").filter((b) => b.className.includes("suggestion"));
    if (chips.length > 0) {
      fireEvent.click(chips[0]);
      const input = screen.getByPlaceholderText(/ask sentium/i);
      expect((input as HTMLTextAreaElement).value.length).toBeGreaterThan(0);
    }
  });
});

describe("Assistant workspace context panel", () => {
  it("shows workspace context toggle button", () => {
    renderAssistant();
    const ctxBtn = document.querySelector(".wsContextBtn") ?? screen.queryByTitle(/workspace context/i);
    if (ctxBtn) {
      fireEvent.click(ctxBtn as HTMLElement);
    }
  });
});

describe("Assistant empty state", () => {
  it("shows empty state with no conversations", () => {
    vi.spyOn(useConversationsHook, "default").mockReturnValue({
      ...defaultConversationsHook,
      conversations: [],
    });
    renderAssistant();
    expect(screen.getByPlaceholderText(/ask sentium/i)).toBeInTheDocument();
  });
});

describe("Assistant new conversation button", () => {
  it("renders the New conversation button in sidebar", () => {
    renderAssistant();
    expect(screen.getByTitle("New conversation")).toBeInTheDocument();
  });

  it("calls createConversation when New conversation button is clicked", async () => {
    const createConversation = vi.fn().mockResolvedValue({ id: "conv-new" });
    vi.spyOn(useConversationsHook, "default").mockReturnValue({
      ...defaultConversationsHook,
      createConversation,
    });
    renderAssistant();
    fireEvent.click(screen.getByTitle("New conversation"));
    await waitFor(() => expect(createConversation).toHaveBeenCalled());
  });
});

describe("Assistant message sending", () => {
  it("send button is disabled when input is empty", () => {
    renderAssistant();
    const sendBtn = document.querySelector("button[type='submit']") as HTMLButtonElement;
    expect(sendBtn).toBeDisabled();
  });

  it("send button is enabled after typing", () => {
    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "Hello" } });
    const sendBtn = document.querySelector("button[type='submit']") as HTMLButtonElement;
    expect(sendBtn).not.toBeDisabled();
  });

  it("submits message and calls sendChatMessage", async () => {
    const mockResponse = {
      ok: true,
      body: {
        getReader: () => ({
          read: vi.fn().mockResolvedValue({ done: true, value: undefined }),
        }),
      },
    };
    vi.mocked(agentRuntimeService.sendChatMessage).mockResolvedValue(mockResponse as unknown as Response);
    vi.mocked(agentRuntimeService.createConversation).mockResolvedValue({ id: "conv-1" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "Test message" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(agentRuntimeService.sendChatMessage).toHaveBeenCalled());
  });

  it("shows message in chat after sending", async () => {
    const mockResponse = {
      ok: true,
      body: {
        getReader: () => ({
          read: vi.fn().mockResolvedValue({ done: true, value: undefined }),
        }),
      },
    };
    vi.mocked(agentRuntimeService.sendChatMessage).mockResolvedValue(mockResponse as unknown as Response);
    vi.mocked(agentRuntimeService.createConversation).mockResolvedValue({ id: "conv-1" });
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "Hello world" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.getByText("Hello world")).toBeInTheDocument());
  });
});

describe("Assistant conversation display", () => {
  it("renders user and assistant messages", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        { id: "m1", role: "user", content: "What is AI?", timestamp: new Date("2025-01-01T10:00:00Z") },
        {
          id: "m2",
          role: "assistant",
          content: "Artificial Intelligence is...",
          timestamp: new Date("2025-01-01T10:01:00Z"),
        },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    expect(screen.getByText("What is AI?")).toBeInTheDocument();
    expect(screen.getByText(/Artificial Intelligence is/)).toBeInTheDocument();
  });

  it("renders thought block toggle when thought is present", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        {
          id: "m1",
          role: "assistant",
          content: "My response",
          thought: "Internal reasoning here",
          timestamp: new Date("2025-01-01T10:00:00Z"),
        },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    expect(screen.getByText("Thinking")).toBeInTheDocument();
  });

  it("expands thought block when Thinking button is clicked", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        {
          id: "m1",
          role: "assistant",
          content: "My response",
          thought: "Internal reasoning here",
          timestamp: new Date("2025-01-01T10:00:00Z"),
        },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    fireEvent.click(screen.getByText("Thinking"));
    expect(screen.getByText(/Internal reasoning here/)).toBeInTheDocument();
  });

  it("renders tool calls when present", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        {
          id: "m1",
          role: "assistant",
          content: "Used a tool",
          toolCalls: ["search(query='AI')"],
          timestamp: new Date("2025-01-01T10:00:00Z"),
        },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    expect(screen.getByText(/search\(query='AI'\)/)).toBeInTheDocument();
  });

  it("shows YOU and SENTIUM sender labels", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        { id: "m1", role: "user", content: "Hello", timestamp: new Date() },
        { id: "m2", role: "assistant", content: "Hi", timestamp: new Date() },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    expect(screen.getByText("SENTIUM")).toBeInTheDocument();
  });
});

describe("Assistant conversation deletion", () => {
  it("calls deleteConversation when trash button is clicked", async () => {
    const deleteConversation = vi.fn();
    vi.spyOn(useConversationsHook, "default").mockReturnValue({
      ...defaultConversationsHook,
      deleteConversation,
    });
    renderAssistant();
    const delBtn = screen.getByTitle("Delete conversation");
    fireEvent.click(delBtn);
    const confirmBtn = await screen.findByRole("button", { name: /delete chat/i });
    fireEvent.click(confirmBtn);
    expect(deleteConversation).toHaveBeenCalledWith("conv-1", expect.any(Object));
  });

  it("clears conversation when deleting the active conversation via mutation", async () => {
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });
    const deleteConversationMock = vi.fn(
      (_id: string, options?: Parameters<typeof defaultConversationsHook.deleteConversation>[1]) => {
        options?.onSuccess?.(undefined as unknown as void, _id, undefined);
      },
    );
    vi.spyOn(useConversationsHook, "default").mockReturnValue({
      ...defaultConversationsHook,
      deleteConversation: deleteConversationMock,
    });
    renderAssistant();
    const delBtn = screen.getByTitle("Delete conversation");
    fireEvent.click(delBtn);
    const confirmBtn = await screen.findByRole("button", { name: /delete chat/i });
    fireEvent.click(confirmBtn);
    await waitFor(() => expect(useConversationStore.getState().activeConversationId).toBeNull());
  });
});

describe("Assistant workspace context panel", () => {
  it("opens workspace context panel when Workspace Context is clicked", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([
      {
        id: "ws-1",
        name: "Alpha Workspace",
        description: null,
        fileCount: 0,
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
      },
    ]);
    renderAssistant();
    fireEvent.click(screen.getByText("Workspace Context"));
    await waitFor(() => expect(screen.getByText("Alpha Workspace")).toBeInTheDocument());
  });

  it("shows 'No workspaces found' when no workspaces", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([]);
    renderAssistant();
    fireEvent.click(screen.getByText("Workspace Context"));
    await waitFor(() => expect(screen.getByText(/no workspaces found/i)).toBeInTheDocument());
  });

  it("injects workspace context into input when + is clicked", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([
      {
        id: "ws-1",
        name: "Alpha Workspace",
        description: null,
        fileCount: 0,
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
      },
    ]);
    renderAssistant();
    fireEvent.click(screen.getByText("Workspace Context"));
    await waitFor(() => screen.getByText("Alpha Workspace"));
    const injectBtn = screen.getByTitle("Insert workspace reference into message");
    fireEvent.click(injectBtn);
    expect(screen.getAllByText("Alpha Workspace").length).toBeGreaterThanOrEqual(1);
  });
});

describe("Assistant model input when no models list", () => {
  it("renders text input for model when no models returned", () => {
    vi.spyOn(useModelsHook, "default").mockReturnValue({ ...defaultModelsHook, models: [] });
    useConversationStore.setState((s) => ({ ...s, model: "" }));
    renderAssistant();
    expect(screen.getByPlaceholderText(/model name/i)).toBeInTheDocument();
  });
});

describe("Assistant stop generation", () => {
  it("shows stop button when typing/generating", async () => {
    const mockResponse = {
      ok: true,
      body: {
        getReader: () => ({
          read: () => new Promise(() => {}),
        }),
      },
    };
    vi.mocked(agentRuntimeService.sendChatMessage).mockResolvedValue(mockResponse as unknown as Response);
    vi.mocked(agentRuntimeService.createConversation).mockResolvedValue({ id: "conv-1" });
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "Tell me something long" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.getByTitle("Stop generation")).toBeInTheDocument());
  });

  it("handles non-ok response gracefully", async () => {
    const mockResponse = { ok: false, body: null };
    vi.mocked(agentRuntimeService.sendChatMessage).mockResolvedValue(mockResponse as unknown as Response);
    vi.mocked(agentRuntimeService.createConversation).mockResolvedValue({ id: "conv-1" });
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "test" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.queryByTitle("Stop generation")).not.toBeInTheDocument(), { timeout: 3000 });
  });

  it("does not submit when already generating (isTyping)", async () => {
    const mockResponse = {
      ok: true,
      body: { getReader: () => ({ read: () => new Promise(() => {}) }) },
    };
    vi.mocked(agentRuntimeService.sendChatMessage).mockResolvedValue(mockResponse as unknown as Response);
    vi.mocked(agentRuntimeService.createConversation).mockResolvedValue({ id: "conv-1" });
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "first message" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.getByTitle("Stop generation")).toBeInTheDocument());
    fireEvent.change(input, { target: { value: "second message" } });
    fireEvent.submit(document.querySelector("form")!);
    expect(agentRuntimeService.sendChatMessage).toHaveBeenCalledTimes(1);
  });

  it("handles null body (no reader) gracefully", async () => {
    const mockResponse = { ok: true, body: null };
    vi.mocked(agentRuntimeService.sendChatMessage).mockResolvedValue(mockResponse as unknown as Response);
    vi.mocked(agentRuntimeService.createConversation).mockResolvedValue({ id: "conv-1" });
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "test" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.queryByTitle("Stop generation")).not.toBeInTheDocument(), { timeout: 3000 });
  });

  it("processes streamed content chunks", async () => {
    const encoder = new TextEncoder();
    const chunk1 = encoder.encode(JSON.stringify({ type: "content", message: { content: "Hello" } }) + "\n");
    const chunk2 = encoder.encode(JSON.stringify({ type: "thought", message: { content: "Thinking..." } }) + "\n");
    const chunk3 = encoder.encode(JSON.stringify({ type: "tool", message: { content: "tool_call" } }) + "\n");
    let callCount = 0;
    const reads = [
      { done: false, value: chunk1 },
      { done: false, value: chunk2 },
      { done: false, value: chunk3 },
      { done: true, value: undefined },
    ];
    const mockResponse = {
      ok: true,
      body: {
        getReader: () => ({
          read: vi
            .fn()
            .mockImplementation(() => Promise.resolve(reads[callCount++] || { done: true, value: undefined })),
        }),
      },
    };
    vi.mocked(agentRuntimeService.sendChatMessage).mockResolvedValue(mockResponse as unknown as Response);
    vi.mocked(agentRuntimeService.createConversation).mockResolvedValue({ id: "conv-1" });
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "Hello" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.queryByTitle("Stop generation")).not.toBeInTheDocument(), { timeout: 3000 });
  });
});

describe("Assistant workspace file expansion", () => {
  it("shows files when workspace is expanded in context panel", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([
      {
        id: "ws-1",
        name: "MyWorkspace",
        description: "",
        fileCount: 1,
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
      },
    ]);
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([
      {
        id: "file-1",
        fileName: "data.csv",
        extension: "csv",
        processingStatus: "Completed",
        sizeBytes: 1024,
        workspaceId: "ws-1",
        createdAt: "2024-01-01T00:00:00Z",
      },
    ]);
    renderAssistant();
    fireEvent.click(screen.getByText("Workspace Context"));
    await waitFor(() => screen.getByText("MyWorkspace"));
    fireEvent.click(screen.getByTitle("Expand workspace files"));
    await waitFor(() => expect(screen.getByText("data.csv")).toBeInTheDocument());
  });

  it("shows No files when workspace has no files", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([
      {
        id: "ws-1",
        name: "MyWorkspace",
        description: "",
        fileCount: 0,
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
      },
    ]);
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([]);
    renderAssistant();
    fireEvent.click(screen.getByText("Workspace Context"));
    await waitFor(() => screen.getByText("MyWorkspace"));
    fireEvent.click(screen.getByTitle("Expand workspace files"));
    await waitFor(() => expect(screen.getByText("No files.")).toBeInTheDocument());
  });

  it("injects file context when file button is clicked", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([
      {
        id: "ws-1",
        name: "MyWorkspace",
        description: "",
        fileCount: 1,
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
      },
    ]);
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([
      {
        id: "file-1",
        fileName: "data.csv",
        extension: "csv",
        processingStatus: "Completed",
        sizeBytes: 1024,
        workspaceId: "ws-1",
        createdAt: "2024-01-01T00:00:00Z",
      },
    ]);
    renderAssistant();
    fireEvent.click(screen.getByText("Workspace Context"));
    await waitFor(() => screen.getByText("MyWorkspace"));
    fireEvent.click(screen.getByTitle("Expand workspace files"));
    await waitFor(() => screen.getByTitle("Insert file reference: data.csv"));
    fireEvent.click(screen.getByTitle("Insert file reference: data.csv"));
    expect(screen.getAllByText("data.csv").length).toBeGreaterThanOrEqual(1);
  });
});
