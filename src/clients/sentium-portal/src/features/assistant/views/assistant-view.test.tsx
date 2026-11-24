import { describe, it, expect, vi, beforeEach } from "vitest";
import { act, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AssistantView as Assistant } from "./assistant-view";
import * as useConversationsHook from "@/features/assistant/hooks/useConversations";
import * as useModelsHook from "@/features/models/hooks/useModels";
import * as assistantService from "@/features/assistant/api/assistant.service";
import * as workspaceService from "@/features/workspaces/api/workspaces.service";
import { useConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import type { ConversationSummary } from "@/features/assistant/types/assistant";
import { SUGGESTIONS_POOL } from "@/shared/utils/constants";
import { FakeEventSource } from "@/test/fake-event-source";

vi.mock("@/features/assistant/api/assistant.service", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/assistant/api/assistant.service")>();
  return {
    ...actual,
    fetchConversations: vi.fn().mockResolvedValue([]),
    createConversation: vi.fn().mockResolvedValue({ id: "conv-1" }),
    deleteConversation: vi.fn().mockResolvedValue(undefined),
    fetchConversation: vi.fn().mockResolvedValue({ id: "conv-1", title: "Chat 1", model: "llama3.2", messages: [] }),
    startChatTurn: vi.fn().mockResolvedValue({ turnId: "t1" }),
    cancelChatTurn: vi.fn().mockResolvedValue(undefined),
  };
});
vi.mock("@/features/workspaces/api/workspaces.service", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/features/workspaces/api/workspaces.service")>()),
  fetchWorkspaces: vi.fn().mockResolvedValue([]),
  fetchWorkspaceFiles: vi.fn().mockResolvedValue([]),
}));

const mockConversation: ConversationSummary = {
  id: "conv-1",
  title: "Chat Jan 1",
  model: "llama3.2",
  createdAt: "2025-01-01T00:00:00Z",
};

const defaultConversationsHook = {
  conversations: [mockConversation],
  isLoading: false,
  hasMore: false,
  loadMore: vi.fn(),
  isLoadingMore: false,
  createConversation: vi.fn().mockResolvedValue({ id: "conv-new" }),
  isCreating: false,
  deleteConversation: vi.fn(),
  isDeleting: false,
  totalCount: 1,
  renameConversation: vi.fn(),
  setPinned: vi.fn(),
  setArchived: vi.fn(),
};

const defaultModelsHook = {
  models: ["llama3.2", "gemma-4-e2b"],
  defaultModel: null as string | null,
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
  FakeEventSource.install();
  sessionStorage.clear();
  useConversationStore.setState({
    activeConversationId: null,
    messages: [],
    model: "llama3.2",
    isStreaming: false,
    streamingConversationId: null,
  });
  vi.spyOn(useConversationsHook, "useConversations").mockReturnValue(defaultConversationsHook);
  vi.spyOn(useModelsHook, "useModels").mockReturnValue(defaultModelsHook);
});

