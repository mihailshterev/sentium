import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import MessageBubble from "./message-bubble";
import type { ConversationMessage } from "../../../types/assistant";

const base = (overrides: Partial<ConversationMessage> = {}): ConversationMessage => ({
  id: "m1",
  role: "assistant",
  content: "Hello there",
  timestamp: new Date(),
  ...overrides,
});

const noop = () => {};

const renderBubble = (msg: ConversationMessage, props: Partial<React.ComponentProps<typeof MessageBubble>> = {}) =>
  render(
    <MessageBubble
      msg={msg}
      isTyping={false}
      expandedThoughts={new Set<string>()}
      statusIndex={0}
      statusVisible
      copiedMessageId={null}
      onToggleThought={noop}
      onCopyMessage={noop}
      onApproval={noop}
      {...props}
    />,
  );

describe("MessageBubble", () => {
  it("renders user message content", () => {
    renderBubble(base({ role: "user", content: "my question" }));
    expect(screen.getByText("my question")).toBeInTheDocument();
  });

  it("renders assistant content with the SENTIUM header and copy button", () => {
    const onCopyMessage = vi.fn();
    renderBubble(base({ content: "an answer" }), { onCopyMessage });
    expect(screen.getByText("SENTIUM")).toBeInTheDocument();
    expect(screen.getByText("an answer")).toBeInTheDocument();
    fireEvent.click(screen.getByTitle("Copy to clipboard"));
    expect(onCopyMessage).toHaveBeenCalledWith("an answer", "m1");
  });

  it("shows the thought timeline and toggles it", () => {
    const onToggleThought = vi.fn();
    renderBubble(base({ thought: "reasoning" }), {
      onToggleThought,
      expandedThoughts: new Set(["m1"]),
    });
    expect(screen.getByText("Thinking")).toBeInTheDocument();
    expect(screen.getByText("reasoning")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Thinking"));
    expect(onToggleThought).toHaveBeenCalledWith("m1");
  });

  it("renders tool calls", () => {
    renderBubble(base({ content: "", toolCalls: ["search(q)"] }));
    expect(screen.getByText("search(q)")).toBeInTheDocument();
  });

  it("renders a pending approval and routes approve/deny", () => {
    const onApproval = vi.fn();
    renderBubble(
      base({
        content: "",
        pendingApproval: { toolName: "delete_file", requestId: "req-1", arguments: { path: "/x" } },
      }),
      { onApproval },
    );
    expect(screen.getByText("delete_file")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Approve"));
    expect(onApproval).toHaveBeenCalledWith("m1", "req-1", true);
    fireEvent.click(screen.getByText("Deny"));
    expect(onApproval).toHaveBeenCalledWith("m1", "req-1", false);
  });

  it("shows the status cycler while typing an empty assistant message", () => {
    renderBubble(base({ content: "" }), { isTyping: true, statusIndex: 0 });
    expect(screen.getByText("Synthesizing latent variables...")).toBeInTheDocument();
  });

  it("renders an error banner with a retry button", () => {
    const onRetry = vi.fn();
    renderBubble(base({ content: "", error: "Connection failed" }), { onRetry });
    expect(screen.getByText("Connection failed")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(onRetry).toHaveBeenCalled();
  });

  it("expands an enhanced prompt on a user message", () => {
    renderBubble(base({ role: "user", content: "q", enhancedPrompt: "sharper q" }));
    expect(screen.queryByText("sharper q")).not.toBeInTheDocument();
    fireEvent.click(screen.getByText("Enhanced prompt"));
    expect(screen.getByText("sharper q")).toBeInTheDocument();
  });
});
