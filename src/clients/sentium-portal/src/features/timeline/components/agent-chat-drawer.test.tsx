import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import * as assistantApi from "@/features/assistant/api/assistant.service";
import { task } from "../test-fixtures";
import { timelineThreads, useTimelineChatStore } from "../state/timeline-chat-store";
import { AgentChatDrawer } from "./agent-chat-drawer";

vi.mock("@/features/agents/hooks/useAgentRoster", () => ({
  useAgentRoster: () => ({
    roster: [
      { name: "TimelinePlanner", description: "Plans", kind: "BuiltIn" },
      { name: "Budget Bot", description: "Mine", kind: "Custom", model: "qwen3" },
    ],
    isLoading: false,
    isError: false,
  }),
}));

vi.mock("@/features/models/hooks/useModels", () => ({
  useModels: () => ({ models: ["gemma3"], defaultModel: "gemma3" }),
}));

const sendMessage = vi.fn();

const renderDrawer = (context: Parameters<typeof AgentChatDrawer>[0]["context"]) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <AgentChatDrawer context={context} today="2026-11-08" onClose={vi.fn()} />
      </MemoryRouter>
    </QueryClientProvider>,
  );

describe("AgentChatDrawer", () => {
  beforeEach(() => {
    timelineThreads.clear();
    useTimelineChatStore.setState({
      activeConversationId: null,
      messages: [],
      isStreaming: false,
      streamingConversationId: null,
      sendMessage,
    });
    vi.spyOn(assistantApi, "createConversation").mockResolvedValue({ id: "conv-1" });
  });

  afterEach(() => sendMessage.mockReset());

  it("starts a conversation with the task's agent and sends the task as context", async () => {
    renderDrawer({ kind: "task", task: task({ assignedAgent: "Budget Bot" }) });

    expect(screen.getByText("Budget Bot")).toBeInTheDocument();
    fireEvent.change(screen.getByRole("textbox", { name: "Chat message" }), {
      target: { value: "When should I do this?" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Send message" }));

    await waitFor(() => expect(sendMessage).toHaveBeenCalled());
    expect(assistantApi.createConversation).toHaveBeenCalledWith({
      title: "Timeline · Book dentist",
      model: "qwen3",
      agentName: "Budget Bot",
    });
    expect(sendMessage).toHaveBeenCalledWith({
      conversationId: "conv-1",
      model: "qwen3",
      userContent: expect.stringMatching(/^\[Timeline task: Book dentist \| ID: task-1 .*\] When should I do this\?$/),
    });
    expect(timelineThreads.get("task:task-1")).toBe("conv-1");
    expect(screen.getByRole("link", { name: /Open in assistant/ })).toHaveAttribute("href", "/assistant/conv-1");
  });

  it("talks to the planner about a month, from a starter prompt", async () => {
    renderDrawer({ kind: "month", year: 2026, month: 11 });

    expect(screen.getByText("TimelinePlanner")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Review my workload/ }));
    fireEvent.click(screen.getByRole("button", { name: "Send message" }));

    await waitFor(() => expect(sendMessage).toHaveBeenCalled());
    expect(assistantApi.createConversation).toHaveBeenCalledWith(
      expect.objectContaining({ agentName: "TimelinePlanner", model: "gemma3" }),
    );
    expect(sendMessage.mock.calls[0][0].userContent).toMatch(
      /^\[Timeline month: November 2026 \(2026-11\) \| Today: 2026-11-08\] Review my workload/,
    );
  });

  it("continues an existing thread without repeating the context", async () => {
    timelineThreads.set("month:2026-11", "conv-9");
    useTimelineChatStore.setState({
      activeConversationId: "conv-9",
      messages: [{ id: "m1", role: "assistant", content: "Earlier answer", timestamp: new Date() }],
    });

    renderDrawer({ kind: "month", year: 2026, month: 11 });
    fireEvent.change(screen.getByRole("textbox", { name: "Chat message" }), { target: { value: "And December?" } });
    fireEvent.click(screen.getByRole("button", { name: "Send message" }));

    await waitFor(() =>
      expect(sendMessage).toHaveBeenCalledWith({
        conversationId: "conv-9",
        model: "gemma3",
        userContent: "And December?",
      }),
    );
    expect(assistantApi.createConversation).not.toHaveBeenCalled();
    expect(screen.getByText("Earlier answer")).toBeInTheDocument();
  });
});
