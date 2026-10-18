import { describe, it, expect, vi } from "vitest";
import { createRef } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import ChatInputBar from "./chat-input-bar";

const noop = () => {};

const renderBar = (props: Partial<React.ComponentProps<typeof ChatInputBar>> = {}) =>
  render(
    <ChatInputBar
      input=""
      isTyping={false}
      contextPills={[]}
      model="gemma"
      models={["gemma", "qwen"]}
      onInputChange={noop}
      onKeyDown={noop}
      onSubmit={(e) => e.preventDefault()}
      onStop={noop}
      onRemoveContextPill={noop}
      onSetModel={noop}
      textareaRef={createRef<HTMLTextAreaElement>()}
      {...props}
    />,
  );

describe("ChatInputBar", () => {
  it("calls onInputChange as the user types", () => {
    const onInputChange = vi.fn();
    renderBar({ onInputChange });
    fireEvent.change(screen.getByPlaceholderText("Ask Sentium Assistant..."), { target: { value: "hi" } });
    expect(onInputChange).toHaveBeenCalledWith("hi");
  });

  it("disables send when input is empty and there are no pills", () => {
    renderBar({ input: "" });
    expect(screen.getByRole("button")).toBeDisabled();
  });

  it("enables send when there is input", () => {
    renderBar({ input: "hello" });
    expect(screen.getByRole("button")).toBeEnabled();
  });

  it("shows a stop button while typing and calls onStop", () => {
    const onStop = vi.fn();
    renderBar({ isTyping: true, onStop });
    expect(screen.getByPlaceholderText("Generating...")).toBeDisabled();
    fireEvent.click(screen.getByTitle("Stop generation"));
    expect(onStop).toHaveBeenCalled();
  });

  it("renders context pills and removes them", () => {
    const onRemoveContextPill = vi.fn();
    renderBar({
      contextPills: [{ type: "file", id: "f1", label: "notes.md" }],
      onRemoveContextPill,
    });
    expect(screen.getByText("notes.md")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Remove" }));
    expect(onRemoveContextPill).toHaveBeenCalledWith("f1");
  });

  it("opens the model dropdown and selects a model", () => {
    const onSetModel = vi.fn();
    renderBar({ onSetModel });
    fireEvent.click(screen.getByText("gemma"));
    fireEvent.click(screen.getByRole("button", { name: /qwen/ }));
    expect(onSetModel).toHaveBeenCalledWith("qwen");
  });

  it("exposes an accessible name and keyboard hints", () => {
    renderBar();
    expect(screen.getByRole("textbox", { name: "Chat message" })).toBeInTheDocument();
    expect(screen.getByText(/Enter: Send/)).toBeInTheDocument();
    expect(screen.getByText(/Shift\+Enter: New line/)).toBeInTheDocument();
  });

  it("falls back to a free-text model input when there are no models", () => {
    const onSetModel = vi.fn();
    renderBar({ models: [], model: "", onSetModel });
    const input = screen.getByPlaceholderText("model name...");
    fireEvent.change(input, { target: { value: "llama3" } });
    expect(onSetModel).toHaveBeenCalledWith("llama3");
  });
});
