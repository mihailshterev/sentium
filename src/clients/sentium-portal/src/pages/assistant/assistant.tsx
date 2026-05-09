import React, { useState, useRef, useEffect, useCallback } from "react";
import styles from "./assistant.module.scss";
import Markdown from "react-markdown";
import {
  Plus,
  Trash2,
  MessageSquare,
  ChevronRight,
  Cpu,
  Loader2,
  Brain,
  Wrench,
  ChevronDown,
  ArrowUp,
  Shield,
  Square,
  FolderOpen,
  FileText,
  Copy,
  Check,
  X,
} from "lucide-react";
import {
  fetchConversation,
  sendChatMessage,
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

const STATUS_MESSAGES = [
  "Consulting Sentium nodes...",
  "Analyzing context...",
  "Processing your query...",
  "Synthesizing response...",
  "Traversing knowledge graph...",
];

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
          const trimmed = line.trim();
          if (!trimmed) {
            continue;
          }

          try {
            const parsed = JSON.parse(trimmed);
            const content = parsed.message?.content;
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
            console.error("Stream parse error:", err, trimmed);
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

  return (
    <div className={styles.container}>
      <div className={styles.chatWrapper}>
        <header className={styles.header}>
          <div className={styles.headerLeft}>
            <div className={styles.headerIconWrap}>
              <Brain size={15} />
            </div>
            <span className={styles.headerTitle}>Assistant</span>
          </div>
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
        </header>

        <div className={styles.chatArea} ref={chatAreaRef}>
          {isEmpty ? (
            <div className={styles.welcomeScreen}>
              <div className={styles.welcomeIconWrap}>
                <Shield size={38} />
              </div>
              <h1 className={styles.welcomeTitle}>Good to See You!</h1>
              <h2 className={styles.welcomeSubtitle}>How Can I Assist You Today?</h2>
              <p className={styles.welcomeMeta}>I'm available 24/7 — ask me anything.</p>
              <div className={styles.suggestionRow}>
                {randomizedSuggestions.map((s) => (
                  <button key={s} className={styles.suggestionChip} onClick={() => setInput(s)}>
                    {s}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div className={styles.messagesArea}>
              {messages.map((msg) => {
                const isTypingMsg =
                  msg.id === messages[messages.length - 1]?.id &&
                  msg.role === "assistant" &&
                  msg.content === "" &&
                  isTyping;

                const showStatusCycler = isTypingMsg && !msg.thought && (!msg.toolCalls || msg.toolCalls.length === 0);

                return (
                  <div
                    key={msg.id}
                    className={`${styles.messageWrapper} ${msg.role === "user" ? styles.wrapperUser : styles.wrapperAi}`}
                  >
                    {msg.role === "assistant" && (
                      <div className={`${styles.avatar} ${styles.avatarAi}`}>
                        <Brain size={13} />
                      </div>
                    )}
                    <div className={`${styles.message} ${msg.role === "user" ? styles.messageUser : styles.messageAi}`}>
                      {msg.role === "assistant" && (
                        <div className={styles.messageHeader}>
                          <span className={styles.sender}>SENTIUM</span>
                        </div>
                      )}

                      {msg.thought !== undefined && msg.role === "assistant" && (
                        <div className={styles.thoughtBlock}>
                          <button className={styles.thoughtHeader} onClick={() => toggleThought(msg.id)}>
                            <Brain size={11} />
                            <span>Thinking</span>
                            <ChevronDown
                              size={11}
                              className={`${styles.thoughtChevron} ${expandedThoughts.has(msg.id) ? styles.thoughtChevronOpen : ""}`}
                            />
                          </button>
                          {expandedThoughts.has(msg.id) && (
                            <div className={styles.thoughtContent}>
                              <Markdown>{msg.thought}</Markdown>
                            </div>
                          )}
                        </div>
                      )}

                      {msg.toolCalls && msg.toolCalls.length > 0 && msg.role === "assistant" && (
                        <div className={styles.toolCallList}>
                          {msg.toolCalls.map((call, i) => (
                            <div key={i} className={styles.toolCallRow}>
                              <Wrench size={10} />
                              <span>{call}</span>
                            </div>
                          ))}
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
                            className={`${styles.typingStatusText} ${
                              statusVisible ? styles.statusVisible : styles.statusHidden
                            }`}
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
                      {msg.role === "assistant" && !isTypingMsg && msg.content && (
                        <div className={styles.messageFooter}>
                          <button
                            className={styles.copyBtn}
                            onClick={() => copyMessage(msg.content, msg.id)}
                            title="Copy to clipboard"
                          >
                            {copiedMessageId === msg.id ? <Check size={12} /> : <Copy size={12} />}
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
              <div ref={messagesEndRef} />
            </div>
          )}
        </div>

        {!isAtBottom && (
          <button className={styles.scrollToBottomBtn} onClick={scrollToBottom} title="Scroll to bottom">
            <ChevronDown size={14} />
          </button>
        )}

        <div className={styles.inputContainer}>
          <form onSubmit={handleSubmit} className={styles.inputForm}>
            <div className={styles.inputMain}>
              {contextPills.length > 0 && (
                <div className={styles.contextPillsRow}>
                  {contextPills.map((pill) => (
                    <div key={pill.id} className={styles.contextPill}>
                      {pill.type === "workspace" ? <FolderOpen size={9} /> : <FileText size={9} />}
                      <span>{pill.label}</span>
                      <button type="button" onClick={() => removeContextPill(pill.id)} aria-label="Remove">
                        <X size={9} />
                      </button>
                    </div>
                  ))}
                </div>
              )}
              <textarea
                ref={textareaRef}
                value={input}
                onChange={(e) => {
                  setInput(e.target.value);
                  resizeTextarea();
                }}
                onKeyDown={handleKeyDown}
                placeholder={isTyping ? "Generating..." : "Ask Sentium Assistant..."}
                className={styles.textarea}
                autoComplete="off"
                disabled={isTyping}
                rows={1}
              />
            </div>
            {isTyping ? (
              <button type="button" onClick={handleStop} className={styles.stopButton} title="Stop generation">
                <Square size={13} fill="currentColor" />
              </button>
            ) : (
              <button type="submit" disabled={!input.trim() && contextPills.length === 0} className={styles.sendButton}>
                <ArrowUp size={16} />
              </button>
            )}
          </form>
          <div className={styles.inputFooter}>
            Protected by Sentium Security Protocols &nbsp;·&nbsp; Ctrl+K: New chat
          </div>
        </div>
      </div>

      <aside className={`${styles.sidebar} ${sidebarOpen ? styles.sidebarOpen : ""}`}>
        <div className={styles.sidebarHeader}>
          <span className={styles.sidebarTitle}>Conversations</span>
          <button
            className={styles.newChatBtn}
            onClick={createNewConversation}
            title="New conversation"
            disabled={isCreating}
          >
            {isCreating ? <Loader2 size={13} /> : <Plus size={13} />}
          </button>
        </div>

        <div className={styles.modelSelector}>
          <Cpu size={12} />
          {models.length > 0 ? (
            <select className={styles.modelSelect} value={model} onChange={(e) => setModel(e.target.value)}>
              {models.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          ) : (
            <input
              className={styles.modelInput}
              value={model}
              onChange={(e) => setModel(e.target.value)}
              placeholder="model name..."
            />
          )}
        </div>

        <div className={styles.convList}>
          {conversations.length === 0 && (
            <div className={styles.convEmpty}>
              <MessageSquare size={22} className={styles.convEmptyIcon} />
              <span>No conversations yet</span>
            </div>
          )}
          {conversationGroups.map((group) => (
            <div key={group.label}>
              <div className={styles.convGroupLabel}>{group.label}</div>
              {group.items.map((conv) => (
                <div
                  key={conv.id}
                  className={`${styles.convItem} ${activeConversationId === conv.id ? styles.convItemActive : ""}`}
                  onClick={() => loadConversation(conv)}
                >
                  <MessageSquare size={12} className={styles.convIcon} />
                  <div className={styles.convInfo}>
                    <span className={styles.convTitle}>{conv.title}</span>
                  </div>
                  <button
                    className={styles.convDelete}
                    onClick={(e) => deleteConversation(conv.id, e)}
                    title="Delete conversation"
                  >
                    <Trash2 size={11} />
                  </button>
                </div>
              ))}
            </div>
          ))}
        </div>

        <div className={styles.wsContextSection}>
          <button className={styles.wsContextToggle} onClick={() => setWsContextOpen((v) => !v)}>
            <FolderOpen size={11} />
            <span>Workspace Context</span>
            <ChevronDown size={11} className={`${styles.wsChevron} ${wsContextOpen ? styles.wsChevronOpen : ""}`} />
          </button>
          {wsContextOpen && (
            <div className={styles.wsContextList}>
              {workspaces.length === 0 && <p className={styles.wsContextEmpty}>No workspaces found.</p>}
              {workspaces.map((ws) => (
                <div key={ws.id} className={styles.wsContextItem}>
                  <button
                    className={styles.wsContextName}
                    onClick={() => setExpandedWorkspace((v) => (v === ws.id ? null : ws.id))}
                    title="Expand workspace files"
                  >
                    <FolderOpen size={11} className={styles.wsContextIcon} />
                    <span>{ws.name}</span>
                    <ChevronRight
                      size={10}
                      className={`${styles.wsExpandChevron} ${expandedWorkspace === ws.id ? styles.wsExpandChevronOpen : ""}`}
                    />
                  </button>
                  <button
                    className={styles.wsInjectBtn}
                    onClick={() => injectWorkspaceContext(ws)}
                    title="Insert workspace reference into message"
                  >
                    +
                  </button>
                  {expandedWorkspace === ws.id && (
                    <div className={styles.wsFileList}>
                      {expandedWorkspaceFiles.length === 0 && <p className={styles.wsContextEmpty}>No files.</p>}
                      {expandedWorkspaceFiles.map((f) => (
                        <button
                          key={f.id}
                          className={styles.wsFileItem}
                          onClick={() => injectFileContext(f.fileName, f.id)}
                          title={`Insert file reference: ${f.fileName}`}
                          disabled={f.processingStatus !== "Completed"}
                        >
                          <FileText size={10} />
                          <span>{f.fileName}</span>
                          {f.processingStatus !== "Completed" && (
                            <span className={styles.wsFileStatus}>{f.processingStatus}</span>
                          )}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </aside>
    </div>
  );
};

export default Assistant;