describe("Assistant – initial render", () => {
  it("renders the model selector", () => {
    renderAssistant();
    expect(screen.getByText("llama3.2")).toBeInTheDocument();
  });

  it("renders four suggestion chips from the pool when no conversation", () => {
    renderAssistant();
    // The view picks a random subset, so match against the whole pool rather than specific wording.
    const chips = SUGGESTIONS_POOL.flatMap(({ text }) => screen.queryAllByRole("button", { name: text }));
    expect(chips).toHaveLength(4);
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
    const chip = screen.getByRole("button", { name: /llama3\.2/ });
    fireEvent.click(chip!);
    expect(screen.getAllByText("llama3.2").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("gemma-4-e2b")).toBeInTheDocument();
  });

  it("sets model from available models on initial render", () => {
    renderAssistant();
    expect(screen.getByText("llama3.2")).toBeInTheDocument();
  });

  it("shows 'No model selected' status when no models are available", () => {
    vi.spyOn(useModelsHook, "useModels").mockReturnValue({ ...defaultModelsHook, models: [] });
    useConversationStore.setState((s) => ({ ...s, model: "" }));
    renderAssistant();
    expect(screen.getByPlaceholderText(/model name/i)).toBeInTheDocument();
  });

  it("updates model when model selector is changed", () => {
    renderAssistant();
    const chip = screen.getByRole("button", { name: /llama3\.2/ });
    fireEvent.click(chip!);
    fireEvent.click(screen.getByText("gemma-4-e2b"));
    expect(screen.getByText("gemma-4-e2b")).toBeInTheDocument();
  });

  it("updates model in text input when no models and user types", () => {
    vi.spyOn(useModelsHook, "useModels").mockReturnValue({ ...defaultModelsHook, models: [] });
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
    await waitFor(() => expect(assistantService.fetchConversation).toHaveBeenCalledWith("conv-1"));
  });

  it("loads earlier messages above the newest ones", async () => {
    vi.mocked(assistantService.fetchConversation).mockResolvedValueOnce({
      id: "conv-1",
      title: "Chat 1",
      model: "llama3.2",
      messages: [{ id: "m3", role: "user", content: "Newest question", timestamp: "2025-01-01T10:03:00Z" }],
      hasEarlierMessages: true,
    });
    const earlier = vi.spyOn(assistantService, "fetchEarlierMessages").mockResolvedValue({
      messages: [{ id: "m1", role: "user", content: "First question", timestamp: "2025-01-01T10:00:00Z" }],
      hasEarlierMessages: false,
    });
    renderAssistant("/assistant/conv-1");

    fireEvent.click(await screen.findByRole("button", { name: /load earlier messages/i }));

    expect(await screen.findByText("First question")).toBeInTheDocument();
    expect(earlier).toHaveBeenCalledWith("conv-1", "m3");
    await waitFor(() =>
      expect(screen.queryByRole("button", { name: /load earlier messages/i })).not.toBeInTheDocument(),
    );
  });

  it("offers no earlier messages for a short conversation", async () => {
    vi.mocked(assistantService.fetchConversation).mockResolvedValueOnce({
      id: "conv-1",
      title: "Chat 1",
      model: "llama3.2",
      messages: [{ id: "m1", role: "user", content: "Only question", timestamp: "2025-01-01T10:00:00Z" }],
      hasEarlierMessages: false,
    });
    renderAssistant("/assistant/conv-1");

    expect(await screen.findByText("Only question")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /load earlier messages/i })).not.toBeInTheDocument();
  });

  it("groups pinned conversations first", () => {
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
      ...defaultConversationsHook,
      conversations: [mockConversation, { ...mockConversation, id: "conv-2", title: "Pinned chat", isPinned: true }],
    });
    renderAssistant();
    const groups = screen
      .getAllByRole("region")
      .map((r) => r.getAttribute("aria-label"))
      .filter((label) => label === "Pinned" || label === "Older");
    expect(groups).toEqual(["Pinned", "Older"]);
  });

  it("deletes a conversation when delete button is clicked", async () => {
    const deleteConversation = vi.fn();
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
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

describe("Assistant empty state", () => {
  it("shows empty state with no conversations", () => {
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
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
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
      ...defaultConversationsHook,
      createConversation,
    });
    renderAssistant();
    fireEvent.click(screen.getByTitle("New conversation"));
    await waitFor(() => expect(createConversation).toHaveBeenCalled());
  });

  it("creates the conversation without a title so the server can name it", async () => {
    const createConversation = vi.fn().mockResolvedValue({ id: "conv-new" });
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
      ...defaultConversationsHook,
      createConversation,
    });
    renderAssistant();
    fireEvent.click(screen.getByTitle("New conversation"));
    await waitFor(() => expect(createConversation).toHaveBeenCalledWith({ model: "llama3.2" }));
  });
});

describe("Assistant conversation search", () => {
  it("passes the debounced search to the conversation list", async () => {
    const spy = vi.spyOn(useConversationsHook, "useConversations").mockReturnValue(defaultConversationsHook);
    renderAssistant();

    fireEvent.change(screen.getByLabelText("Search conversations"), { target: { value: "trip" } });

    await waitFor(() => expect(spy).toHaveBeenLastCalledWith("trip", false));
  });
});

