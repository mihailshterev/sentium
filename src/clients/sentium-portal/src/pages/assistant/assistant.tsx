import React, { useState, useRef, useEffect, useCallback, useMemo } from "react";
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

  const toggleThought = (id: string) =>
    setExpandedThoughts((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const chatAreaRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const scrollToBottom = useCallback(() => {
    if (chatAreaRef.current) {
      chatAreaRef.current.scrollTop = chatAreaRef.current.scrollHeight;
    }
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, isTyping, scrollToBottom]);

  useEffect(() => {
    if (models.length > 0 && !model) {
      setModel(models[0]);
    }
  }, [models, model, setModel]);

  const randomizedSuggestions = useMemo(() => {
    return [...SUGGESTIONS_POOL].sort(() => 0.5 - Math.random()).slice(0, 4);
  }, []);

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
    setInput((prev) => {
      const ref = `[Workspace: ${ws.name} | ID: ${ws.id}]`;
      return prev ? `${ref} ${prev}` : ref + " ";
    });
  };

  const injectFileContext = (fileName: string, fileId: string) => {
    setInput((prev) => {
      const ref = `[File: ${fileName} | ID: ${fileId}]`;
      return prev ? `${ref} ${prev}` : ref + " ";
    });
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

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!input.trim() || isTyping) {
      return;
    }

    let conversationId = activeConversationId;
    if (!conversationId) {
      conversationId = await createNewConversation();
    }

    const userContent = input.trim();

    const userMsg: ConversationMessage = {
      id: Date.now().toString(),
      role: "user",
      content: userContent,
      timestamp: new Date(),
    };

    appendMessage(userMsg);
    setInput("");
    setIsTyping(true);

    const aiMsgId = (Date.now() + 1).toString();
    appendMessage({
      id: aiMsgId,
      role: "assistant",
      content: "",
      timestamp: new Date(),
    });

    const chatHistory = [...messages, userMsg].map((msg) => ({
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
        if (done) break;

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

  const formatTime = (date: Date) => date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", hour12: false });

  const formatDate = (dateStr: string) =>
    new Date(dateStr).toLocaleDateString("en-GB", { month: "short", day: "numeric" });

  const isEmpty = messages.length === 0 && !isTyping;

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

                return (
                  <div
                    key={msg.id}
                    className={`${styles.messageWrapper} ${msg.role === "user" ? styles.wrapperUser : styles.wrapperAi}`}
                  >
                    <div className={`${styles.message} ${msg.role === "user" ? styles.messageUser : styles.messageAi}`}>
                      <div className={styles.messageHeader}>
                        <span className={styles.sender}>{msg.role === "user" ? "YOU" : "SENTIUM"}</span>
                        <span className={styles.timestamp}>{formatTime(msg.timestamp)}</span>
                      </div>

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

                      {isTypingMsg ? (
                        <div className={styles.typingIndicator}>
                          <span></span>
                          <span></span>
                          <span></span>
                        </div>
                      ) : (
                        msg.content && (
                          <div className={styles.content}>
                            <Markdown>{msg.content}</Markdown>
                          </div>
                        )
                      )}
                    </div>
                  </div>
                );
              })}
              <div ref={messagesEndRef} />
            </div>
          )}
        </div>

        <div className={styles.inputContainer}>
          <form onSubmit={handleSubmit} className={styles.inputForm}>
            <button type="button" className={styles.attachBtn} title="Add attachment">
              <Plus size={16} />
            </button>
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder={isTyping ? "Generating..." : "Ask anything..."}
              className={styles.input}
              autoComplete="off"
              disabled={isTyping}
            />
            {isTyping ? (
              <button type="button" onClick={handleStop} className={styles.stopButton} title="Stop generation">
                <Square size={13} fill="currentColor" />
              </button>
            ) : (
              <button type="submit" disabled={!input.trim()} className={styles.sendButton}>
                <ArrowUp size={16} />
              </button>
            )}
          </form>
          <div className={styles.inputFooter}>Protected by Sentium Security Protocols</div>
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
          {conversations.map((conv) => (
            <div
              key={conv.id}
              className={`${styles.convItem} ${activeConversationId === conv.id ? styles.convItemActive : ""}`}
              onClick={() => loadConversation(conv)}
            >
              <MessageSquare size={12} className={styles.convIcon} />
              <div className={styles.convInfo}>
                <span className={styles.convTitle}>{conv.title}</span>
                <span className={styles.convDate}>{formatDate(conv.createdAt)}</span>
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
