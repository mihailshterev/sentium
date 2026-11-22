import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { act } from "@testing-library/react";
import { createConversationStore, useConversationStore } from "./assistant-conversation-store";
import { DEFAULT_ASSISTANT_MODEL } from "@/shared/utils/constants";
import * as assistantService from "@/features/assistant/api/assistant.service";
import { FakeEventSource } from "@/test/fake-event-source";

const RESUME_KEY = "sentium.assistant.activeTurn";

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

/** The stream the store opened for its latest turn, once the turn was accepted. */
const turnStream = async () => {
  await vi.waitFor(() => expect(FakeEventSource.instances.length).toBeGreaterThan(0));
  return FakeEventSource.last();
};

const send = (conversationId: string | null = "c1", userContent = "Hi") =>
  useConversationStore.getState().sendMessage({ conversationId, model: "gemma", userContent });

const settle = async (pending: Promise<void>) => {
  await act(async () => {
    await pending;
  });
};

beforeEach(() => {
  reset();
  sessionStorage.clear();
  FakeEventSource.install();
  vi.spyOn(assistantService, "startChatTurn").mockResolvedValue({ turnId: "t1" });
  vi.spyOn(assistantService, "cancelChatTurn").mockResolvedValue(undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("sendMessage streaming", () => {
  it("follows the accepted turn's stream into the assistant message and settles", async () => {
    const pending = send();
    const source = await turnStream();

    expect(source.url).toContain("/praxis/assistant/turns/t1/stream");
    act(() => {
      source.emit({ type: "thought", text: "thinking" }, "1");
      source.emit({ type: "message", text: "Hello" }, "2");
      source.emit({ type: "done", done: true }, "3");
    });
    await settle(pending);

    const { messages, isStreaming } = useConversationStore.getState();
    expect(messages).toHaveLength(2);
    expect(messages[0].role).toBe("user");
    expect(messages[1].content).toBe("Hello");
    expect(messages[1].steps).toMatchObject([{ kind: "thought", text: "thinking" }]);
    expect(messages[1].steps?.[0]).toHaveProperty("endedAt");
    expect(isStreaming).toBe(false);
    expect(source.closed).toBe(true);
  });

  it("moves narration written before a tool call onto the timeline, leaving the final answer", async () => {
    const pending = send("c1", "drill?");
    const source = await turnStream();

    act(() => {
      source.emit({ type: "message", text: "Let me look that up." });
      source.emit({ type: "tool_call", text: JSON.stringify({ callId: "c1", name: "find_assets" }) });
      source.emit({ type: "tool_result", text: JSON.stringify({ callId: "c1", status: "ok", result: "drill", durationMs: 12 }) });
      source.emit({ type: "message", text: "Your drill is in the garage." });
      source.emit({ type: "done", done: true });
    });
    await settle(pending);

    const ai = useConversationStore.getState().messages[1];
    expect(ai.content).toBe("Your drill is in the garage.");
    expect(ai.steps).toMatchObject([
      { kind: "text", text: "Let me look that up." },
      { kind: "tool", callId: "c1", status: "ok", durationMs: 12 },
    ]);
  });

  it("captures an enhanced prompt for the user message", async () => {
    const pending = send(null);
    const source = await turnStream();

    act(() => {
      source.emit({ type: "enhanced_prompt", text: "sharper" });
      source.emit({ type: "done", done: true });
    });
    await settle(pending);

    expect(useConversationStore.getState().messages[0].enhancedPrompt).toBe("sharper");
  });

  it("shows the server's error when the turn fails", async () => {
    const pending = send(null);
    const source = await turnStream();

    act(() => source.emit({ type: "error", message: "The response took too long and was stopped.", done: true }));
    await settle(pending);

    expect(useConversationStore.getState().messages[1].error).toBe("The response took too long and was stopped.");
    expect(useConversationStore.getState().isStreaming).toBe(false);
  });

  it("records a pending approval stamped with the streaming conversation", async () => {
    const pending = send("c-9");
    const source = await turnStream();

    act(() => {
      source.emit({ type: "approval_request", text: JSON.stringify({ toolName: "delete", requestId: "req-1", arguments: {} }) });
      source.emit({ type: "done", done: true });
    });
    await settle(pending);

    expect(useConversationStore.getState().messages[1].pendingApproval).toEqual({
      toolName: "delete",
      requestId: "req-1",
      arguments: {},
      conversationId: "c-9",
    });
  });

  it("keeps following the turn through a dropped connection, resuming after the last event", async () => {
    vi.useFakeTimers();
    try {
      const pending = send();
      await vi.advanceTimersByTimeAsync(0);
      const first = FakeEventSource.last();
      act(() => first.emit({ type: "message", text: "Hel" }, "4"));

      first.fail();
      await vi.advanceTimersByTimeAsync(1_000);

      const second = FakeEventSource.last();
      expect(second).not.toBe(first);
      expect(second.url).toContain("after=4");
      act(() => {
        second.emit({ type: "message", text: "lo" }, "5");
        second.emit({ type: "done", done: true }, "6");
      });
      await settle(pending);

      expect(useConversationStore.getState().messages[1].content).toBe("Hello");
    } finally {
      vi.useRealTimers();
    }
  });

  it("reports a session expiry when the turn cannot be started", async () => {
    vi.spyOn(assistantService, "startChatTurn").mockRejectedValue(new Error("Session expired. Please log in again."));

    await settle(send());

    expect(useConversationStore.getState().messages[1].error).toBe("Session expired. Please log in again.");
    expect(useConversationStore.getState().isStreaming).toBe(false);
  });

  it("stops the previous turn when a new message is sent", async () => {
    const first = send("a", "1");
    const firstSource = await turnStream();
    vi.spyOn(assistantService, "startChatTurn").mockResolvedValue({ turnId: "t2" });

    const second = send("b", "2");
    await vi.waitFor(() => expect(FakeEventSource.last()).not.toBe(firstSource));
    act(() => FakeEventSource.last().emit({ type: "done", done: true }));
    await settle(Promise.all([first, second]).then(() => undefined));

    expect(assistantService.cancelChatTurn).toHaveBeenCalledWith("t1");
    expect(firstSource.closed).toBe(true);
    expect(useConversationStore.getState().isStreaming).toBe(false);
    expect(useConversationStore.getState().streamingConversationId).toBeNull();
  });
});

describe("stopStreaming", () => {
  it("is a no-op when there is no active turn", () => {
    expect(() => useConversationStore.getState().stopStreaming()).not.toThrow();
  });

  it("cancels the turn and settles quietly on the server's cancelled frame", async () => {
    const pending = send();
    const source = await turnStream();
    act(() => source.emit({ type: "message", text: "partial" }));

    act(() => useConversationStore.getState().stopStreaming());
    expect(assistantService.cancelChatTurn).toHaveBeenCalledWith("t1");
    act(() => source.emit({ type: "cancelled", done: true }));
    await settle(pending);

    const ai = useConversationStore.getState().messages[1];
    expect(ai.content).toBe("partial");
    expect(ai.error).toBeUndefined();
    expect(useConversationStore.getState().isStreaming).toBe(false);
  });

  it("settles on its own when the cancelled frame never arrives", async () => {
    vi.useFakeTimers();
    try {
      const pending = send();
      await vi.advanceTimersByTimeAsync(0);

      useConversationStore.getState().stopStreaming();
      await vi.advanceTimersByTimeAsync(8_000);
      await settle(pending);

      expect(useConversationStore.getState().isStreaming).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });
});

describe("respondToApproval", () => {
  it("follows the resumed turn after an approval decision", async () => {
    act(() => useConversationStore.setState({ messages: [{ id: "ai-1", role: "assistant", content: "", timestamp: new Date() }] }));
    vi.spyOn(assistantService, "approveToolCall").mockResolvedValue({ turnId: "t-approve" });

    const pending = useConversationStore
      .getState()
      .respondToApproval({ aiMsgId: "ai-1", requestId: "req-1", approved: true, conversationId: "c1" });
    const source = await turnStream();
    act(() => {
      source.emit({ type: "message", text: "done it" });
      source.emit({ type: "done", done: true });
    });
    await settle(pending);

    expect(assistantService.approveToolCall).toHaveBeenCalledWith("req-1", true);
    expect(source.url).toContain("/turns/t-approve/stream");
    expect(useConversationStore.getState().messages[0].content).toBe("done it");
  });

  it("binds the resumed turn to the approval's conversation, not the active one", async () => {
    act(() =>
      useConversationStore.setState({
        activeConversationId: "c-viewing",
        messages: [{ id: "ai-1", role: "assistant", content: "", timestamp: new Date() }],
      }),
    );
    let observedStreamingId: string | null = null;
    vi.spyOn(assistantService, "approveToolCall").mockImplementation(async () => {
      observedStreamingId = useConversationStore.getState().streamingConversationId;
      return { turnId: "t-approve" };
    });

    const pending = useConversationStore
      .getState()
      .respondToApproval({ aiMsgId: "ai-1", requestId: "req-1", approved: true, conversationId: "c-origin" });
    const source = await turnStream();
    act(() => source.emit({ type: "done", done: true }));
    await settle(pending);

    expect(observedStreamingId).toBe("c-origin");
  });
});

describe("surviving a reload", () => {
  it("re-attaches to the stored turn when its conversation is shown again", async () => {
    sessionStorage.setItem(RESUME_KEY, JSON.stringify({ turnId: "t-old", conversationId: "c1", userContent: "Hi" }));
    const store = createConversationStore({ resumeKey: RESUME_KEY });

    act(() => store.getState().setActiveConversation("c1", [], "gemma"));
    const source = await turnStream();
    act(() => {
      source.emit({ type: "message", text: "Welcome back" }, "1");
      source.emit({ type: "done", done: true }, "2");
    });
    await vi.waitFor(() => expect(store.getState().isStreaming).toBe(false));

    expect(source.url).toContain("/turns/t-old/stream");
    expect(source.url).not.toContain("after=");
    expect(store.getState().messages.map((m) => [m.role, m.content])).toEqual([
      ["user", "Hi"],
      ["assistant", "Welcome back"],
    ]);
    expect(sessionStorage.getItem(RESUME_KEY)).toBeNull();
  });

  it("shows the saved conversation when the stored turn is long gone", async () => {
    sessionStorage.setItem(RESUME_KEY, JSON.stringify({ turnId: "t-old", conversationId: "c1", userContent: "Hi" }));
    vi.spyOn(assistantService, "fetchConversation").mockResolvedValue({
      id: "c1",
      model: "gemma",
      messages: [
        { id: "m1", role: "user", content: "Hi", timestamp: new Date().toISOString() },
        { id: "m2", role: "assistant", content: "Saved answer", timestamp: new Date().toISOString() },
      ],
    } as unknown as Awaited<ReturnType<typeof assistantService.fetchConversation>>);
    const store = createConversationStore({ resumeKey: RESUME_KEY });

    act(() => store.getState().setActiveConversation("c1", [], "gemma"));
    const source = await turnStream();
    act(() => source.emit({ type: "gone", done: true }));

    await vi.waitFor(() => expect(store.getState().messages.map((m) => m.content)).toEqual(["Hi", "Saved answer"]));
  });

  it("stores the turn of a persisted conversation while it runs", async () => {
    const pending = send("c1", "Remember me");
    const source = await turnStream();

    expect(JSON.parse(sessionStorage.getItem(RESUME_KEY) ?? "{}")).toMatchObject({
      turnId: "t1",
      conversationId: "c1",
      userContent: "Remember me",
    });
    act(() => source.emit({ type: "done", done: true }));
    await settle(pending);
    expect(sessionStorage.getItem(RESUME_KEY)).toBeNull();
  });
});

describe("sendMessage title frames", () => {
  it("records the generated title and keeps it out of the answer text", async () => {
    act(() => useConversationStore.setState({ conversationTitles: {} }));

    const pending = send("c1", "Plan a trip");
    const source = await turnStream();
    act(() => {
      source.emit({ type: "message", text: "Sure." });
      source.emit({ type: "title", text: "Trip planning" });
      source.emit({ type: "done", done: true });
    });
    await settle(pending);

    const { messages, conversationTitles } = useConversationStore.getState();
    expect(messages[1].content).toBe("Sure.");
    expect(conversationTitles).toEqual({ c1: "Trip planning" });
  });

  it("clears a recorded title", () => {
    act(() => useConversationStore.setState({ conversationTitles: { c1: "Trip planning", c2: "Other" } }));

    act(() => useConversationStore.getState().clearConversationTitle("c1"));

    expect(useConversationStore.getState().conversationTitles).toEqual({ c2: "Other" });
  });
});

/** Frames are applied in batches; wait for the batch holding the ones just emitted. */
const applied = () => act(() => new Promise((resolve) => setTimeout(resolve, 60)));

const contents = () => useConversationStore.getState().messages.map((m) => m.content);

describe("switching conversations while a turn streams", () => {
  const otherConversation = [{ id: "o1", role: "user" as const, content: "other", timestamp: new Date() }];

  it("keeps following the turn out of view and shows its live answer on return", async () => {
    act(() => useConversationStore.getState().setActiveConversation("c1", [], "gemma"));
    const pending = send("c1", "Hi");
    const source = await turnStream();
    act(() => source.emit({ type: "message", text: "Hel" }));
    await applied();

    act(() => useConversationStore.getState().setActiveConversation("c2", otherConversation, "gemma"));
    act(() => source.emit({ type: "message", text: "lo" }));
    await applied();
    expect(contents()).toEqual(["other"]);

    // The saved transcript does not have the running turn yet; the live copy wins.
    act(() => useConversationStore.getState().setActiveConversation("c1", [], "gemma"));
    expect(contents()).toEqual(["Hi", "Hello"]);

    act(() => source.emit({ type: "done", done: true }));
    await settle(pending);
    expect(contents()).toEqual(["Hi", "Hello"]);
  });

  it("shows the saved transcript when the turn finished out of view", async () => {
    act(() => useConversationStore.getState().setActiveConversation("c1", [], "gemma"));
    const pending = send("c1", "Hi");
    const source = await turnStream();

    act(() => useConversationStore.getState().setActiveConversation("c2", otherConversation, "gemma"));
    act(() => {
      source.emit({ type: "message", text: "Hello" });
      source.emit({ type: "done", done: true });
    });
    await settle(pending);

    const saved = [
      { id: "m1", role: "user" as const, content: "Hi", timestamp: new Date() },
      { id: "m2", role: "assistant" as const, content: "Hello", timestamp: new Date() },
    ];
    act(() => useConversationStore.getState().setActiveConversation("c1", saved, "gemma"));
    expect(useConversationStore.getState().messages).toBe(saved);
  });
});

describe("frame batching", () => {
  it("applies a burst of frames - a replay after a reload - in a single update", async () => {
    const pending = send();
    const source = await turnStream();
    const listener = vi.fn();
    const unsubscribe = useConversationStore.subscribe(listener);

    for (let i = 0; i < 500; i++) {
      source.emit({ type: "message", text: "x" }, String(i + 1));
    }
    expect(useConversationStore.getState().messages[1].content).toBe("");
    await applied();
    unsubscribe();

    expect(useConversationStore.getState().messages[1].content).toBe("x".repeat(500));
    expect(listener).toHaveBeenCalledTimes(1);

    act(() => source.emit({ type: "done", done: true }));
    await settle(pending);
  });
});
