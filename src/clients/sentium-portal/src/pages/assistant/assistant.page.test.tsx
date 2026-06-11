import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Assistant from "./assistant";
import type { ConversationMessage } from "../../types/assistant";

const navigate = vi.fn();
let routeParams: { conversationId?: string } = {};
vi.mock("react-router", async (orig) => ({
  ...(await orig<typeof import("react-router")>()),
  useNavigate: () => navigate,
  useParams: () => routeParams,
}));

const storeState: Record<string, unknown> = {};
vi.mock("../../stores/assistant-conversation-store", () => ({
  useConversationStore: () => storeState,
}));

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const fetchConversation = vi.fn((_id: string) => new Promise(() => {}));
vi.mock("../../services/agentRuntime.service", () => ({
  fetchConversation: (id: string) => fetchConversation(id),
  fetchWorkspaces: vi.fn().mockResolvedValue([]),
  fetchWorkspaceFiles: vi.fn().mockResolvedValue([]),
}));

const createConversation = vi.fn().mockResolvedValue({ id: "new-c" });
const deleteConversationMutate = vi.fn();
vi.mock("../../hooks/useConversations", () => ({
  default: () => ({
    conversations: [],
    createConversation,
    deleteConversation: deleteConversationMutate,
    isCreating: false,
  }),
}));

vi.mock("../../hooks/useModels", () => ({ default: () => ({ models: ["gemma"] }) }));
vi.mock("../../hooks/useProfile", () => ({ default: () => ({ profile: { firstName: "Alice" } }) }));

const sendMessage = vi.fn();
const respondToApproval = vi.fn();
const retryLastMessage = vi.fn();

const setStore = (overrides: Record<string, unknown> = {}) => {
  Object.assign(storeState, {
    activeConversationId: "c1",
    messages: [] as ConversationMessage[],
    model: "gemma",
    isStreaming: false,
    streamingConversationId: null,
    setActiveConversation: vi.fn(),
    setModel: vi.fn(),
    sendMessage,
    respondToApproval,
    stopStreaming: vi.fn(),
    retryLastMessage,
    ...overrides,
  });
};

const renderPage = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <Assistant />
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  navigate.mockReset();
  sendMessage.mockReset();
  respondToApproval.mockReset();
  retryLastMessage.mockReset();
  createConversation.mockClear();
  fetchConversation.mockClear();
  fetchConversation.mockImplementation(() => new Promise(() => {}));
  routeParams = {};
  for (const k of Object.keys(storeState)) delete storeState[k];
  setStore();
});

