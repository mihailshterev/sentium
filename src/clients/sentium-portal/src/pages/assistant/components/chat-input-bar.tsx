import { ArrowUp, Square, FolderOpen, FileText, X } from "lucide-react";
import styles from "../assistant.module.scss";

type ContextPill = { type: "workspace" | "file"; id: string; label: string };

interface ChatInputBarProps {
  input: string;
  isTyping: boolean;
  contextPills: ContextPill[];
  onInputChange: (value: string) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  onStop: () => void;
  onRemoveContextPill: (id: string) => void;
  textareaRef: React.RefObject<HTMLTextAreaElement | null>;
}

const ChatInputBar = ({
  input,
  isTyping,
  contextPills,
  onInputChange,
  onKeyDown,
  onSubmit,
  onStop,
  onRemoveContextPill,
  textareaRef,
}: ChatInputBarProps) => {
  return (
    <div className={styles.inputContainer}>
      <form onSubmit={onSubmit} className={styles.inputForm}>
        <div className={styles.inputMain}>
          {contextPills.length > 0 && (
            <div className={styles.contextPillsRow}>
              {contextPills.map((pill) => (
                <div key={pill.id} className={styles.contextPill}>
                  {pill.type === "workspace" ? <FolderOpen size={9} /> : <FileText size={9} />}
                  <span>{pill.label}</span>
                  <button type="button" onClick={() => onRemoveContextPill(pill.id)} aria-label="Remove">
                    <X size={9} />
                  </button>
                </div>
              ))}
            </div>
          )}
          <textarea
            ref={textareaRef}
            value={input}
            onChange={(e) => onInputChange(e.target.value)}
            onKeyDown={onKeyDown}
            placeholder={isTyping ? "Generating..." : "Ask Sentium Assistant..."}
            className={styles.textarea}
            autoComplete="off"
            disabled={isTyping}
            rows={1}
          />
        </div>
        {isTyping ? (
          <button type="button" onClick={onStop} className={styles.stopButton} title="Stop generation">
            <Square size={13} fill="currentColor" />
          </button>
        ) : (
          <button type="submit" disabled={!input.trim() && contextPills.length === 0} className={styles.sendButton}>
            <ArrowUp size={16} />
          </button>
        )}
      </form>
      <div className={styles.inputFooter}>Protected by Sentium Security Protocols &nbsp;·&nbsp; Ctrl+K: New chat</div>
    </div>
  );
};

export default ChatInputBar;
