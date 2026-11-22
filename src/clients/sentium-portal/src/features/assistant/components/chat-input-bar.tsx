import { useState } from "react";
import { ArrowUp, Square, Upload } from "lucide-react";
import styles from "../assistant.module.css";
import { ModelSelector } from "@/shared/ui/model-selector";
import { AttachmentChip } from "./attachment-chip";
import type { ComposerAttachment } from "../hooks/useComposerAttachments";

interface ChatInputBarProps {
  input: string;
  isTyping: boolean;
  model: string;
  models: string[];
  onInputChange: (value: string) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  onStop: () => void;
  onSetModel: (model: string) => void;
  textareaRef: React.RefObject<HTMLTextAreaElement | null>;
  placeholder?: string;
  hint?: string | null;
  /** Extra control next to the send button, e.g. push-to-talk. */
  voiceSlot?: React.ReactNode;
  /** Controls at the start of the composer bar, e.g. the attach menu and workspace chip. */
  leadingSlot?: React.ReactNode;
  attachments?: ComposerAttachment[];
  onRemoveAttachment?: (key: string) => void;
  /** Receives files dropped on or pasted into the composer; without it, dropping and pasting files is off. */
  onAddFiles?: (files: File[]) => void;
  /** Keeps the message from being sent, e.g. while attachments are still uploading. */
  sendBlocked?: boolean;
  /** A problem with the attachments to show above the composer. */
  attachmentProblem?: string | null;
}

const hasFiles = (e: React.DragEvent) => Array.from(e.dataTransfer.types).includes("Files");

export function ChatInputBar({
  input,
  isTyping,
  model,
  models,
  onInputChange,
  onKeyDown,
  onSubmit,
  onStop,
  onSetModel,
  textareaRef,
  placeholder = "Ask Sentium Assistant...",
  hint = "Enter: Send · Shift+Enter: New line · Ctrl+Shift+O: New chat",
  voiceSlot,
  leadingSlot,
  attachments = [],
  onRemoveAttachment,
  onAddFiles,
  sendBlocked = false,
  attachmentProblem,
}: ChatInputBarProps) {
  const [dragging, setDragging] = useState(false);
  const canSend = (input.trim().length > 0 || attachments.length > 0) && !sendBlocked;

  const dropHandlers = onAddFiles
    ? {
        onDragOver: (e: React.DragEvent<HTMLFormElement>) => {
          if (hasFiles(e)) {
            e.preventDefault();
            setDragging(true);
          }
        },
        onDragLeave: (e: React.DragEvent<HTMLFormElement>) => {
          if (!e.currentTarget.contains(e.relatedTarget as Node | null)) {
            setDragging(false);
          }
        },
        onDrop: (e: React.DragEvent<HTMLFormElement>) => {
          if (!hasFiles(e)) {
            return;
          }
          e.preventDefault();
          setDragging(false);
          onAddFiles(Array.from(e.dataTransfer.files));
        },
      }
    : {};

  const handlePaste = (e: React.ClipboardEvent<HTMLTextAreaElement>) => {
    const files = Array.from(e.clipboardData.files);
    if (onAddFiles && files.length > 0) {
      e.preventDefault();
      onAddFiles(files);
    }
  };

  return (
    <div className={styles.composerWrap}>
      {attachmentProblem && (
        <p className={styles.attachmentProblem} role="alert">
          {attachmentProblem}
        </p>
      )}
      <form onSubmit={onSubmit} className={styles.composer} data-dragging={dragging || undefined} {...dropHandlers}>
        {dragging && (
          <div className={styles.dropOverlay} aria-hidden="true">
            <Upload size={18} />
            <span>Drop files to attach</span>
          </div>
        )}

        {attachments.length > 0 && onRemoveAttachment && (
          <div className={styles.contextPills}>
            {attachments.map((a) => (
              <AttachmentChip key={a.key} attachment={a} onRemove={onRemoveAttachment} />
            ))}
          </div>
        )}

        <textarea
          ref={textareaRef}
          value={input}
          onChange={(e) => onInputChange(e.target.value)}
          onKeyDown={onKeyDown}
          onPaste={handlePaste}
          placeholder={isTyping ? "Generating..." : placeholder}
          className={styles.textarea}
          aria-label="Chat message"
          autoComplete="off"
          disabled={isTyping}
          rows={1}
        />

        <div className={styles.composerBar}>
          <div className={styles.composerLeading}>
            {leadingSlot}
            <ModelSelector
              variant="chip"
              models={models}
              value={model}
              onChange={onSetModel}
              placeholder="model name..."
            />
          </div>

          {voiceSlot && <span className={styles.voiceSlot}>{voiceSlot}</span>}

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
              disabled={!canSend}
              className={styles.sendButton}
              aria-label="Send message"
              title={sendBlocked ? "Waiting for uploads to finish" : undefined}
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
