import { describe, it, expect, beforeEach, vi } from "vitest";
import { act } from "@testing-library/react";
import { useConversationStore } from "./assistant-conversation-store";
import { DEFAULT_ASSISTANT_MODEL } from "../utils/constants";
import * as agentRuntimeService from "../services/agentRuntime.service";
import { handleUnauthorized } from "../api/client";

vi.mock("../api/client", async (orig) => {
  const mod = await orig<typeof import("../api/client")>();
  return {
    ...mod,
    handleUnauthorized: vi.fn(() => {
      throw new Error("Session expired. Please log in again.");
    }),
  };
});

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

const hangingResponse = (signal?: AbortSignal | null): Response => {
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      signal?.addEventListener("abort", () => controller.error(new DOMException("Aborted", "AbortError")));
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

  it("stamps the streaming conversation onto a pending approval", async () => {
    const approval = JSON.stringify({ toolName: "delete", requestId: "req-1", arguments: {} });
    vi.spyOn(agentRuntimeService, "sendChatMessage").mockResolvedValue(
      sseResponse([JSON.stringify({ type: "approval_request", message: { content: approval } })]),
    );

    await act(async () => {
      await useConversationStore.getState().sendMessage({ conversationId: "c-9", model: "g", userContent: "Hi" });
    });

    const ai = useConversationStore.getState().messages[1];
    expect(ai.pendingApproval?.conversationId).toBe("c-9");
  });

  it("reports a timeout error when the stream stalls", async () => {
    vi.useFakeTimers();
    try {
      vi.spyOn(agentRuntimeService, "sendChatMessage").mockImplementation(async (_payload, signal) =>
        hangingResponse(signal),
      );

      const pending = useConversationStore.getState().sendMessage({
        conversationId: "c1",
        model: "g",
        userContent: "Hi",
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(120_000);
        await pending;
      });

      const ai = useConversationStore.getState().messages[1];
      expect(ai.error).toBe("Response timed out. Please try again.");
      expect(useConversationStore.getState().isStreaming).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it("stays silent when the user stops the stream", async () => {
    vi.spyOn(agentRuntimeService, "sendChatMessage").mockImplementation(async (_payload, signal) =>
      hangingResponse(signal),
    );

    const pending = useConversationStore.getState().sendMessage({
      conversationId: "c1",
      model: "g",
      userContent: "Hi",
    });

    await act(async () => {
      await Promise.resolve();
      useConversationStore.getState().stopStreaming();
      await pending;
    });

    const ai = useConversationStore.getState().messages[1];
    expect(ai.error).toBeUndefined();
    expect(useConversationStore.getState().isStreaming).toBe(false);
  });

  it("redirects via handleUnauthorized when the stream responds 401", async () => {
    vi.spyOn(agentRuntimeService, "sendChatMessage").mockResolvedValue({
      ok: false,
      status: 401,
    } as unknown as Response);

    await act(async () => {
      await useConversationStore.getState().sendMessage({ conversationId: "c1", model: "g", userContent: "Hi" });
    });

    expect(handleUnauthorized).toHaveBeenCalled();
    const ai = useConversationStore.getState().messages[1];
    expect(ai.error).toBe("Session expired. Please log in again.");
    expect(useConversationStore.getState().isStreaming).toBe(false);
  });

  it("aborts the previous stream when a new message is sent", async () => {
    const spy = vi
      .spyOn(agentRuntimeService, "sendChatMessage")
      .mockImplementationOnce(async (_payload, signal) => hangingResponse(signal))
      .mockImplementationOnce(async () =>
        sseResponse([
          JSON.stringify({ type: "content", message: { content: "second" } }),
          JSON.stringify({ type: "done" }),
        ]),
      );

    const first = useConversationStore.getState().sendMessage({ conversationId: "a", model: "g", userContent: "1" });
    await act(async () => {
      await Promise.resolve();
    });

    await act(async () => {
      await useConversationStore.getState().sendMessage({ conversationId: "b", model: "g", userContent: "2" });
      await first;
    });

    const firstSignal = spy.mock.calls[0][1];
    expect(firstSignal?.aborted).toBe(true);
    expect(useConversationStore.getState().isStreaming).toBe(false);
    expect(useConversationStore.getState().streamingConversationId).toBeNull();
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
      await useConversationStore.getState().respondToApproval({
        aiMsgId: "ai-1",
        requestId: "req-1",
        approved: true,
        conversationId: "c1",
      });
    });

    expect(agentRuntimeService.approveToolCall).toHaveBeenCalledWith("req-1", true, expect.anything());
    expect(useConversationStore.getState().messages[0].content).toBe("done it");
  });

  it("binds the resumed stream to the approval's conversation, not the active one", async () => {
    act(() =>
      useConversationStore.setState({
        activeConversationId: "c-viewing",
        messages: [{ id: "ai-1", role: "assistant", content: "", timestamp: new Date() }],
      }),
    );

    let observedStreamingId: string | null = null;
    vi.spyOn(agentRuntimeService, "approveToolCall").mockImplementation(async () => {
      observedStreamingId = useConversationStore.getState().streamingConversationId;
      return sseResponse([JSON.stringify({ type: "done" })]);
    });

    await act(async () => {
      await useConversationStore.getState().respondToApproval({
        aiMsgId: "ai-1",
        requestId: "req-1",
        approved: true,
        conversationId: "c-origin",
      });
    });

    expect(observedStreamingId).toBe("c-origin");
  });
});

describe("stopStreaming", () => {
  it("is a no-op when there is no active stream", () => {
    expect(() => useConversationStore.getState().stopStreaming()).not.toThrow();
  });
});
