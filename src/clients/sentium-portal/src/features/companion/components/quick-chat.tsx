import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router";
import { useQueryClient } from "@tanstack/react-query";
import { ArrowUp, ExternalLink, Plus, Square, X } from "lucide-react";
import { createConversation } from "@/features/assistant/api/assistant.service";
import { conversationKeys } from "@/features/assistant/api/assistant.queries";
import { MessageBubble } from "@/features/assistant/components/message-bubble";
import { useMessageListUi } from "@/features/assistant/hooks/useMessageListUi";
import { useModels } from "@/features/models/hooks/useModels";
import { Button } from "@/shared/ui/button";
import { StatusMessage } from "@/shared/ui/status-message";
import { markSpokenTurn } from "../audio/reply-speaker";
import { speechPlayer } from "../audio/speech-player";
import { useCompanionStore, useVoiceHint } from "../state/companion-store";
import { useQuickChatStore } from "../state/quick-chat-store";
import { isImeComposing } from "@/shared/utils/keyboard";
import { PushToTalkButton } from "./push-to-talk-button";
import styles from "./quick-chat.module.css";

interface QuickChatProps {
  /** The element that opened the popover; clicks on it are not "outside" clicks. */
  anchor: React.RefObject<HTMLElement | null>;
  position: React.CSSProperties;
  onClose: () => void;
}

/**
 * A small chat with the assistant that opens from the Companion on any page. Portalled to the body because the
 * sidebar clips its overflow. Tool approvals are given here by clicking, never by voice.
 */
export function QuickChat({ anchor, position, onClose }: QuickChatProps) {
  const queryClient = useQueryClient();
  const {
    activeConversationId,
    messages,
    isStreaming,
    setActiveConversation,
    sendMessage,
    respondToApproval,
    stopStreaming,
    retryLastMessage,
  } = useQuickChatStore();
  const { models, defaultModel } = useModels();
  const voice = useCompanionStore((s) => s.voice);
  const hint = useVoiceHint();

  const [input, setInput] = useState("");
  const [error, setError] = useState<string | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const ui = useMessageListUi(isStreaming);

  useEffect(() => {
    textareaRef.current?.focus();
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
        anchor.current?.focus();
      }
    };
    const onPointer = (e: PointerEvent) => {
      const target = e.target as Node;
      if (!panelRef.current?.contains(target) && !anchor.current?.contains(target)) {
        onClose();
      }
    };

    window.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointer);
    return () => {
      window.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onPointer);
    };
  }, [anchor, onClose]);

  useEffect(() => {
    if (listRef.current) {
      listRef.current.scrollTop = listRef.current.scrollHeight;
    }
  }, [messages]);

  const send = async (text: string, spoken = false) => {
    const content = text.trim();
    if (!content || isStreaming) {
      return;
    }
    setError(null);

    const model = defaultModel ?? models[0] ?? "";
    if (!model) {
      setError("No chat model is available. Load a model first.");
      return;
    }

    let conversationId = activeConversationId;
    if (!conversationId) {
      try {
        conversationId = (await createConversation({ model })).id;
        setActiveConversation(conversationId, [], model);
        void queryClient.invalidateQueries({ queryKey: conversationKeys.all });
      } catch {
        setError("Could not start a conversation.");
        return;
      }
    }

    if (spoken) {
      markSpokenTurn(useQuickChatStore);
    }
    setInput("");
    ui.resetStatus();
    void sendMessage({ conversationId, model, userContent: content });
  };

  const onTranscript = (text: string) => {
    if (voice.reviewBeforeSending) {
      setInput((current) => (current.trim() ? `${current.trim()} ${text}` : text));
      textareaRef.current?.focus();
      return;
    }
    void send(text, true);
  };

  const startOver = () => {
    stopStreaming();
    speechPlayer.stop();
    setActiveConversation(null, [], "");
    setError(null);
  };

  return createPortal(
    <div
      ref={panelRef}
      role="dialog"
      aria-label="Quick chat"
      className={`frame ${styles.panel}`}
      style={position}
      data-testid="quick-chat"
    >
      <header className={styles.header}>
        <span className={styles.heading}>quick chat</span>
        {activeConversationId && (
          <Link className={styles.headerLink} to={`/assistant/${activeConversationId}`} onClick={onClose}>
            <ExternalLink size={13} aria-hidden="true" /> Open in assistant
          </Link>
        )}
        <span className={styles.headerActions}>
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Plus size={15} />}
            aria-label="New quick chat"
            onClick={startOver}
            disabled={messages.length === 0}
          />
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<X size={15} />}
            aria-label="Close quick chat"
            onClick={onClose}
          />
        </span>
      </header>

      <div className={styles.messages} ref={listRef}>
        {messages.length === 0 ? (
          <p className={styles.empty}>
            Ask the assistant anything without leaving this page.
            {voice.canListen && " Hold the microphone to talk."}
          </p>
        ) : (
          messages.map((msg, i) => (
            <MessageBubble
              key={msg.id}
              msg={msg}
              isTyping={isStreaming}
              live={isStreaming && i === messages.length - 1}
              statusIndex={ui.statusIndex}
              statusVisible={ui.statusVisible}
              copiedMessageId={ui.copiedMessageId}
              onCopyMessage={ui.copyMessage}
              onApproval={(aiMsgId, requestId, approved, conversationId) => {
                ui.resetStatus();
                void respondToApproval({
                  aiMsgId,
                  requestId,
                  approved,
                  conversationId: conversationId ?? activeConversationId,
                });
              }}
              onRetry={msg.error ? retryLastMessage : undefined}
            />
          ))
        )}
      </div>

      <div className={styles.composerArea}>
        {error && <StatusMessage variant="error" message={error} />}
        {hint && (
          <p className={styles.hint} role="status">
            {hint}
          </p>
        )}

        <form
          className={styles.composer}
          onSubmit={(e) => {
            e.preventDefault();
            void send(input);
          }}
        >
          <textarea
            ref={textareaRef}
            className={styles.textarea}
            value={input}
            rows={1}
            placeholder={isStreaming ? "Generating…" : "Ask the assistant…"}
            aria-label="Quick chat message"
            autoComplete="off"
            disabled={isStreaming}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                if (!isImeComposing(e)) void send(input);
              }
            }}
          />

          <PushToTalkButton onTranscript={onTranscript} disabled={isStreaming} />

          {isStreaming ? (
            <button type="button" className={styles.send} onClick={stopStreaming} aria-label="Stop generation">
              <Square size={11} fill="currentColor" />
            </button>
          ) : (
            <button type="submit" className={styles.send} disabled={!input.trim()} aria-label="Send message">
              <ArrowUp size={16} />
            </button>
          )}
        </form>
      </div>
    </div>,
    document.body,
  );
}
