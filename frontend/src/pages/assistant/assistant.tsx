import React, { useState, useRef, useEffect, useCallback } from "react";
import styles from "./assistant.module.scss";
import Markdown from "react-markdown";
import { Plus, Trash2, MessageSquare, ChevronRight, Cpu } from "lucide-react";
import { API_BASE } from "../../utils/constants";
import useConversation from "../../hooks/useConversation";
import type { ConversationMessage } from "../../providers/conversation-context";
import type { ConversationSummary } from "../../types/assistant";

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
  } = useConversation();

  const [input, setInput] = useState("");
  const [isTyping, setIsTyping] = useState(false);
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [models, setModels] = useState<string[]>([]);

  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, isTyping, scrollToBottom]);

  const fetchConversations = useCallback(async () => {
    try {
      const res = await fetch(`${API_BASE}/agent-runtime/conversations`);
      if (!res.ok) {
        return;
      }
      const data: ConversationSummary[] = await res.json();
      setConversations(data);
    } catch {
      // non-blocking
    }
  }, []);

  const fetchModels = useCallback(async () => {
    try {
      const res = await fetch(`${API_BASE}/agent-runtime/assistant/models`);
      if (!res.ok) {
        return;
      }
      const data: string[] = await res.json();
      setModels(data);
      if (data.length > 0 && !model) {
        setModel(data[0]);
      }
    } catch {
      // non-blocking
    }
  }, [model, setModel]);

  useEffect(() => {
    fetchConversations();
    fetchModels();
  }, [fetchConversations, fetchModels]);

  const loadConversation = async (conv: ConversationSummary) => {
    try {
      const res = await fetch(`${API_BASE}/agent-runtime/conversations/${conv.id}`);
      if (!res.ok) {
        return;
      }
      const data = await res.json();
      const loadedMessages: ConversationMessage[] = (data.messages ?? []).map(
        (m: { id: string; role: "user" | "assistant"; content: string; timestamp: string }) => ({
          id: m.id,
          role: m.role,
          content: m.content,
          timestamp: new Date(m.timestamp),
        }),
      );
      setActiveConversation(conv.id, loadedMessages, conv.model);
      setModel(conv.model);
    } catch {
      // non-blocking
    }
  };

  const createNewConversation = async () => {
    const title = `Chat ${new Date().toLocaleString("en-US", {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    })}`;
    try {
      const res = await fetch(`${API_BASE}/agent-runtime/conversations`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ title, model }),
      });
      if (!res.ok) {
        return null;
      }
      const data = await res.json();
      await fetchConversations();
      setActiveConversation(data.id, [], model);
      return data.id as string;
    } catch {
      return null;
    }
  };

  const deleteConversation = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await fetch(`${API_BASE}/agent-runtime/conversations/${id}`, { method: "DELETE" });
      await fetchConversations();
      if (activeConversationId === id) {
        clearConversation();
      }
    } catch {
      // non-blocking
    }
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

    try {
      const response = await fetch(`${API_BASE}/agent-runtime/assistant/chat`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          conversationId: conversationId ?? undefined,
          model,
          messages: chatHistory,
          stream: true,
        }),
      });

      if (!response.ok) {
        throw new Error("Network response was not ok");
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder("utf-8");
      if (!reader) {
        throw new Error("Failed to read stream");
      }

      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }
        const chunk = decoder.decode(value, { stream: true });
        const lines = chunk.split("\n").filter(Boolean);
        for (const line of lines) {
          try {
            const parsed = JSON.parse(line);
            if (parsed.message?.content) {
              updateLastMessage(aiMsgId, parsed.message.content);
            }
          } catch {
            // non-JSON line
          }
        }
      }
    } catch {
      updateLastMessage(aiMsgId, "\n\n_Error: Connection to Ollama node failed._");
    } finally {
      setIsTyping(false);
    }
  };

  const formatTime = (date: Date) => date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

  const formatDate = (dateStr: string) =>
    new Date(dateStr).toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
    });

  return (
    <div className={styles.container}>
      <aside className={`${styles.sidebar} ${sidebarOpen ? styles.sidebarOpen : ""}`}>
        <div className={styles.sidebarHeader}>
          <span className={styles.sidebarTitle}>Conversations</span>
          <button className={styles.newChatBtn} onClick={createNewConversation} title="New conversation">
            <Plus size={13} />
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
      </aside>

      <div className={styles.chatWrapper}>
        <header className={styles.header}>
          <button
            className={styles.sidebarToggle}
            onClick={() => setSidebarOpen((v) => !v)}
            title="Toggle conversations"
          >
            <ChevronRight
              size={15}
              style={{
                transform: sidebarOpen ? "rotate(180deg)" : "none",
                transition: "transform 0.2s",
              }}
            />
          </button>
          <div className={styles.headerTitle}>
            <h1>Assistant Workspace</h1>
          </div>
          <p className={styles.subtitle}>
            {model} · {activeConversationId ? "conversation active" : "no active conversation"}
          </p>
        </header>

        <div className={styles.chatArea}>
          {messages.map((msg) => (
            <div
              key={msg.id}
              className={`${styles.messageWrapper} ${msg.role === "user" ? styles.wrapperUser : styles.wrapperAi}`}
            >
              <div className={`${styles.message} ${msg.role === "user" ? styles.messageUser : styles.messageAi}`}>
                <div className={styles.messageHeader}>
                  <span className={styles.sender}>{msg.role === "user" ? "YOU" : "SYSTEM"}</span>
                  <span className={styles.timestamp}>{formatTime(msg.timestamp)}</span>
                </div>

                <div className={styles.content}>
                  <Markdown>{msg.content}</Markdown>
                </div>
              </div>
            </div>
          ))}

          {isTyping && messages[messages.length - 1]?.content === "" && (
            <div className={`${styles.messageWrapper} ${styles.wrapperAi}`}>
              <div className={`${styles.message} ${styles.messageAi}`}>
                <div className={styles.typingIndicator}>
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
              </div>
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>

        <div className={styles.inputContainer}>
          <form onSubmit={handleSubmit} className={styles.inputForm}>
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder={`Query ${model}...`}
              className={styles.input}
              autoComplete="off"
              disabled={isTyping}
            />
            <button type="submit" disabled={!input.trim() || isTyping} className={styles.sendButton}>
              <svg
                width="20"
                height="20"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <line x1="22" y1="2" x2="11" y2="13"></line>
                <polygon points="22 2 15 22 11 13 2 9 22 2"></polygon>
              </svg>
            </button>
          </form>
          <div className={styles.inputFooter}>Protected by Sentium Security Protocols</div>
        </div>
      </div>
    </div>
  );
};

export default Assistant;
