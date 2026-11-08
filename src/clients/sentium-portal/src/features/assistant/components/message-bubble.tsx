import { useState } from "react";
import Markdown from "react-markdown";
import { Brain, Check, ChevronDown, Copy, ShieldAlert, Sparkles, Wrench } from "lucide-react";
import styles from "../assistant.module.css";
import { Button } from "@/shared/ui/button";
import type { ConversationMessage } from "@/features/assistant/types/assistant";
import { STATUS_MESSAGES } from "../status-messages";

interface MessageBubbleProps {
  msg: ConversationMessage;
  isTyping: boolean;
  expandedThoughts: Set<string>;
  statusIndex: number;
  statusVisible: boolean;
  copiedMessageId: string | null;
  onToggleThought: (id: string) => void;
  onCopyMessage: (content: string, id: string) => void;
  onApproval: (aiMsgId: string, requestId: string, approved: boolean, conversationId?: string) => void;
  onRetry?: () => void;
  senderName?: string;
}

export function MessageBubble({
  msg,
  isTyping,
  expandedThoughts,
  statusIndex,
  statusVisible,
  copiedMessageId,
  onToggleThought,
  onCopyMessage,
  onApproval,
  onRetry,
  senderName = "Sentium",
}: MessageBubbleProps) {
  const [enhancedOpen, setEnhancedOpen] = useState(false);

  const isAssistant = msg.role === "assistant";
  const isLastMessage = isAssistant && msg.content === "" && isTyping && !msg.error;
  const showStatusCycler = isLastMessage && !msg.thought && (!msg.toolCalls || msg.toolCalls.length === 0);
  const isActiveThought = isTyping && msg.content === "";
  const thoughtOpen = expandedThoughts.has(msg.id);
  const hasActivity = isAssistant && (msg.thought !== undefined || (msg.toolCalls && msg.toolCalls.length > 0));

  if (!isAssistant) {
    return (
      <div className={`${styles.message} ${styles.messageUser}`}>
        <div className={styles.userBubble}>
          <div className={styles.content}>
            <Markdown>{msg.content}</Markdown>
          </div>
          {msg.enhancedPrompt && (
            <div className={styles.enhanced}>
              <button
                type="button"
                className={styles.disclosure}
                onClick={() => setEnhancedOpen((v) => !v)}
                aria-expanded={enhancedOpen}
              >
                <Sparkles size={13} aria-hidden="true" />
                <span>Enhanced prompt</span>
                <ChevronDown size={13} className={enhancedOpen ? styles.chevronOpen : styles.chevron} />
              </button>
              {enhancedOpen && (
                <div className={`${styles.content} ${styles.enhancedContent}`}>
                  <Markdown>{msg.enhancedPrompt}</Markdown>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className={`${styles.message} ${styles.messageAi}`}>
      <img src="/sentium-mark.png" alt="" className={styles.avatar} width={26} height={26} />

      <div className={styles.aiBody}>
        <span className={styles.sender}>{senderName}</span>

        {hasActivity && (
          <div className={styles.activity}>
            {msg.thought !== undefined && (
              <div className={styles.thought}>
                <button
                  type="button"
                  className={`${styles.disclosure} ${isActiveThought ? styles.disclosureActive : ""}`}
                  onClick={() => onToggleThought(msg.id)}
                  aria-expanded={thoughtOpen}
                >
                  <Brain size={13} aria-hidden="true" />
                  <span>Thinking</span>
                  {isActiveThought && (
                    <span className={styles.dots} aria-hidden="true">
                      <span />
                      <span />
                      <span />
                    </span>
                  )}
                  <ChevronDown size={13} className={thoughtOpen ? styles.chevronOpen : styles.chevron} />
                </button>
                {thoughtOpen && (
                  <div className={`${styles.content} ${styles.thoughtContent}`}>
                    <Markdown>{msg.thought}</Markdown>
                  </div>
                )}
              </div>
            )}

            {msg.toolCalls && msg.toolCalls.length > 0 && (
              <ul className={styles.toolCalls}>
                {msg.toolCalls.map((call, i) => (
                  <li key={`tool-${i}`} className={styles.toolCall}>
                    <Wrench size={12} aria-hidden="true" />
                    <span>{call}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {msg.pendingApproval && (
          <div className={styles.approval} role="group" aria-label="Tool approval">
            <div className={styles.approvalHeader}>
              <ShieldAlert size={15} aria-hidden="true" />
              <span>Tool Approval Required</span>
            </div>
            <div className={styles.approvalTool}>{msg.pendingApproval.toolName}</div>
            {Object.keys(msg.pendingApproval.arguments).length > 0 && (
              <pre className={styles.approvalArgs}>{JSON.stringify(msg.pendingApproval.arguments, null, 2)}</pre>
            )}
            <div className={styles.approvalActions}>
              <Button
                variant="secondary"
                size="sm"
                onClick={() =>
                  onApproval(msg.id, msg.pendingApproval!.requestId, false, msg.pendingApproval!.conversationId)
                }
                disabled={isTyping}
              >
                Deny
              </Button>
              <Button
                variant="primary"
                size="sm"
                onClick={() =>
                  onApproval(msg.id, msg.pendingApproval!.requestId, true, msg.pendingApproval!.conversationId)
                }
                disabled={isTyping}
              >
                Approve
              </Button>
            </div>
          </div>
        )}

        {showStatusCycler ? (
          <div className={styles.typing} role="status" aria-label="Assistant is thinking">
            <span className={styles.dots} aria-hidden="true">
              <span />
              <span />
              <span />
            </span>
            <span className={`${styles.typingText} ${statusVisible ? styles.statusVisible : styles.statusHidden}`}>
              {STATUS_MESSAGES[statusIndex]}
            </span>
          </div>
        ) : (
          msg.content && (
            <div className={styles.content}>
              <Markdown>{msg.content}</Markdown>
            </div>
          )
        )}

        {msg.error && (
          <div className={styles.errorBanner} role="alert">
            <span>{msg.error}</span>
            {onRetry && (
              <Button variant="secondary" size="sm" onClick={onRetry} disabled={isTyping}>
                Retry
              </Button>
            )}
          </div>
        )}

        {!isLastMessage && msg.content && (
          <div className={styles.messageActions}>
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              icon={copiedMessageId === msg.id ? <Check size={14} /> : <Copy size={14} />}
              onClick={() => onCopyMessage(msg.content, msg.id)}
              title="Copy to clipboard"
            />
          </div>
        )}
      </div>
    </div>
  );
}
