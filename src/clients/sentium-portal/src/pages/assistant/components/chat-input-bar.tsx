import { useEffect, useRef, useState } from "react";
import { ArrowUp, Square, FolderOpen, FileText, X, ChevronDown, Check } from "lucide-react";
import styles from "../assistant.module.scss";

type ContextPill = { type: "workspace" | "file"; id: string; label: string };

interface ChatInputBarProps {
  input: string;
  isTyping: boolean;
  contextPills: ContextPill[];
  model: string;
  models: string[];
  onInputChange: (value: string) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  onStop: () => void;
  onRemoveContextPill: (id: string) => void;
  onSetModel: (model: string) => void;
  textareaRef: React.RefObject<HTMLTextAreaElement | null>;
}

const ChatInputBar = ({
  input,
  isTyping,
  contextPills,
  model,
  models,
  onInputChange,
  onKeyDown,
  onSubmit,
  onStop,
  onRemoveContextPill,
  onSetModel,
  textareaRef,
}: ChatInputBarProps) => {
  const [modelDropdownOpen, setModelDropdownOpen] = useState(false);
  const chipRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!modelDropdownOpen) {
      return;
    }
    const handleClickOutside = (e: MouseEvent) => {
      if (chipRef.current && !chipRef.current.contains(e.target as Node)) {
        setModelDropdownOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [modelDropdownOpen]);

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
            aria-label="Chat message"
            autoComplete="off"
            disabled={isTyping}
            rows={1}
          />
        </div>

        <div
          ref={chipRef}
          className={`${styles.inputModelChip} ${modelDropdownOpen ? styles.inputModelChipOpen : ""}`}
          onClick={() => models.length > 0 && setModelDropdownOpen((v) => !v)}
          onMouseDown={(e) => e.preventDefault()}
        >
          {models.length > 0 ? (
            <span className={styles.inputModelLabel}>{model || "Select model"}</span>
          ) : (
            <input
              type="text"
              className={styles.inputModelInput}
              value={model}
              onChange={(e) => onSetModel(e.target.value)}
              placeholder="model name..."
              autoComplete="off"
              spellCheck={false}
              onClick={(e) => e.stopPropagation()}
            />
          )}

          {models.length > 0 && (
            <ChevronDown
              size={10}
              className={`${styles.inputModelChevron} ${modelDropdownOpen ? styles.inputModelChevronOpen : ""}`}
            />
          )}

          {modelDropdownOpen && models.length > 0 && (
            <div className={`${styles.modelDropdown} ${styles.modelDropdownUp}`}>
              {[...models]
                .sort((a, b) => a.localeCompare(b))
                .map((m) => (
                  <button
                    key={m}
                    type="button"
                    className={`${styles.modelOption} ${m === model ? styles.modelOptionActive : ""}`}
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={(e) => {
                      e.stopPropagation();
                      onSetModel(m);
                      setModelDropdownOpen(false);
                    }}
                  >
                    <span className={styles.modelOptionCheck}>{m === model && <Check size={10} />}</span>
                    <span>{m}</span>
                  </button>
                ))}
            </div>
          )}
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

      <div className={styles.inputFooter}>
        Protected by Sentium Security Protocols &nbsp;·&nbsp; Ctrl+K: New chat &nbsp;·&nbsp; Enter: Send &nbsp;·&nbsp;
        Shift+Enter: New line
      </div>
    </div>
  );
};

export default ChatInputBar;