describe("Assistant page", () => {
  it("shows the welcome screen when there are no messages", () => {
    renderPage();
    expect(screen.getByText("Assistant")).toBeInTheDocument();
    expect(screen.getByText(/Alice/)).toBeInTheDocument();
  });

  it("puts a clicked suggestion into the input box", () => {
    renderPage();
    const chips = screen.getAllByRole("button").filter((b) => b.className.includes("suggestionChip"));
    if (chips.length > 0) {
      fireEvent.click(chips[0]);
      expect(
        (screen.getByPlaceholderText("Ask Sentium Assistant...") as HTMLTextAreaElement).value.length,
      ).toBeGreaterThan(0);
    }
  });

  it("renders message bubbles when the conversation has messages", () => {
    setStore({
      messages: [
        { id: "u1", role: "user", content: "hi there", timestamp: new Date() },
        { id: "a1", role: "assistant", content: "hello back", timestamp: new Date() },
      ],
    });
    renderPage();
    expect(screen.getByText("hi there")).toBeInTheDocument();
    expect(screen.getByText("hello back")).toBeInTheDocument();
  });

  it("shows a loading state when navigating to a not-yet-active conversation", () => {
    routeParams = { conversationId: "other" };
    setStore({ activeConversationId: "c1" });
    renderPage();
    expect(screen.getByText(/loading conversation/i)).toBeInTheDocument();
  });

  it("sends a typed message", async () => {
    renderPage();
    const textarea = screen.getByPlaceholderText("Ask Sentium Assistant...");
    fireEvent.change(textarea, { target: { value: "what is up" } });
    fireEvent.submit(textarea.closest("form")!);
    await waitFor(() =>
      expect(sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({ conversationId: "c1", model: "gemma", userContent: "what is up" }),
      ),
    );
  });

  it("creates a new conversation via Ctrl+K", async () => {
    renderPage();
    const textarea = screen.getByPlaceholderText("Ask Sentium Assistant...");
    fireEvent.keyDown(textarea, { key: "k", ctrlKey: true });
    await waitFor(() => expect(createConversation).toHaveBeenCalled());
  });

  it("toggles the conversation sidebar", () => {
    renderPage();
    const toggle = screen.getByTitle("Toggle conversations");
    expect(toggle).toBeInTheDocument();
    fireEvent.click(toggle);
  });

  it("routes a tool approval to the store", () => {
    setStore({
      messages: [
        {
          id: "a1",
          role: "assistant",
          content: "",
          timestamp: new Date(),
          pendingApproval: { toolName: "delete", requestId: "req-1", arguments: {} },
        },
      ],
    });
    renderPage();
    fireEvent.click(screen.getByText("Approve"));
    expect(respondToApproval).toHaveBeenCalledWith({
      aiMsgId: "a1",
      requestId: "req-1",
      approved: true,
      conversationId: "c1",
    });
  });

  it("routes a tool approval to the conversation it belongs to", () => {
    setStore({
      activeConversationId: "c-other",
      messages: [
        {
          id: "a1",
          role: "assistant",
          content: "",
          timestamp: new Date(),
          pendingApproval: { toolName: "delete", requestId: "req-1", arguments: {}, conversationId: "c-origin" },
        },
      ],
    });
    renderPage();
    fireEvent.click(screen.getByText("Approve"));
    expect(respondToApproval).toHaveBeenCalledWith({
      aiMsgId: "a1",
      requestId: "req-1",
      approved: true,
      conversationId: "c-origin",
    });
  });

  it("waits while the routed conversation is streaming, then refetches it when the stream ends", () => {
    routeParams = { conversationId: "c2" };
    setStore({ activeConversationId: "c1", isStreaming: true, streamingConversationId: "c2" });
    const { rerender } = renderPage();
    expect(fetchConversation).not.toHaveBeenCalled();
    expect(screen.getByText(/loading conversation/i)).toBeInTheDocument();

    setStore({ activeConversationId: "c1", isStreaming: false, streamingConversationId: null });
    rerender(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Assistant />
      </QueryClientProvider>,
    );
    expect(fetchConversation).toHaveBeenCalledWith("c2");
  });

  it("keeps the input and shows an error when conversation creation fails", async () => {
    createConversation.mockRejectedValueOnce(new Error("boom"));
    setStore({ activeConversationId: null });
    renderPage();
    const textarea = screen.getByPlaceholderText("Ask Sentium Assistant...");
    fireEvent.change(textarea, { target: { value: "hello" } });
    fireEvent.submit(textarea.closest("form")!);
    await waitFor(() => expect(screen.getByText(/failed to create a conversation/i)).toBeInTheDocument());
    expect(sendMessage).not.toHaveBeenCalled();
    expect((textarea as HTMLTextAreaElement).value).toBe("hello");
  });

  it("retries the last message from an error banner", () => {
    setStore({
      messages: [
        { id: "u1", role: "user", content: "hi", timestamp: new Date() },
        { id: "a1", role: "assistant", content: "", error: "failed", timestamp: new Date() },
      ],
    });
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(retryLastMessage).toHaveBeenCalled();
  });

  it("recalls the last user message with the ArrowUp key when input is empty", () => {
    setStore({
      messages: [{ id: "u1", role: "user", content: "previous question", timestamp: new Date() }],
    });
    renderPage();
    const textarea = screen.getByPlaceholderText("Ask Sentium Assistant...");
    fireEvent.keyDown(textarea, { key: "ArrowUp" });
    expect((textarea as HTMLTextAreaElement).value).toBe("previous question");
  });
});
