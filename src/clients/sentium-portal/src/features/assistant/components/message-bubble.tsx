import { memo, useState } from "react";
import Markdown from "react-markdown";
import { Check, ChevronDown, Copy, ShieldAlert, Sparkles } from "lucide-react";
import styles from "../assistant.module.css";
import { Button } from "@/shared/ui/button";
import { SentiumMark } from "@/shared/ui/sentium-mark";
import { AgentTimeline } from "@/shared/agent-activity";
import type { ConversationMessage } from "@/features/assistant/types/assistant";
import { STATUS_MESSAGES } from "../status-messages";

interface MessageBubbleProps {
  msg: ConversationMessage;
  isTyping: boolean;
  live?: boolean;
  statusIndex: number;
  statusVisible: boolean;
  copiedMessageId: string | null;
  onCopyMessage: (content: string, id: string) => void;
  onApproval: (aiMsgId: string, requestId: string, approved: boolean, conversationId?: string) => void;
  onRetry?: () => void;
  senderName?: string;
}

/**
 * Memoised: while an answer streams the list re-renders on every chunk, and only the last bubble's props change.
 */
export const MessageBubble = memo(function MessageBubble({
  msg,
  isTyping,
  live,
  statusIndex,
  statusVisible,
  copiedMessageId,
  onCopyMessage,
  onApproval,
  onRetry,
  senderName = "Sentium",
}: MessageBubbleProps) {
  const [enhancedOpen, setEnhancedOpen] = useState(false);

  const isAssistant = msg.role === "assistant";
  const isLastMessage = isAssistant && msg.content === "" && isTyping && !msg.error;
  const steps = msg.steps ?? [];
  const showStatusCycler = isLastMessage && steps.length === 0;
  const isLive = (live ?? isLastMessage) && !msg.error;
  const pending = msg.pendingApproval;

  const approvalOnTimeline =
    pending !== undefined &&
    steps.some(
      (s) =>
        s.kind === "tool" &&
        s.status === "awaiting_approval" &&
        (pending.callId ? s.callId === pending.callId : s.name === pending.toolName),
    );

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
      <SentiumMark size={26} className={styles.avatar} />

      <div className={styles.aiBody}>
        <span className={styles.sender}>{senderName}</span>

        {steps.length > 0 && (
          <AgentTimeline
            className={styles.activity}
            steps={steps}
            live={isLive}
            collapsible
            hasAnswer={msg.content.trim().length > 0}
            approval={
              pending && approvalOnTimeline
                ? {
                    callId: pending.callId,
                    toolName: pending.toolName,
                    disabled: isTyping,
                    onDecide: (approved) => onApproval(msg.id, pending.requestId, approved, pending.conversationId),
                  }
                : undefined
            }
          />
        )}

        {msg.pendingApproval && !approvalOnTimeline && (
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
});