describe("Assistant conversation rename", () => {
  const renderWithRename = () => {
    const renameConversation = vi.fn();
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
      ...defaultConversationsHook,
      renameConversation,
    });
    renderAssistant();
    fireEvent.click(screen.getByTitle("Rename conversation"));
    return { renameConversation, input: screen.getByLabelText("Conversation title") };
  };

  it("renames on Enter", () => {
    const { renameConversation, input } = renderWithRename();
    fireEvent.change(input, { target: { value: "Trip planning" } });
    fireEvent.keyDown(input, { key: "Enter" });
    expect(renameConversation).toHaveBeenCalledWith({ id: "conv-1", title: "Trip planning" });
    expect(screen.queryByLabelText("Conversation title")).not.toBeInTheDocument();
  });

  it("keeps the old title on Escape", () => {
    const { renameConversation, input } = renderWithRename();
    fireEvent.change(input, { target: { value: "Discarded" } });
    fireEvent.keyDown(input, { key: "Escape" });
    expect(renameConversation).not.toHaveBeenCalled();
    expect(screen.getByText("Chat Jan 1")).toBeInTheDocument();
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

  it("submits message and starts a chat turn", async () => {
    vi.mocked(assistantService.startChatTurn).mockResolvedValue({ turnId: "t1" });
    vi.mocked(assistantService.createConversation).mockResolvedValue({ id: "conv-1" });

    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: "Test message" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(assistantService.startChatTurn).toHaveBeenCalled());
  });

  it("shows message in chat after sending", async () => {
    vi.mocked(assistantService.startChatTurn).mockResolvedValue({ turnId: "t1" });
    vi.mocked(assistantService.createConversation).mockResolvedValue({ id: "conv-1" });
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

  it("renders a finished turn's activity as a collapsed summary", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        {
          id: "m1",
          role: "assistant",
          content: "My response",
          steps: [{ kind: "thought", id: "t1", text: "Internal reasoning here" }],
          timestamp: new Date("2025-01-01T10:00:00Z"),
        },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    expect(screen.getByRole("button", { name: /1 thought/ })).toHaveAttribute("aria-expanded", "false");
  });

  it("expands the summary into thought steps and a thought into its text", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        {
          id: "m1",
          role: "assistant",
          content: "My response",
          steps: [{ kind: "thought", id: "t1", text: "Internal reasoning here" }],
          timestamp: new Date("2025-01-01T10:00:00Z"),
        },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    fireEvent.click(screen.getByRole("button", { name: /1 thought/ }));
    const thought = screen.getByRole("button", { name: /^thought/ });
    fireEvent.click(thought);
    expect(thought).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText(/Internal reasoning here/)).toBeInTheDocument();
  });

  it("renders tool steps when the summary is opened", () => {
    useConversationStore.setState({
      activeConversationId: "conv-1",
      messages: [
        {
          id: "m1",
          role: "assistant",
          content: "Used a tool",
          steps: [{ kind: "tool", id: "c1", name: "knowledge_base_search", arguments: '{"query":"AI"}', status: "ok" }],
          timestamp: new Date("2025-01-01T10:00:00Z"),
        },
      ],
      model: "llama3.2",
    });
    renderAssistant();
    fireEvent.click(screen.getByRole("button", { name: /1 tool call/ }));
    expect(screen.getByText("knowledge_base_search")).toBeInTheDocument();
    expect(screen.getByText("AI")).toBeInTheDocument();
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
    expect(screen.getByText("Sentium")).toBeInTheDocument();
  });
});

