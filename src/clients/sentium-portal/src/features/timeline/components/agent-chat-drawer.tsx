import { useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { useQueryClient } from "@tanstack/react-query";
import { Bot, ExternalLink, X } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import { StatusMessage } from "@/shared/ui/status-message";
import { ChatInputBar } from "@/features/assistant/components/chat-input-bar";
import { MessageBubble } from "@/features/assistant/components/message-bubble";
import { useMessageListUi } from "@/features/assistant/hooks/useMessageListUi";
import { createConversation, fetchConversation } from "@/features/assistant/api/assistant.service";
import type { ConversationMessage } from "@/features/assistant/types/assistant";
import { useAgentRoster } from "@/features/agents/hooks/useAgentRoster";
import { useModels } from "@/features/models/hooks/useModels";
import { MODELS_QUERY_KEY } from "@/features/models/hooks/useModelCatalog";
import { fetchModelCatalog } from "@/features/models/api/models.service";
import { useTimelineChatStore, timelineThreads } from "../state/timeline-chat-store";
import { timelineKeys } from "../hooks/useTimeline";
import type { AgentChatContext } from "../types/timeline";
import { contextAgent, contextKey, contextLabel, contextPrefix, starterPrompts } from "../utils/agent-context";

interface AgentChatDrawerProps {
  context: AgentChatContext;
  today: string;
  onClose: () => void;
}

export function AgentChatDrawer({ context, today, onClose }: AgentChatDrawerProps) {
  const queryClient = useQueryClient();
  const {
    activeConversationId,
    messages,
    isStreaming,
    streamingConversationId,
    setActiveConversation,
    sendMessage,
    respondToApproval,
    stopStreaming,
    retryLastMessage,
  } = useTimelineChatStore();
  const { roster } = useAgentRoster();
  const { models, defaultModel } = useModels();

  const key = contextKey(context);
  const agentName = contextAgent(context);
  const agent = roster.find((a) => a.name === agentName);
  const [model, setModel] = useState("");
  const effectiveModel = model || agent?.model || defaultModel || models[0] || "";

  const [input, setInput] = useState("");
  const [error, setError] = useState<string | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const threadId = timelineThreads.get(key) ?? null;
  const showsThisThread = threadId !== null && activeConversationId === threadId;
  const visibleMessages: ConversationMessage[] = showsThisThread ? messages : [];
  const isTyping = isStreaming && streamingConversationId === threadId && threadId !== null;
  const ui = useMessageListUi(isTyping);

  // Switching to another task/month: stop a reply still streaming for the previous one, then show this thread.
  useEffect(() => {
    const thread = timelineThreads.get(key);
    const { isStreaming: streaming, activeConversationId: active } = useTimelineChatStore.getState();
    if (streaming && active !== thread) {
      useTimelineChatStore.getState().stopStreaming();
    }
    if (!thread) {
      if (active !== null) setActiveConversation(null, [], "");
      return;
    }
    if (active === thread) return;

    let cancelled = false;
    fetchConversation(thread)
      .then((data) => {
        if (cancelled) return;
        setActiveConversation(
          thread,
          (data.messages ?? []).map((m) => ({ ...m, timestamp: new Date(m.timestamp) })),
          data.model,
        );
      })
      .catch(() => !cancelled && setError("The earlier conversation could not be loaded."));
    return () => {
      cancelled = true;
    };
  }, [key, setActiveConversation]);

  // A finished reply may have added suggestions or notes: refresh the timeline.
  const wasStreaming = useRef(false);
  useEffect(() => {
    if (wasStreaming.current && !isStreaming) {
      void queryClient.invalidateQueries({ queryKey: timelineKeys.all });
    }
    wasStreaming.current = isStreaming;
  }, [isStreaming, queryClient]);

  useEffect(() => {
    if (listRef.current) listRef.current.scrollTop = listRef.current.scrollHeight;
  }, [visibleMessages.length, messages]);

  useEffect(() => {
    textareaRef.current?.focus();
  }, [key]);

  /** The chosen model, else the agent's or the default one - waiting for the model catalog if it is still loading. */
  const resolveModel = async () => {
    if (effectiveModel) return effectiveModel;
    try {
      const catalog = await queryClient.fetchQuery({ queryKey: MODELS_QUERY_KEY, queryFn: fetchModelCatalog });
      return agent?.model || catalog.defaultModel || catalog.models[0]?.id || "";
    } catch {
      return "";
    }
  };

  const send = async (text: string) => {
    const content = text.trim();
    if (!content || isTyping) return;
    setError(null);
    const model = await resolveModel();
    if (!model) {
      setError("No chat model is available. Load a model first.");
      return;
    }

    let conversationId = threadId;
    if (!conversationId) {
      try {
        const created = await createConversation({ title: `Timeline · ${contextLabel(context)}`, model, agentName });
        conversationId = created.id;
        timelineThreads.set(key, conversationId);
        setActiveConversation(conversationId, [], model);
        void queryClient.invalidateQueries({ queryKey: ["conversations"] });
      } catch {
        setError(`Could not start a conversation with ${agentName}.`);
        return;
      }
    }

    const first = visibleMessages.length === 0;
    setInput("");
    ui.resetStatus();
    void sendMessage({
      conversationId,
      model,
      userContent: first ? `${contextPrefix(context, today)} ${content}` : content,
    });
  };

  return (
    <aside className={`${styles.drawer} mat-thick frame`} aria-label="Agent chat" data-testid="agent-chat-drawer">
      <header className={styles.drawerHead}>
        <span className={styles.drawerAgent}>
          <Bot size={16} aria-hidden="true" />
          <span>
            <strong>{agentName}</strong>
            <span className={styles.drawerContext}>{contextLabel(context)}</span>
          </span>
        </span>
        {threadId && (
          <Link className={styles.drawerLink} to={`/assistant/${threadId}`}>
            <ExternalLink size={13} aria-hidden="true" /> Open in assistant
          </Link>
        )}
        <Button
          size="sm"
          variant="ghost"
          iconOnly
          icon={<X size={16} />}
          aria-label="Close agent chat"
          onClick={onClose}
        />
      </header>

      <div className={styles.drawerMessages} ref={listRef}>
        {visibleMessages.length === 0 ? (
          <div className={styles.drawerEmpty}>
            <p>
              Ask {agentName} about <strong>{contextLabel(context)}</strong>. Suggestions it makes appear on the
              timeline for you to accept.
            </p>
            <div className={styles.starters}>
              {starterPrompts(context).map((prompt) => (
                <button key={prompt} type="button" className={styles.starter} onClick={() => setInput(prompt)}>
                  {prompt}
                </button>
              ))}
            </div>
          </div>
        ) : (
          visibleMessages.map((msg) => (
            <MessageBubble
              key={msg.id}
              msg={msg}
              isTyping={isTyping}
              expandedThoughts={ui.expandedThoughts}
              statusIndex={ui.statusIndex}
              statusVisible={ui.statusVisible}
              copiedMessageId={ui.copiedMessageId}
              onToggleThought={ui.toggleThought}
              onCopyMessage={ui.copyMessage}
              onApproval={(aiMsgId, requestId, approved, conversationId) => {
                ui.resetStatus();
                void respondToApproval({ aiMsgId, requestId, approved, conversationId: conversationId ?? threadId });
              }}
              onRetry={msg.error ? retryLastMessage : undefined}
              senderName={agentName}
            />
          ))
        )}
      </div>

      {error && <StatusMessage variant="error" message={error} />}

      <ChatInputBar
        input={input}
        isTyping={isTyping}
        contextPills={[]}
        model={effectiveModel}
        models={models}
        onInputChange={setInput}
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            void send(input);
          }
        }}
        onSubmit={(e) => {
          e.preventDefault();
          void send(input);
        }}
        onStop={stopStreaming}
        onRemoveContextPill={() => {}}
        onSetModel={setModel}
        textareaRef={textareaRef}
        placeholder={`Message ${agentName}…`}
        hint={null}
      />
    </aside>
  );
}
