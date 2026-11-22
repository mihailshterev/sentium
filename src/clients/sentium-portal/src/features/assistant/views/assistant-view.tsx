import React, { useState, useRef, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router";
import styles from "../assistant.module.css";
import { ArrowDown, Loader2, PanelRightClose, PanelRightOpen } from "lucide-react";
import { fetchConversation, setConversationWorkspace } from "@/features/assistant/api/assistant.service";
import { useConversations } from "@/features/assistant/hooks/useConversations";
import { useDebouncedValue } from "@/shared/hooks/useDebouncedValue";
import { useModels } from "@/features/models/hooks/useModels";
import type { ConversationMessage, ConversationSummary } from "@/features/assistant/types/assistant";
import { useConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import { markSpokenTurn } from "@/features/companion/audio/reply-speaker";
import { PushToTalkButton } from "@/features/companion/components/push-to-talk-button";
import { useCompanionStore, useVoiceHint } from "@/features/companion/state/companion-store";
import { SUGGESTIONS_POOL } from "@/shared/utils/constants";
import { isImeComposing } from "@/shared/utils/keyboard";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/shared/ui/button";
import { Page } from "@/shared/ui/page";
import { MessageBubble } from "../components/message-bubble";
import { useMessageListUi } from "../hooks/useMessageListUi";
import { messageFromDto } from "../utils/message-from-dto";
import { WelcomeScreen } from "../components/welcome-screen";
import { ChatInputBar } from "../components/chat-input-bar";
import { ConversationSidebar } from "../components/conversation-sidebar";
import { AttachMenu } from "../components/attach-menu";
import { WorkspaceChip, type ChatWorkspace } from "../components/workspace-chip";
import { useComposerAttachments } from "../hooks/useComposerAttachments";
import { useUploadRules } from "@/features/workspaces/hooks/useUploadRules";
import { acceptAttribute } from "@/features/workspaces/utils/upload-validation";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { StatusMessage } from "@/shared/ui/status-message";

const groupConversationsByDate = (convs: ConversationSummary[]) => {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const yesterday = new Date(today.getTime() - 86_400_000);
  const sevenDaysAgo = new Date(today.getTime() - 7 * 86_400_000);

  const groups: { label: string; items: ConversationSummary[] }[] = [
    { label: "Today", items: [] },
    { label: "Yesterday", items: [] },
    { label: "Previous 7 Days", items: [] },
    { label: "Older", items: [] },
  ];

  convs.forEach((conv) => {
    const d = new Date(conv.createdAt);
    const cd = new Date(d.getFullYear(), d.getMonth(), d.getDate());
    if (cd >= today) {
      groups[0].items.push(conv);
    } else if (cd >= yesterday) {
      groups[1].items.push(conv);
    } else if (cd >= sevenDaysAgo) {
      groups[2].items.push(conv);
    } else {
      groups[3].items.push(conv);
    }
  });

  return groups.filter((g) => g.items.length > 0);
};

export function AssistantView() {
  // One selector per field: while an answer streams only `messages` changes, and the actions never do.
  const activeConversationId = useConversationStore((s) => s.activeConversationId);
  const messages = useConversationStore((s) => s.messages);
  const model = useConversationStore((s) => s.model);
  const isStreaming = useConversationStore((s) => s.isStreaming);
  const streamingConversationId = useConversationStore((s) => s.streamingConversationId);
  const setActiveConversation = useConversationStore((s) => s.setActiveConversation);
  const setModel = useConversationStore((s) => s.setModel);
  const sendMessage = useConversationStore((s) => s.sendMessage);
  const respondToApproval = useConversationStore((s) => s.respondToApproval);
  const stopStreaming = useConversationStore((s) => s.stopStreaming);
  const retryLastMessage = useConversationStore((s) => s.retryLastMessage);

  const [conversationSearch, setConversationSearch] = useState("");
  const debouncedConversationSearch = useDebouncedValue(conversationSearch);

  const {
    conversations,
    totalCount: conversationCount,
    createConversation,
    deleteConversation: deleteConversationMutate,
    renameConversation,
    isCreating,
    hasMore: hasMoreConversations,
    loadMore: loadMoreConversations,
    isLoadingMore: isLoadingMoreConversations,
  } = useConversations(debouncedConversationSearch);

  const { models, defaultModel } = useModels();

  const { conversationId: routeConversationId } = useParams<{ conversationId?: string }>();
  const navigate = useNavigate();

  const [input, setInput] = useState("");
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [chatWorkspace, setChatWorkspace] = useState<ChatWorkspace | null>(null);
  const [isAtBottom, setIsAtBottom] = useState(true);

  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const [conversationToDelete, setConversationToDelete] = useState<string | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);

  const chatAreaRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const isTyping = isStreaming && streamingConversationId === activeConversationId;
  const queryClient = useQueryClient();
  const uploadRules = useUploadRules();
  const composer = useComposerAttachments(chatWorkspace?.id ?? null);
  const clearAttachments = composer.clear;
  const { copiedMessageId, copyMessage, statusIndex, statusVisible, resetStatus } = useMessageListUi(isTyping);

  const scrollToBottom = useCallback(() => {
    if (chatAreaRef.current) {
      chatAreaRef.current.scrollTop = chatAreaRef.current.scrollHeight;
    }
  }, []);

  const handleScroll = useCallback(() => {
    if (!chatAreaRef.current) {
      return;
    }
    const { scrollTop, scrollHeight, clientHeight } = chatAreaRef.current;
    setIsAtBottom(scrollHeight - scrollTop - clientHeight < 60);
  }, []);

  useEffect(() => {
    const el = chatAreaRef.current;
    if (!el) {
      return;
    }
    el.addEventListener("scroll", handleScroll);
    return () => el.removeEventListener("scroll", handleScroll);
  }, [handleScroll]);

  useEffect(() => {
    if (isAtBottom) {
      scrollToBottom();
    }
  }, [messages, isTyping, isAtBottom, scrollToBottom]);

  useEffect(() => {
    const preferred = defaultModel ?? models[0];
    if (!model && preferred) {
      setModel(preferred);
    }
  }, [models, defaultModel, model, setModel]);

  useEffect(() => {
    let cancelled = false;
    if (routeConversationId) {
      if (routeConversationId === activeConversationId) {
        return;
      }
      // Always load, even the conversation whose turn is streaming: the store swaps in its live copy.
      fetchConversation(routeConversationId)
        .then((data) => {
          if (cancelled) {
            return;
          }
          const loadedMessages: ConversationMessage[] = (data.messages ?? []).map(messageFromDto);
          setActiveConversation(routeConversationId, loadedMessages, data.model);
          setModel(data.model);
          setChatWorkspace(data.workspaceId ? { id: data.workspaceId, name: data.workspaceName ?? "Workspace" } : null);
        })
        .catch(() => {
          if (!cancelled) {
            navigate("/assistant", { replace: true });
          }
        });
    }
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [routeConversationId, activeConversationId]);

  // Attachments belong to the message being written in one conversation; switching conversations drops them.
  useEffect(() => {
    clearAttachments();
  }, [routeConversationId, clearAttachments]);

  const [randomizedSuggestions] = useState(() => [...SUGGESTIONS_POOL].sort(() => 0.5 - Math.random()).slice(0, 4));

  const changeWorkspace = async (next: ChatWorkspace | null) => {
    const previous = chatWorkspace;
    setChatWorkspace(next);
    if (!activeConversationId) {
      return;
    }
    try {
      await setConversationWorkspace(activeConversationId, next?.id ?? null);
      void queryClient.invalidateQueries({ queryKey: ["conversations"] });
    } catch {
      setChatWorkspace(previous);
      setSendError("The workspace could not be changed. Please try again.");
    }
  };

  const loadConversation = (conv: ConversationSummary) => {
    if (conv.id !== activeConversationId) {
      navigate(`/assistant/${conv.id}`);
    }
  };

  const createNewConversation = async (): Promise<string | null> => {
    try {
      const data = await createConversation({ model, ...(chatWorkspace ? { workspaceId: chatWorkspace.id } : {}) });
      setActiveConversation(data.id, [], model);
      navigate(`/assistant/${data.id}`);
      return data.id;
    } catch {
      setSendError("Failed to create a conversation. Please try again.");
      return null;
    }
  };

  const deleteConversation = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setConversationToDelete(id);
    setIsConfirmOpen(true);
  };

  const handleConfirmDelete = () => {
    if (!conversationToDelete) return;

    deleteConversationMutate(conversationToDelete, {
      onSuccess: () => {
        if (activeConversationId === conversationToDelete) {
          setActiveConversation(null, [], model);
          navigate("/assistant");
        }
        setIsConfirmOpen(false);
        setConversationToDelete(null);
      },
      onError: () => {
        setIsConfirmOpen(false);
        setConversationToDelete(null);
      },
    });
  };

  const handleCancelDelete = () => {
    setIsConfirmOpen(false);
    setConversationToDelete(null);
  };

  const handleStop = () => {
    stopStreaming();
  };

  const handleApproval = (aiMsgId: string, requestId: string, approved: boolean, conversationId?: string) => {
    resetStatus();
    void respondToApproval({ aiMsgId, requestId, approved, conversationId: conversationId ?? activeConversationId });
  };

  const reviewTranscripts = useCompanionStore((s) => s.voice.reviewBeforeSending);
  const voiceHint = useVoiceHint();

  /** `spoken`: a push-to-talk transcript, sent as it is instead of the typed input. */
  const submitMessage = async (spoken?: string) => {
    const text = spoken ?? input;
    const attachments = composer.ready;
    const hasContent = text.trim() || attachments.length > 0;
    if (!hasContent || isTyping || composer.isUploading) {
      return;
    }

    setSendError(null);

    let conversationId = activeConversationId;
    if (!conversationId) {
      conversationId = await createNewConversation();
      if (!conversationId) {
        return;
      }
    }

    if (spoken) {
      markSpokenTurn(useConversationStore);
    }

    setInput("");
    composer.clear();
    resetStatus();

    void sendMessage({ conversationId, model, userContent: text.trim(), attachments });
  };

  const handleTranscript = (transcript: string) => {
    if (reviewTranscripts) {
      setInput(input.trim() ? `${input.trim()} ${transcript}` : transcript);
      textareaRef.current?.focus();
      return;
    }
    void submitMessage(transcript);
  };

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    submitMessage();
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      if (!isImeComposing(e)) {
        submitMessage();
      }
    }
    // Ctrl+K belongs to the command palette, so a new chat uses the Ctrl+Shift+O convention.
    if ((e.metaKey || e.ctrlKey) && e.shiftKey && e.key.toLowerCase() === "o") {
      e.preventDefault();
      createNewConversation();
    }
    if (e.key === "ArrowUp" && input === "") {
      const lastUserMsg = [...messages].reverse().find((m) => m.role === "user");
      if (lastUserMsg) {
        e.preventDefault();
        setInput(lastUserMsg.content);
      }
    }
  };

  const isEmpty = messages.length === 0 && !isTyping;
  const isLoadingConversation = !!routeConversationId && routeConversationId !== activeConversationId;
  const conversationGroups = groupConversationsByDate(conversations);

  return (
    <Page
      title="Assistant"
      subtitle="Chat with the Sentium assistant"
      scroll={false}
      width="full"
      actions={
        <Button
          variant="secondary"
          size="sm"
          iconOnly
          icon={sidebarOpen ? <PanelRightClose size={16} /> : <PanelRightOpen size={16} />}
          onClick={() => setSidebarOpen((v) => !v)}
          title="Toggle conversations"
          aria-pressed={sidebarOpen}
        />
      }
    >
      <div className={styles.layout} data-sidebar-open={sidebarOpen || undefined}>
        <section
          className={styles.chatPanel}
          aria-label="Conversation"
          data-empty={(isEmpty && !isLoadingConversation) || undefined}
        >
          <div className={styles.chatArea} ref={chatAreaRef}>
            {isLoadingConversation ? (
              <div className={styles.loadingConversation} role="status">
                <Loader2 size={20} className="animate-spin" aria-hidden="true" />
                <span>Loading conversation…</span>
              </div>
            ) : isEmpty ? (
              <WelcomeScreen suggestions={randomizedSuggestions} onSelectSuggestion={(s) => setInput(s)} />
            ) : (
              <div className={styles.messagesArea}>
                {messages.map((msg, i) => {
                  const live = isTyping && i === messages.length - 1;
                  return (
                    <MessageBubble
                      key={msg.id}
                      msg={msg}
                      isTyping={isTyping}
                      live={live}
                      // Only the live bubble shows the status cycler; the others keep stable props and skip its ticks.
                      statusIndex={live ? statusIndex : 0}
                      statusVisible={live && statusVisible}
                      copiedMessageId={copiedMessageId}
                      onCopyMessage={copyMessage}
                      onApproval={handleApproval}
                      onRetry={msg.error ? retryLastMessage : undefined}
                    />
                  );
                })}
                <div ref={messagesEndRef} />
              </div>
            )}
          </div>

          <div className={styles.composerArea}>
            {!isAtBottom && (
              <Button
                variant="secondary"
                size="sm"
                iconOnly
                icon={<ArrowDown size={16} />}
                onClick={scrollToBottom}
                title="Scroll to bottom"
                className={styles.scrollToBottom}
              />
            )}

            {sendError && <StatusMessage variant="error" message={sendError} className={styles.sendError} />}

            <ChatInputBar
              input={input}
              isTyping={isTyping}
              model={model}
              models={models}
              onInputChange={setInput}
              onKeyDown={handleKeyDown}
              onSubmit={handleSubmit}
              onStop={handleStop}
              onSetModel={setModel}
              attachments={composer.attachments}
              onRemoveAttachment={composer.remove}
              onAddFiles={composer.uploadFiles}
              sendBlocked={composer.isUploading}
              attachmentProblem={composer.problem}
              leadingSlot={
                <>
                  <AttachMenu
                    preferredWorkspaceId={chatWorkspace?.id ?? null}
                    selectedIds={composer.selectedIds}
                    onToggleFile={composer.toggleFile}
                    onUploadFiles={composer.uploadFiles}
                    accept={acceptAttribute(uploadRules)}
                    disabled={isTyping}
                  />
                  <WorkspaceChip value={chatWorkspace} onChange={changeWorkspace} disabled={isTyping} />
                </>
              }
              textareaRef={textareaRef}
              voiceSlot={<PushToTalkButton onTranscript={handleTranscript} disabled={isTyping} />}
              hint={voiceHint ?? undefined}
            />
          </div>
        </section>

        <ConversationSidebar
          isOpen={sidebarOpen}
          conversations={conversations}
          conversationGroups={conversationGroups}
          totalCount={conversationCount}
          search={conversationSearch}
          onSearchChange={setConversationSearch}
          activeConversationId={routeConversationId ?? activeConversationId}
          isCreating={isCreating}
          hasMoreConversations={hasMoreConversations}
          isLoadingMoreConversations={isLoadingMoreConversations}
          onLoadMoreConversations={loadMoreConversations}
          onNewConversation={createNewConversation}
          onLoadConversation={loadConversation}
          onDeleteConversation={deleteConversation}
          onRenameConversation={(id, title) => renameConversation({ id, title })}
        />
      </div>

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Delete Conversation"
        description="Are you sure you want to delete this conversation? This action cannot be undone."
        confirmLabel="Delete Chat"
        cancelLabel="Cancel"
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
      />
    </Page>
  );
}
