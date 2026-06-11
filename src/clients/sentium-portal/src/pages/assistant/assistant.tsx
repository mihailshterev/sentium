import React, { useState, useRef, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router";
import styles from "./assistant.module.scss";
import { ChevronRight, ChevronDown, BotMessageSquare, Loader } from "lucide-react";
import { fetchConversation, fetchWorkspaces, fetchWorkspaceFiles } from "../../services/agentRuntime.service";
import useConversations from "../../hooks/useConversations";
import useModels from "../../hooks/useModels";
import type { ConversationMessage, ConversationMessageDto, ConversationSummary } from "../../types/assistant";
import type { Workspace } from "../../types/workspace";
import { useConversationStore } from "../../stores/assistant-conversation-store";
import { SUGGESTIONS_POOL } from "../../utils/constants";
import { useQuery } from "@tanstack/react-query";
import PageHeader from "../../components/ui/page-header";
import MessageBubble, { STATUS_MESSAGES } from "./components/message-bubble";
import WelcomeScreen from "./components/welcome-screen";
import ChatInputBar from "./components/chat-input-bar";
import ConversationSidebar from "./components/conversation-sidebar";
import ConfirmDialog from "../../components/ui/confirm-dialog";
import StatusMessage from "../../components/ui/status-message";

type ContextPill = { type: "workspace" | "file"; id: string; label: string };

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

const Assistant = () => {
  const {
    activeConversationId,
    messages,
    model,
    isStreaming,
    streamingConversationId,
    setActiveConversation,
    setModel,
    sendMessage,
    respondToApproval,
    stopStreaming,
    retryLastMessage,
  } = useConversationStore();

  const {
    conversations,
    createConversation,
    deleteConversation: deleteConversationMutate,
    isCreating,
  } = useConversations();

  const { models } = useModels();

  const { conversationId: routeConversationId } = useParams<{ conversationId?: string }>();
  const navigate = useNavigate();

  const [input, setInput] = useState("");
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [expandedThoughts, setExpandedThoughts] = useState<Set<string>>(new Set());
  const [wsContextOpen, setWsContextOpen] = useState(false);
  const [expandedWorkspace, setExpandedWorkspace] = useState<string | null>(null);
  const [contextPills, setContextPills] = useState<ContextPill[]>([]);
  const [copiedMessageId, setCopiedMessageId] = useState<string | null>(null);
  const [isAtBottom, setIsAtBottom] = useState(true);
  const [statusIndex, setStatusIndex] = useState(0);
  const [statusVisible, setStatusVisible] = useState(true);

  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const [conversationToDelete, setConversationToDelete] = useState<string | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);

  const toggleThought = (id: string) =>
    setExpandedThoughts((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });

  const chatAreaRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const isTyping = isStreaming && streamingConversationId === activeConversationId;

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
    if (models.length > 0 && !model) {
      setModel(models[0]);
    }
  }, [models, model, setModel]);

  useEffect(() => {
    let cancelled = false;
    if (routeConversationId) {
      if (routeConversationId === activeConversationId) {
        return;
      }
      if (isStreaming && streamingConversationId === routeConversationId) {
        return;
      }
      fetchConversation(routeConversationId)
        .then((data) => {
          if (cancelled) {
            return;
          }
          const loadedMessages: ConversationMessage[] = (data.messages ?? []).map((m: ConversationMessageDto) => ({
            id: m.id,
            role: m.role,
            content: m.content,
            enhancedPrompt: m.enhancedPrompt,
            thought: m.thought,
            toolCalls: m.toolCalls,
            timestamp: new Date(m.timestamp),
          }));
          setActiveConversation(routeConversationId, loadedMessages, data.model);
          setModel(data.model);
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
  }, [routeConversationId, isStreaming, streamingConversationId, activeConversationId]);

  useEffect(() => {
    if (!isTyping) {
      return;
    }
    let cancelled = false;
    const interval = setInterval(() => {
      setStatusVisible(false);
      setTimeout(() => {
        if (!cancelled) {
          setStatusIndex((prev) => (prev + 1) % STATUS_MESSAGES.length);
          setStatusVisible(true);
        }
      }, 300);
    }, 2500);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [isTyping]);

  const [randomizedSuggestions] = useState(() => [...SUGGESTIONS_POOL].sort(() => 0.5 - Math.random()).slice(0, 4));

  const { data: workspaces = [] } = useQuery({
    queryKey: ["workspaces"],
    queryFn: fetchWorkspaces,
    enabled: wsContextOpen,
  });

  const { data: expandedWorkspaceFiles = [] } = useQuery({
    queryKey: ["workspaceFiles", expandedWorkspace],
    queryFn: () => fetchWorkspaceFiles(expandedWorkspace!),
    enabled: !!expandedWorkspace,
  });

  const injectWorkspaceContext = (ws: Workspace) => {
    setContextPills((prev) => {
      if (prev.some((p) => p.id === ws.id)) {
        return prev;
      }
      return [...prev, { type: "workspace", id: ws.id, label: ws.name }];
    });
  };

  const injectFileContext = (fileName: string, fileId: string) => {
    setContextPills((prev) => {
      if (prev.some((p) => p.id === fileId)) {
        return prev;
      }
      return [...prev, { type: "file", id: fileId, label: fileName }];
    });
  };

  const removeContextPill = (id: string) => {
    setContextPills((prev) => prev.filter((p) => p.id !== id));
  };

  const copyMessage = (content: string, id: string) => {
    navigator.clipboard.writeText(content).then(() => {
      setCopiedMessageId(id);
      setTimeout(() => setCopiedMessageId(null), 2000);
    });
  };

  const resizeTextarea = () => {
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
      textareaRef.current.style.height = Math.min(textareaRef.current.scrollHeight, 160) + "px";
    }
  };

  const loadConversation = (conv: ConversationSummary) => {
    if (conv.id !== activeConversationId) {
      navigate(`/assistant/${conv.id}`);
    }
  };

  const createNewConversation = async (): Promise<string | null> => {
    const uniqueId = Math.random().toString(36).substring(2, 5);
    const title = `Chat ${new Date().toLocaleString("en-GB", {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false,
    })} ${uniqueId}`;
    try {
      const data = await createConversation({ title, model });
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
    setStatusIndex(0);
    setStatusVisible(true);
    void respondToApproval({ aiMsgId, requestId, approved, conversationId: conversationId ?? activeConversationId });
  };

  const submitMessage = async () => {
    const hasContent = input.trim() || contextPills.length > 0;
    if (!hasContent || isTyping) {
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

    const pillPrefix = contextPills
      .map((p) =>
        p.type === "workspace" ? `[Workspace: ${p.label} | ID: ${p.id}]` : `[File: ${p.label} | ID: ${p.id}]`,
      )
      .join(" ");
    const userContent = pillPrefix ? (input.trim() ? `${pillPrefix} ${input.trim()}` : pillPrefix) : input.trim();

    setInput("");
    setContextPills([]);
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
    }
    setStatusIndex(0);
    setStatusVisible(true);

    void sendMessage({ conversationId, model, userContent });
  };

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    submitMessage();
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      submitMessage();
    }
    if ((e.metaKey || e.ctrlKey) && e.key === "k") {
      e.preventDefault();
      createNewConversation();
    }
    if (e.key === "ArrowUp" && input === "") {
      const lastUserMsg = [...messages].reverse().find((m) => m.role === "user");
      if (lastUserMsg) {
        e.preventDefault();
        setInput(lastUserMsg.content);
        setTimeout(() => resizeTextarea(), 0);
      }
    }
  };

  const isEmpty = messages.length === 0 && !isTyping;
  const isLoadingConversation = !!routeConversationId && routeConversationId !== activeConversationId;
  const conversationGroups = groupConversationsByDate(conversations);

  const resizeTextareaOnInput = (value: string) => {
    setInput(value);
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
      textareaRef.current.style.height = Math.min(textareaRef.current.scrollHeight, 160) + "px";
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.chatWrapper}>
        <PageHeader
          icon={<BotMessageSquare size={20} className={styles.headerIcon} />}
          title="Assistant"
          subtitle="Chat with the Sentium assistant"
          right={
            <div className={styles.headerRight}>
              <button
                className={styles.sidebarToggle}
                onClick={() => setSidebarOpen((v) => !v)}
                title="Toggle conversations"
              >
                <ChevronRight
                  size={14}
                  style={{
                    transform: sidebarOpen ? "none" : "rotate(180deg)",
                    transition: "transform 0.2s",
                  }}
                />
              </button>
            </div>
          }
        />

        <div className={styles.chatArea} ref={chatAreaRef}>
          {isLoadingConversation ? (
            <div className={styles.loadingConversation}>
              <Loader size={22} className={styles.statusSpinner} />
              <span>Loading conversation…</span>
            </div>
          ) : isEmpty ? (
            <WelcomeScreen suggestions={randomizedSuggestions} onSelectSuggestion={(s) => setInput(s)} />
          ) : (
            <div className={styles.messagesArea}>
              {messages.map((msg) => (
                <MessageBubble
                  key={msg.id}
                  msg={msg}
                  isTyping={isTyping}
                  expandedThoughts={expandedThoughts}
                  statusIndex={statusIndex}
                  statusVisible={statusVisible}
                  copiedMessageId={copiedMessageId}
                  onToggleThought={toggleThought}
                  onCopyMessage={copyMessage}
                  onApproval={handleApproval}
                  onRetry={msg.error ? retryLastMessage : undefined}
                />
              ))}
              <div ref={messagesEndRef} />
            </div>
          )}
        </div>

        {!isAtBottom && (
          <button className={styles.scrollToBottomBtn} onClick={scrollToBottom} title="Scroll to bottom">
            <ChevronDown size={14} />
          </button>
        )}

        {sendError && <StatusMessage variant="error" message={sendError} />}

        <ChatInputBar
          input={input}
          isTyping={isTyping}
          contextPills={contextPills}
          model={model}
          models={models}
          onInputChange={resizeTextareaOnInput}
          onKeyDown={handleKeyDown}
          onSubmit={handleSubmit}
          onStop={handleStop}
          onRemoveContextPill={removeContextPill}
          onSetModel={setModel}
          textareaRef={textareaRef}
        />
      </div>

      <ConversationSidebar
        isOpen={sidebarOpen}
        conversations={conversations}
        conversationGroups={conversationGroups}
        activeConversationId={routeConversationId ?? activeConversationId}
        isCreating={isCreating}
        wsContextOpen={wsContextOpen}
        workspaces={workspaces}
        expandedWorkspace={expandedWorkspace}
        expandedWorkspaceFiles={expandedWorkspaceFiles}
        onNewConversation={createNewConversation}
        onLoadConversation={loadConversation}
        onDeleteConversation={deleteConversation}
        onToggleWsContext={() => setWsContextOpen((v) => !v)}
        onToggleExpandWorkspace={(wsId) => setExpandedWorkspace((v) => (v === wsId ? null : wsId))}
        onInjectWorkspaceContext={injectWorkspaceContext}
        onInjectFileContext={injectFileContext}
      />

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
    </div>
  );
};

export default Assistant;
