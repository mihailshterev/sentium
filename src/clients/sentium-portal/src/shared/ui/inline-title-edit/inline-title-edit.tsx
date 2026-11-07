import { useEffect, useRef, useState } from "react";
import styles from "./inline-title-edit.module.css";

const MAX_TITLE_LENGTH = 255;

interface InlineTitleEditProps {
  value: string;
  ariaLabel: string;
  onSave: (title: string) => void;
  onCancel: () => void;
  className?: string;
}

export function InlineTitleEdit({ value, ariaLabel, onSave, onCancel, className }: InlineTitleEditProps) {
  const [draft, setDraft] = useState(value);
  const inputRef = useRef<HTMLInputElement>(null);
  const settledRef = useRef(false);

  useEffect(() => {
    inputRef.current?.focus();
    inputRef.current?.select();
  }, []);

  const settle = (commit: boolean) => {
    if (settledRef.current) {
      return;
    }
    settledRef.current = true;

    const title = draft.trim();
    if (commit && title && title !== value.trim()) {
      onSave(title);
    } else {
      onCancel();
    }
  };

  return (
    <input
      ref={inputRef}
      type="text"
      className={`${styles.input} ${className ?? ""}`}
      value={draft}
      maxLength={MAX_TITLE_LENGTH}
      aria-label={ariaLabel}
      onChange={(e) => setDraft(e.target.value)}
      onClick={(e) => e.stopPropagation()}
      onKeyDown={(e) => {
        e.stopPropagation();
        if (e.key === "Enter") {
          e.preventDefault();
          settle(true);
        } else if (e.key === "Escape") {
          e.preventDefault();
          settle(false);
        }
      }}
      onBlur={() => settle(true)}
    />
  );
}
