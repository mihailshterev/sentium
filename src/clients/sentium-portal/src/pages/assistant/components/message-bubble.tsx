import { useState } from "react";
import Markdown from "react-markdown";
import { ChevronDown, Wrench, Check, Copy, BotMessageSquare, Brain, Sparkles } from "lucide-react";
import styles from "../assistant.module.scss";
import type { ConversationMessage } from "../../../types/assistant";

const STATUS_MESSAGES = [
  "Synthesizing latent variables...",
  "Optimizing heuristic pathways...",
  "Reconciling disparate data points...",
  "Querying the collective consciousness...",
  "Assembling coherent thought-clusters...",
  "Teaching the server how to love...",
  "Rounding up the rogue bits...",
  "Calculating the last digit of Pi (almost there)...",
  "Poking the mainframe with a stick...",
  "Dusting off the neural pathways...",
  "Reticulating splines...",
  "Consulting the Oracle...",
  "Buffing the chrome on the logic gates...",
  "Achieving 99% sentience... please hold...",
  "Defragmenting my digital soul...",
  "Checking under the digital rug...",
  "Asking my supervisor (a toaster)...",
  "Ignoring the laws of thermodynamics...",
];

export { STATUS_MESSAGES };

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
}

const MessageBubble = ({
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
}: MessageBubbleProps) => {
  const [enhancedOpen, setEnhancedOpen] = useState(false);

  const isLastMessage = msg.role === "assistant" && msg.content === "" && isTyping && !msg.error;

  const showStatusCycler = isLastMessage && !msg.thought && (!msg.toolCalls || msg.toolCalls.length === 0);

  return (
    <div className={`${styles.messageWrapper} ${msg.role === "user" ? styles.wrapperUser : styles.wrapperAi}`}>
      {msg.role === "assistant" && (
        <div className={`${styles.avatar} ${styles.avatarAi}`}>
          <BotMessageSquare size={13} />
        </div>
      )}
      <div className={`${styles.message} ${msg.role === "user" ? styles.messageUser : styles.messageAi}`}>
        {msg.role === "assistant" && (
          <div className={styles.messageHeader}>
            <span className={styles.sender}>SENTIUM</span>
          </div>
        )}

        {msg.role === "assistant" && (msg.thought !== undefined || (msg.toolCalls && msg.toolCalls.length > 0)) && (
          <div className={styles.activityTimeline}>
            {msg.thought !== undefined && (
              <div className={styles.timelineRow}>
                <span className={`${styles.timelineDot} ${styles.timelineDotThought}`} />
                <div className={styles.timelineRowBody}>
                  <div className={styles.thoughtBlock}>
                    {(() => {
                      const isActiveThought = isTyping && msg.content === "";
                      return (
                        <button
                          className={`${styles.thoughtHeader} ${isActiveThought ? styles.thoughtHeaderActive : ""}`}
                          onClick={() => onToggleThought(msg.id)}
                        >
                          <Brain size={11} className={isActiveThought ? styles.brainPulse : undefined} />
                          <span>Thinking</span>
                          {isActiveThought && (
                            <span className={styles.thinkingDots}>
                              <span className={styles.thinkingDot} />
                              <span className={styles.thinkingDot} />
                              <span className={styles.thinkingDot} />
                            </span>
                          )}
                          <ChevronDown
                            size={11}
                            className={`${styles.thoughtChevron} ${expandedThoughts.has(msg.id) ? styles.thoughtChevronOpen : ""}`}
                          />
                        </button>
                      );
                    })()}
                    {expandedThoughts.has(msg.id) && (
                      <div className={styles.thoughtContent}>
                        <Markdown>{msg.thought}</Markdown>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            )}

            {msg.toolCalls?.map((call, i) => (
              <div key={`tool-${i}`} className={styles.timelineRow}>
                <span className={`${styles.timelineDot} ${styles.timelineDotTool}`} />
                <div className={styles.timelineRowBody}>
                  <div className={styles.toolCallRow}>
                    <Wrench size={10} />
                    <span>{call}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {msg.pendingApproval && msg.role === "assistant" && (
          <div className={styles.approvalBlock}>
            <div className={styles.approvalHeader}>
              <Wrench size={11} />
              <span>Tool Approval Required</span>
            </div>
            <div className={styles.approvalBody}>
              <div className={styles.approvalToolName}>{msg.pendingApproval.toolName}</div>
              {Object.keys(msg.pendingApproval.arguments).length > 0 && (
                <pre className={styles.approvalArgs}>{JSON.stringify(msg.pendingApproval.arguments, null, 2)}</pre>
              )}
            </div>
            <div className={styles.approvalActions}>
              <button
                className={styles.approvalDeny}
                onClick={() =>
                  onApproval(msg.id, msg.pendingApproval!.requestId, false, msg.pendingApproval!.conversationId)
                }
                disabled={isTyping}
              >
                Deny
              </button>
              <button
                className={styles.approvalApprove}
                onClick={() =>
                  onApproval(msg.id, msg.pendingApproval!.requestId, true, msg.pendingApproval!.conversationId)
                }
                disabled={isTyping}
              >
                Approve
              </button>
            </div>
          </div>
        )}

        {showStatusCycler ? (
          <div className={styles.typingStatusRow}>
            <div className={styles.neuronLoader}>
              <div className={styles.neuronNode} />
              <div className={styles.neuronLine} />
              <div className={styles.neuronNode} />
              <div className={styles.neuronLine} />
              <div className={styles.neuronNode} />
            </div>
            <span
              className={`${styles.typingStatusText} ${statusVisible ? styles.statusVisible : styles.statusHidden}`}
            >
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

        {msg.enhancedPrompt && msg.role === "user" && (
          <div className={styles.enhancedBlock}>
            <button className={styles.enhancedHeader} onClick={() => setEnhancedOpen((v) => !v)}>
              <Sparkles size={11} />
              <span>Enhanced prompt</span>
              <ChevronDown
                size={11}
                className={`${styles.thoughtChevron} ${enhancedOpen ? styles.thoughtChevronOpen : ""}`}
              />
            </button>
            {enhancedOpen && (
              <div className={styles.enhancedContent}>
                <Markdown>{msg.enhancedPrompt}</Markdown>
              </div>
            )}
          </div>
        )}

        {msg.error && msg.role === "assistant" && (
          <div className={styles.errorBanner}>
            <span className={styles.errorBannerText}>{msg.error}</span>
            {onRetry && (
              <button className={styles.retryBtn} onClick={onRetry} disabled={isTyping}>
                Retry
              </button>
            )}
          </div>
        )}

        {msg.role === "assistant" && !isLastMessage && msg.content && (
          <div className={styles.messageFooter}>
            <button
              className={styles.copyBtn}
              onClick={() => onCopyMessage(msg.content, msg.id)}
              title="Copy to clipboard"
            >
              {copiedMessageId === msg.id ? <Check size={12} /> : <Copy size={12} />}
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default MessageBubble;
