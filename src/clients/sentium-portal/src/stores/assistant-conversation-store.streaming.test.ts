import { describe, it, expect, beforeEach, vi } from "vitest";
import { act } from "@testing-library/react";
import { useConversationStore } from "./assistant-conversation-store";
import { DEFAULT_ASSISTANT_MODEL } from "../utils/constants";
import * as agentRuntimeService from "../services/agentRuntime.service";

const sseResponse = (lines: string[]): Response => {
  const encoder = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const line of lines) {
        controller.enqueue(encoder.encode(`data: ${line}\n`));
      }
      controller.close();
    },
  });
  return { ok: true, body: stream } as unknown as Response;
};

const reset = () =>
  act(() =>
    useConversationStore.setState({
      activeConversationId: null,
      messages: [],
      model: DEFAULT_ASSISTANT_MODEL,
      isStreaming: false,
      streamingConversationId: null,
    }),
  );

beforeEach(() => {
  reset();
  vi.restoreAllMocks();
});

describe("sendMessage streaming", () => {
  it("streams thought and content into the assistant message and settles", async () => {
    vi.spyOn(agentRuntimeService, "sendChatMessage").mockResolvedValue(
      sseResponse([
        JSON.stringify({ type: "thought", message: { content: "thinking" } }),
        JSON.stringify({ type: "content", message: { content: "Hello" } }),
        JSON.stringify({ type: "done" }),
      ]),
    );

    await act(async () => {
      await useConversationStore.getState().sendMessage({
        conversationId: "c1",
        model: "gemma",
        userContent: "Hi",
      });
    });

    const { messages, isStreaming } = useConversationStore.getState();
    expect(messages).toHaveLength(2);
    expect(messages[0].role).toBe("user");
    const ai = messages[1];
    expect(ai.role).toBe("assistant");
    expect(ai.content).toBe("Hello");
    expect(ai.thought).toBe("thinking");
    expect(isStreaming).toBe(false);
  });

  it("captures an enhanced prompt for the user message", async () => {
    vi.spyOn(agentRuntimeService, "sendChatMessage").mockResolvedValue(
      sseResponse([
        JSON.stringify({ type: "enhanced_prompt", message: { content: "sharper" } }),
        JSON.stringify({ type: "content", message: { content: "ok" } }),
        JSON.stringify({ type: "done" }),
      ]),
    );

    await act(async () => {
      await useConversationStore.getState().sendMessage({ conversationId: null, model: "g", userContent: "Hi" });
    });

    const userMsg = useConversationStore.getState().messages[0];
    expect(userMsg.enhancedPrompt).toBe("sharper");
  });

  it("sets a message error when the stream emits an error event", async () => {
    vi.spyOn(agentRuntimeService, "sendChatMessage").mockResolvedValue(
      sseResponse([JSON.stringify({ type: "error", message: "node failed" })]),
    );

    await act(async () => {
      await useConversationStore.getState().sendMessage({ conversationId: null, model: "g", userContent: "Hi" });
    });

    const ai = useConversationStore.getState().messages[1];
    expect(ai.error).toBe("Connection to AI node failed.");
    expect(useConversationStore.getState().isStreaming).toBe(false);
  });

  it("records a pending approval and stops streaming", async () => {
    const approval = JSON.stringify({ toolName: "delete", requestId: "req-1", arguments: {} });
    vi.spyOn(agentRuntimeService, "sendChatMessage").mockResolvedValue(
      sseResponse([JSON.stringify({ type: "approval_request", message: { content: approval } })]),
    );

    await act(async () => {
      await useConversationStore.getState().sendMessage({ conversationId: null, model: "g", userContent: "Hi" });
    });

    const ai = useConversationStore.getState().messages[1];
    expect(ai.pendingApproval).toEqual({ toolName: "delete", requestId: "req-1", arguments: {} });
  });
});

describe("respondToApproval", () => {
  it("resumes the stream after an approval decision", async () => {
    act(() =>
      useConversationStore.setState({
        messages: [{ id: "ai-1", role: "assistant", content: "", timestamp: new Date() }],
      }),
    );

    vi.spyOn(agentRuntimeService, "approveToolCall").mockResolvedValue(
      sseResponse([
        JSON.stringify({ type: "content", message: { content: "done it" } }),
        JSON.stringify({ type: "done" }),
      ]),
    );

    await act(async () => {
      await useConversationStore.getState().respondToApproval({ aiMsgId: "ai-1", requestId: "req-1", approved: true });
    });

    expect(agentRuntimeService.approveToolCall).toHaveBeenCalledWith("req-1", true, expect.anything());
    expect(useConversationStore.getState().messages[0].content).toBe("done it");
  });
});

describe("stopStreaming", () => {
  it("is a no-op when there is no active stream", () => {
    expect(() => useConversationStore.getState().stopStreaming()).not.toThrow();
  });
});
