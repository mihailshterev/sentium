import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MessageBubble } from "./message-bubble";
import type { ConversationMessage } from "@/features/assistant/types/assistant";

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
      statusIndex={0}
      statusVisible
      copiedMessageId={null}
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
    expect(screen.getByText("Sentium")).toBeInTheDocument();
    expect(screen.getByText("an answer")).toBeInTheDocument();
    fireEvent.click(screen.getByTitle("Copy to clipboard"));
    expect(onCopyMessage).toHaveBeenCalledWith("an answer", "m1");
  });

  it("folds a finished turn's activity into a summary that expands into the ordered timeline", () => {
    renderBubble(
      base({
        steps: [
          { kind: "thought", id: "t1", text: "reasoning" },
          { kind: "tool", id: "c1", callId: "c1", name: "knowledge_base_search", status: "ok", durationMs: 420 },
          { kind: "thought", id: "t2", text: "second pass" },
        ],
      }),
    );

    const summary = screen.getByRole("button", { name: /2 thoughts · 1 tool call/ });
    expect(summary).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("knowledge_base_search")).not.toBeInTheDocument();

    fireEvent.click(summary);

    const rows = screen.getAllByRole("listitem");
    expect(rows.map((r) => r.getAttribute("data-kind"))).toEqual(["thought", "tool", "thought"]);
    expect(screen.getByText("knowledge_base_search")).toBeInTheDocument();
    expect(screen.getByText("420ms")).toBeInTheDocument();
  });

  it("keeps the timeline open while the turn streams", () => {
    renderBubble(base({ content: "", steps: [{ kind: "thought", id: "t1", text: "working it out", startedAt: 1 }] }), {
      isTyping: true,
      live: true,
    });
    expect(screen.getByText("thinking")).toBeInTheDocument();
    expect(screen.getByText("working it out")).toBeInTheDocument();
    expect(screen.queryByText("Synthesizing latent variables...")).not.toBeInTheDocument();
  });

  it("renders an approval inline on its awaiting tool step", () => {
    const onApproval = vi.fn();
    renderBubble(
      base({
        content: "",
        steps: [
          {
            kind: "tool",
            id: "call-7",
            callId: "call-7",
            name: "execute_code_sandbox",
            arguments: '{"input":"print(1)"}',
            status: "awaiting_approval",
          },
        ],
        pendingApproval: {
          toolName: "execute_code_sandbox",
          requestId: "req-7",
          arguments: { input: "print(1)" },
          callId: "call-7",
          conversationId: "c1",
        },
      }),
      { onApproval },
    );

    const group = screen.getByRole("group", { name: "Tool approval" });
    expect(group).toHaveTextContent("print(1)");
    fireEvent.click(screen.getByRole("button", { name: "Approve" }));
    expect(onApproval).toHaveBeenCalledWith("m1", "req-7", true, "c1");
    expect(screen.getAllByRole("group", { name: "Tool approval" })).toHaveLength(1);
  });

  it("renders a pending approval and routes approve/deny", () => {
    const onApproval = vi.fn();
    renderBubble(
      base({
        content: "",
        pendingApproval: {
          toolName: "delete_file",
          requestId: "req-1",
          arguments: { path: "/x" },
          conversationId: "c1",
        },
      }),
      { onApproval },
    );
    expect(screen.getByText("delete_file")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Approve"));
    expect(onApproval).toHaveBeenCalledWith("m1", "req-1", true, "c1");
    fireEvent.click(screen.getByText("Deny"));
    expect(onApproval).toHaveBeenCalledWith("m1", "req-1", false, "c1");
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