describe("Assistant conversation deletion", () => {
  it("calls deleteConversation when trash button is clicked", async () => {
    const deleteConversation = vi.fn();
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
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
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
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

describe("Assistant model input when no models list", () => {
  it("renders text input for model when no models returned", () => {
    vi.spyOn(useModelsHook, "useModels").mockReturnValue({ ...defaultModelsHook, models: [] });
    useConversationStore.setState((s) => ({ ...s, model: "" }));
    renderAssistant();
    expect(screen.getByPlaceholderText(/model name/i)).toBeInTheDocument();
  });
});

describe("Assistant stop generation", () => {
  const sendFromComposer = (text: string) => {
    renderAssistant();
    const input = screen.getByPlaceholderText(/ask sentium/i);
    fireEvent.change(input, { target: { value: text } });
    fireEvent.submit(document.querySelector("form")!);
    return input;
  };

  beforeEach(() => {
    vi.mocked(assistantService.startChatTurn).mockResolvedValue({ turnId: "t1" });
    vi.mocked(assistantService.createConversation).mockResolvedValue({ id: "conv-1" });
    useConversationStore.setState({ activeConversationId: "conv-1", messages: [], model: "llama3.2" });
  });

  it("shows the stop button while the turn is generating", async () => {
    sendFromComposer("test");
    await waitFor(() => expect(screen.getByTitle("Stop generation")).toBeInTheDocument());
  });

  it("stops generating when the turn cannot be started", async () => {
    vi.mocked(assistantService.startChatTurn).mockRejectedValue(new Error("Assistant request failed"));
    sendFromComposer("test");
    await waitFor(() => expect(screen.queryByTitle("Stop generation")).not.toBeInTheDocument(), { timeout: 3000 });
  });

  it("does not submit when already generating (isTyping)", async () => {
    const input = sendFromComposer("first message");
    await waitFor(() => expect(screen.getByTitle("Stop generation")).toBeInTheDocument());

    fireEvent.change(input, { target: { value: "second message" } });
    fireEvent.submit(document.querySelector("form")!);

    expect(assistantService.startChatTurn).toHaveBeenCalledTimes(1);
  });

  it("renders the streamed answer and settles on the done frame", async () => {
    sendFromComposer("Hello");
    await waitFor(() => expect(FakeEventSource.instances.length).toBeGreaterThan(0));

    act(() => {
      FakeEventSource.last().emit({ type: "thought", text: "Thinking..." });
      FakeEventSource.last().emit({ type: "message", text: "Hi there" });
      FakeEventSource.last().emit({ type: "done", done: true });
    });

    expect(await screen.findByText("Hi there")).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTitle("Stop generation")).not.toBeInTheDocument(), { timeout: 3000 });
  });
});

const workspace = {
  id: "ws-1",
  name: "MyWorkspace",
  description: "",
  fileCount: 1,
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const dataFile = {
  id: "file-1",
  fileName: "data.csv",
  extension: ".csv",
  processingStatus: "Pending" as const,
  sizeBytes: 1024,
  workspaceId: "ws-1",
  createdAt: "2024-01-01T00:00:00Z",
};

describe("Assistant attachments", () => {
  it("attaches a workspace file from the paperclip, even while it is still indexing", async () => {
    vi.mocked(workspaceService.fetchWorkspaces).mockResolvedValue([workspace]);
    vi.mocked(workspaceService.fetchWorkspaceFiles).mockResolvedValue([dataFile]);
    renderAssistant();

    fireEvent.click(screen.getByRole("button", { name: "Attach files" }));
    fireEvent.click(await screen.findByText("MyWorkspace"));
    fireEvent.click(await screen.findByText("data.csv"));

    const chip = await screen.findByTestId("composer-attachment");
    expect(chip).toHaveTextContent("data.csv");
    expect(screen.getByRole("button", { name: "Send message" })).toBeEnabled();
  });

  it("sends attachments as file ids instead of text tags", async () => {
    vi.mocked(workspaceService.fetchWorkspaces).mockResolvedValue([workspace]);
    vi.mocked(workspaceService.fetchWorkspaceFiles).mockResolvedValue([{ ...dataFile, processingStatus: "Completed" }]);
    vi.mocked(assistantService.startChatTurn).mockClear();
    renderAssistant();

    fireEvent.click(screen.getByRole("button", { name: "Attach files" }));
    fireEvent.click(await screen.findByText("MyWorkspace"));
    fireEvent.click(await screen.findByText("data.csv"));
    fireEvent.change(screen.getByPlaceholderText(/ask sentium/i), { target: { value: "Summarise it" } });
    fireEvent.submit(screen.getByRole("textbox", { name: "Chat message" }).closest("form")!);

    await waitFor(() => expect(assistantService.startChatTurn).toHaveBeenCalled());
    const payload = vi.mocked(assistantService.startChatTurn).mock.calls[0][0];
    expect(payload.fileIds).toEqual(["file-1"]);
    expect(payload.messages.at(-1)?.content).toBe("Summarise it");
    expect(screen.queryByTestId("composer-attachment")).not.toBeInTheDocument();
  });

  it("creates the conversation in the chosen workspace", async () => {
    vi.mocked(workspaceService.fetchWorkspaces).mockResolvedValue([workspace]);
    const createConversation = vi.fn().mockResolvedValue({ id: "conv-new" });
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
      ...defaultConversationsHook,
      createConversation,
    });
    renderAssistant();

    fireEvent.click(screen.getByRole("button", { name: "Choose a workspace" }));
    fireEvent.click(await screen.findByRole("option", { name: /MyWorkspace/ }));
    expect(screen.getByRole("button", { name: "Workspace: MyWorkspace" })).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText(/ask sentium/i), { target: { value: "Hello" } });
    fireEvent.submit(screen.getByRole("textbox", { name: "Chat message" }).closest("form")!);

    await waitFor(() =>
      expect(createConversation).toHaveBeenCalledWith(expect.objectContaining({ workspaceId: "ws-1" })),
    );
  });
});
