import { ArrowUp, FileText, FolderOpen, Square, X } from "lucide-react";
import styles from "../assistant.module.css";
import { ModelSelector } from "@/shared/ui/model-selector";

type ContextPill = { type: "workspace" | "file"; id: string; label: string };

interface ChatInputBarProps {
  input: string;
  isTyping: boolean;
  contextPills: ContextPill[];
  model: string;
  models: string[];
  onInputChange: (value: string) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  onStop: () => void;
  onRemoveContextPill: (id: string) => void;
  onSetModel: (model: string) => void;
  textareaRef: React.RefObject<HTMLTextAreaElement | null>;
  placeholder?: string;
  hint?: string | null;
}

export function ChatInputBar({
  input,
  isTyping,
  contextPills,
  model,
  models,
  onInputChange,
  onKeyDown,
  onSubmit,
  onStop,
  onRemoveContextPill,
  onSetModel,
  textareaRef,
  placeholder = "Ask Sentium Assistant...",
  hint = "Enter: Send · Shift+Enter: New line · Ctrl+K: New chat",
}: ChatInputBarProps) {
  return (
    <div className={styles.composerWrap}>
      <form onSubmit={onSubmit} className={styles.composer}>
        {contextPills.length > 0 && (
          <div className={styles.contextPills}>
            {contextPills.map((pill) => (
              <span key={pill.id} className={styles.contextPill}>
                {pill.type === "workspace" ? (
                  <FolderOpen size={12} aria-hidden="true" />
                ) : (
                  <FileText size={12} aria-hidden="true" />
                )}
                <span>{pill.label}</span>
                <button type="button" onClick={() => onRemoveContextPill(pill.id)} aria-label="Remove">
                  <X size={12} />
                </button>
              </span>
            ))}
          </div>
        )}

        <textarea
          ref={textareaRef}
          value={input}
          onChange={(e) => onInputChange(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder={isTyping ? "Generating..." : placeholder}
          className={styles.textarea}
          aria-label="Chat message"
          autoComplete="off"
          disabled={isTyping}
          rows={1}
        />

        <div className={styles.composerBar}>
          <ModelSelector
            variant="chip"
            models={models}
            value={model}
            onChange={onSetModel}
            placeholder="model name..."
          />

          {isTyping ? (
            <button
              type="button"
              onClick={onStop}
              className={`${styles.sendButton} ${styles.stopButton}`}
              title="Stop generation"
            >
              <Square size={12} fill="currentColor" />
            </button>
          ) : (
            <button
              type="submit"
              disabled={!input.trim() && contextPills.length === 0}
              className={styles.sendButton}
              aria-label="Send message"
            >
              <ArrowUp size={17} />
            </button>
          )}
        </div>
      </form>

      {hint && <p className={styles.composerHint}>{hint}</p>}
    </div>
  );
}
