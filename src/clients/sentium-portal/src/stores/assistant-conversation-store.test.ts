import { describe, it, expect, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useConversationStore } from "./assistant-conversation-store";
import { DEFAULT_ASSISTANT_MODEL } from "../utils/constants";
import type { ConversationMessage } from "../types/assistant";

const makeMessage = (overrides: Partial<ConversationMessage> = {}): ConversationMessage => ({
  id: "msg-1",
  role: "user",
  content: "Hello",
  timestamp: new Date("2025-01-01T00:00:00Z"),
  ...overrides,
});

beforeEach(() => {
  act(() => {
    useConversationStore.setState({
      activeConversationId: null,
      messages: [],
      model: DEFAULT_ASSISTANT_MODEL,
      isStreaming: false,
      streamingConversationId: null,
    });
  });
});

describe("useConversationStore initial state", () => {
  it("has null activeConversationId", () => {
    const { result } = renderHook(() => useConversationStore());
    expect(result.current.activeConversationId).toBeNull();
  });

  it("starts with an empty messages array", () => {
    const { result } = renderHook(() => useConversationStore());
    expect(result.current.messages).toHaveLength(0);
  });

  it("defaults model to DEFAULT_ASSISTANT_MODEL", () => {
    const { result } = renderHook(() => useConversationStore());
    expect(result.current.model).toBe(DEFAULT_ASSISTANT_MODEL);
  });
});

describe("setActiveConversation()", () => {
  it("sets the active conversation id and messages", () => {
    const { result } = renderHook(() => useConversationStore());
    const msgs = [makeMessage()];

    act(() => result.current.setActiveConversation("conv-1", msgs, "llama3"));

    expect(result.current.activeConversationId).toBe("conv-1");
    expect(result.current.messages).toEqual(msgs);
    expect(result.current.model).toBe("llama3");
  });

  it("falls back to empty messages array when passed empty list", () => {
    const { result } = renderHook(() => useConversationStore());

    act(() => result.current.setActiveConversation("conv-2", [], "llama3"));

    expect(result.current.messages).toHaveLength(0);
  });

  it("sets id to null when null is passed", () => {
    const { result } = renderHook(() => useConversationStore());

    act(() => result.current.setActiveConversation(null, [], DEFAULT_ASSISTANT_MODEL));

    expect(result.current.activeConversationId).toBeNull();
  });
});

describe("appendMessage()", () => {
  it("adds a message to the end of the list", () => {
    const { result } = renderHook(() => useConversationStore());
    const msg = makeMessage({ id: "msg-1" });

    act(() => result.current.appendMessage(msg));

    expect(result.current.messages).toHaveLength(1);
    expect(result.current.messages[0]).toEqual(msg);
  });

  it("appends multiple messages in order", () => {
    const { result } = renderHook(() => useConversationStore());

    act(() => result.current.appendMessage(makeMessage({ id: "msg-1" })));
    act(() => result.current.appendMessage(makeMessage({ id: "msg-2", role: "assistant" })));

    expect(result.current.messages[0].id).toBe("msg-1");
    expect(result.current.messages[1].id).toBe("msg-2");
  });
});

describe("updateLastMessage()", () => {
  it("appends content to the matching message (default 'content' type)", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1", content: "Hi" })));

    act(() => result.current.updateLastMessage("msg-1", " there"));

    expect(result.current.messages[0].content).toBe("Hi there");
  });

  it("appends thought when type is 'thought'", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1", thought: "" })));

    act(() => result.current.updateLastMessage("msg-1", "thinking...", "thought"));

    expect(result.current.messages[0].thought).toBe("thinking...");
  });

  it("accumulates thought across multiple calls", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1", thought: "A" })));

    act(() => result.current.updateLastMessage("msg-1", "B", "thought"));

    expect(result.current.messages[0].thought).toBe("AB");
  });

  it("appends tool call entry when type is 'tool'", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1", toolCalls: [] })));

    act(() => result.current.updateLastMessage("msg-1", "search(query)", "tool"));

    expect(result.current.messages[0].toolCalls).toContain("search(query)");
  });

  it("does not modify other messages", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1", content: "A" })));
    act(() => result.current.appendMessage(makeMessage({ id: "msg-2", content: "B" })));

    act(() => result.current.updateLastMessage("msg-1", "X"));

    expect(result.current.messages[1].content).toBe("B");
  });

  it("is a no-op when id is not found", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1", content: "Hello" })));

    act(() => result.current.updateLastMessage("nonexistent", " world"));

    expect(result.current.messages[0].content).toBe("Hello");
  });
});

describe("setEnhancedPrompt()", () => {
  it("attaches the enhanced prompt to the matching message", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1", role: "assistant" })));

    act(() => result.current.setEnhancedPrompt("msg-1", "A sharper prompt"));

    expect(result.current.messages[0].enhancedPrompt).toBe("A sharper prompt");
  });

  it("is a no-op when id is not found", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => result.current.appendMessage(makeMessage({ id: "msg-1" })));

    act(() => result.current.setEnhancedPrompt("nope", "x"));

    expect(result.current.messages[0].enhancedPrompt).toBeUndefined();
  });
});

describe("setModel()", () => {
  it("updates the model string", () => {
    const { result } = renderHook(() => useConversationStore());

    act(() => result.current.setModel("mistral"));

    expect(result.current.model).toBe("mistral");
  });
});

describe("clearConversation()", () => {
  it("resets to initial state", () => {
    const { result } = renderHook(() => useConversationStore());
    act(() => {
      result.current.setActiveConversation("conv-1", [makeMessage()], "llama3");
      result.current.clearConversation();
    });

    expect(result.current.activeConversationId).toBeNull();
    expect(result.current.messages).toHaveLength(0);
    expect(result.current.model).toBe(DEFAULT_ASSISTANT_MODEL);
  });
});
