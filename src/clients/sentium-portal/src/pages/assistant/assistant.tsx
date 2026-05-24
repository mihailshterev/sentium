import React, { useState, useRef, useEffect, useCallback } from "react";
import styles from "./assistant.module.scss";
import { ChevronRight, Brain, ChevronDown } from "lucide-react";
import {
  fetchConversation,
  sendChatMessage,
  approveToolCall,
  fetchWorkspaces,
  fetchWorkspaceFiles,
} from "../../services/agentRuntime.service";
import useConversations from "../../hooks/useConversations";
import useModels from "../../hooks/useModels";
import type { ConversationMessage, ConversationSummary } from "../../types/assistant";
import type { Workspace } from "../../types/workspace";
import { useConversationStore } from "../../stores/assistant-conversation-store";
import { SUGGESTIONS_POOL } from "../../utils/constants";
import { useQuery } from "@tanstack/react-query";
import PageHeader from "../../components/ui/page-header";
import MessageBubble, { STATUS_MESSAGES } from "./components/message-bubble";
import WelcomeScreen from "./components/welcome-screen";
import ChatInputBar from "./components/chat-input-bar";
import ConversationSidebar from "./components/conversation-sidebar";

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
    setActiveConversation,
    appendMessage,
    updateLastMessage,
    clearPendingApproval,
    setModel,
    clearConversation,
  } = useConversationStore();

  const {
    conversations,
    createConversation,
    deleteConversation: deleteConversationMutate,
    isCreating,
  } = useConversations();

  const { models } = useModels();

  const [input, setInput] = useState("");
  const [isTyping, setIsTyping] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [expandedThoughts, setExpandedThoughts] = useState<Set<string>>(new Set());
  const [wsContextOpen, setWsContextOpen] = useState(false);
  const [expandedWorkspace, setExpandedWorkspace] = useState<string | null>(null);
  const [contextPills, setContextPills] = useState<ContextPill[]>([]);
  const [copiedMessageId, setCopiedMessageId] = useState<string | null>(null);
  const [isAtBottom, setIsAtBottom] = useState(true);
  const [statusIndex, setStatusIndex] = useState(0);
  const [statusVisible, setStatusVisible] = useState(true);

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
  const abortControllerRef = useRef<AbortController | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

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

  const loadConversation = async (conv: ConversationSummary) => {
    try {
      const data = await fetchConversation(conv.id);
      const loadedMessages: ConversationMessage[] = (data.messages ?? []).map(
        (m: {
          id: string;
          role: "user" | "assistant";
          content: string;
          timestamp: string;
          thought?: string;
          toolCalls?: string[];
        }) => ({
          id: m.id,
          role: m.role,
          content: m.content,
          thought: m.thought,
          toolCalls: m.toolCalls,
          timestamp: new Date(m.timestamp),
        }),
      );
      setActiveConversation(conv.id, loadedMessages, conv.model);
      setModel(conv.model);
    } catch {
      // non-blocking
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
      return data.id;
    } catch {
      return null;
    }
  };

  const deleteConversation = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    deleteConversationMutate(id, {
      onSuccess: () => {
        if (activeConversationId === id) {
          clearConversation();
        }
      },
    });
  };

  const handleStop = () => {
    abortControllerRef.current?.abort();
  };

  const handleApproval = async (aiMsgId: string, requestId: string, approved: boolean) => {
    clearPendingApproval(aiMsgId);
    setIsTyping(true);

    const controller = new AbortController();
    abortControllerRef.current = controller;

    try {
      const response = await approveToolCall(requestId, approved, controller.signal);

      if (!response.ok) {
        throw new Error("Approval response failed");
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder("utf-8");
      if (!reader) {
        throw new Error("Failed to read stream");
      }

      let leftover = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }

        const chunk = leftover + decoder.decode(value, { stream: true });
        const lines = chunk.split("\n");
        leftover = lines.pop() || "";

        for (const line of lines) {
          let trimmed = line.trim();
          if (!trimmed) {
            continue;
          }

          if (trimmed.startsWith("data: ")) {
            trimmed = trimmed.slice(6).trim();
          }

          if (!trimmed) {
            continue;
          }

          try {
            const parsed = JSON.parse(trimmed);

            if (parsed.type === "done") {
              break;
            }

            if (parsed.type === "error") {
              throw new Error(parsed.message || "Connection to AI node failed.");
            }

            const content = parsed.message?.content;
            if (parsed.type === "approval_request") {
              updateLastMessage(aiMsgId, content, "approval");
              setIsTyping(false);
              return;
            }
            if (content) {
              if (parsed.type === "thought") {
                updateLastMessage(aiMsgId, content, "thought");
              } else if (parsed.type === "tool") {
                updateLastMessage(aiMsgId, content, "tool");
              } else {
                updateLastMessage(aiMsgId, content, "content");
              }
            }
          } catch {
            // ignore parse errors
          }
        }
      }
    } catch (err) {
      if (err instanceof Error && err.name !== "AbortError") {
        updateLastMessage(aiMsgId, "\n\n_Error: Failed to process approval._");
      }
    } finally {
      setIsTyping(false);
      abortControllerRef.current = null;
    }
  };

  const submitMessage = async () => {
    const hasContent = input.trim() || contextPills.length > 0;
    if (!hasContent || isTyping) {
      return;
    }

    let conversationId = activeConversationId;
    if (!conversationId) {
      conversationId = await createNewConversation();
    }

    const pillPrefix = contextPills
      .map((p) =>
        p.type === "workspace" ? `[Workspace: ${p.label} | ID: ${p.id}]` : `[File: ${p.label} | ID: ${p.id}]`,
      )
      .join(" ");
    const userContent = pillPrefix ? (input.trim() ? `${pillPrefix} ${input.trim()}` : pillPrefix) : input.trim();

    const userMsg: ConversationMessage = {
      id: Date.now().toString(),
      role: "user",
      content: userContent,
      timestamp: new Date(),
    };

    appendMessage(userMsg);
    setInput("");
    setContextPills([]);
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
    }
    setStatusIndex(0);
    setStatusVisible(true);
    setIsTyping(true);

    const aiMsgId = (Date.now() + 1).toString();
    appendMessage({
      id: aiMsgId,
      role: "assistant",
      content: "",
      timestamp: new Date(),
    });

    const chatHistory = [...messages, userMsg].map((msg: ConversationMessage) => ({
      role: msg.role,
      content: msg.content,
    }));

    const controller = new AbortController();
    abortControllerRef.current = controller;

    try {
      const response = await sendChatMessage(
        {
          conversationId: conversationId ?? undefined,
          model,
          messages: chatHistory,
          stream: true,
        },
        controller.signal,
      );

      if (!response.ok) {
        throw new Error("Network response was not ok");
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder("utf-8");
      if (!reader) {
        throw new Error("Failed to read stream");
      }

      let leftover = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }

        const chunk = leftover + decoder.decode(value, { stream: true });
        const lines = chunk.split("\n");

        leftover = lines.pop() || "";

        for (const line of lines) {
          let trimmed = line.trim();
          if (!trimmed) {
            continue;
          }

          if (trimmed.startsWith("data: ")) {
            trimmed = trimmed.slice(6).trim();
          }

          if (!trimmed) {
            continue;
          }

          try {
            const parsed = JSON.parse(trimmed);

            if (parsed.type === "done") {
              break;
            }

            if (parsed.type === "error") {
              throw new Error(parsed.message || "Execution exception context");
            }

            const content = parsed.message?.content;

            if (parsed.type === "approval_request") {
              updateLastMessage(aiMsgId, content, "approval");
              setIsTyping(false);
              return;
            }

            if (content) {
              if (parsed.type === "thought") {
                updateLastMessage(aiMsgId, content, "thought");
              } else if (parsed.type === "tool") {
                updateLastMessage(aiMsgId, content, "tool");
              } else {
                updateLastMessage(aiMsgId, content, "content");
              }
            }
          } catch (err) {
            console.error("Stream parse error:", err);
          }
        }
      }
    } catch (err) {
      if (err instanceof Error && err.name === "AbortError") {
        // User stopped generation — not an error
      } else {
        updateLastMessage(aiMsgId, "\n\n_Error: Connection to AI node failed._");
      }
    } finally {
      setIsTyping(false);
      abortControllerRef.current = null;
    }
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
          icon={<Brain size={20} className={styles.headerIcon} />}
          title="Assistant"
          subtitle="Chat with the Sentium assistant"
          right={
            <div className={styles.headerRight}>
              <div className={styles.statusBadge}>
                <span className={styles.statusDot} />
                <span className={styles.statusText}>{model || "No model selected"}</span>
              </div>
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
          {isEmpty ? (
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

        <ChatInputBar
          input={input}
          isTyping={isTyping}
          contextPills={contextPills}
          onInputChange={resizeTextareaOnInput}
          onKeyDown={handleKeyDown}
          onSubmit={handleSubmit}
          onStop={handleStop}
          onRemoveContextPill={removeContextPill}
          textareaRef={textareaRef}
        />
      </div>

      <ConversationSidebar
        isOpen={sidebarOpen}
        conversations={conversations}
        conversationGroups={conversationGroups}
        activeConversationId={activeConversationId}
        model={model}
        models={models}
        isCreating={isCreating}
        wsContextOpen={wsContextOpen}
        workspaces={workspaces}
        expandedWorkspace={expandedWorkspace}
        expandedWorkspaceFiles={expandedWorkspaceFiles}
        onNewConversation={createNewConversation}
        onLoadConversation={loadConversation}
        onDeleteConversation={deleteConversation}
        onSetModel={setModel}
        onToggleWsContext={() => setWsContextOpen((v) => !v)}
        onToggleExpandWorkspace={(wsId) => setExpandedWorkspace((v) => (v === wsId ? null : wsId))}
        onInjectWorkspaceContext={injectWorkspaceContext}
        onInjectFileContext={injectFileContext}
      />
    </div>
  );
};

export default Assistant;
