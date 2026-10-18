import { useEffect, useRef, useState } from "react";
import { Check, ChevronDown } from "lucide-react";
import styles from "./model-selector.module.scss";

interface ModelSelectorProps {
  id?: string;
  className: string;
  models: string[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  variant?: "field" | "chip";
}

const ModelSelector = ({
  id,
  className,
  models,
  value,
  onChange,
  placeholder,
  disabled,
  variant = "field",
}: ModelSelectorProps) => {
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    const handler = (e: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [open]);

  if (variant === "field") {
    if (models.length > 0) {
      return (
        <select
          id={id}
          className={className}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
        >
          {[...models]
            .sort((a, b) => a.localeCompare(b))
            .map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
        </select>
      );
    }

    return (
      <input
        id={id}
        className={className}
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder ?? "e.g. gemma3:1b"}
        autoComplete="off"
        spellCheck={false}
        disabled={disabled}
      />
    );
  }

  const sorted = [...models].sort((a, b) => a.localeCompare(b));

  if (models.length === 0) {
    return (
      <div className={`${styles.wrapper} ${className}`}>
        <input
          id={id}
          className={styles.textInput}
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder ?? "e.g. gemma3:1b"}
          autoComplete="off"
          spellCheck={false}
          disabled={disabled}
        />
      </div>
    );
  }

  return (
    <div ref={wrapperRef} className={`${styles.wrapper} ${className}`}>
      <button
        id={id}
        type="button"
        className={`${styles.trigger} ${open ? styles.triggerOpen : ""}`}
        onClick={() => setOpen((v) => !v)}
        onMouseDown={(e) => e.preventDefault()}
        disabled={disabled}
      >
        {value ? (
          <span className={styles.triggerLabel}>{value}</span>
        ) : (
          <span className={styles.triggerPlaceholder}>{placeholder ?? "Select model"}</span>
        )}
        <ChevronDown size={10} className={`${styles.chevron} ${open ? styles.chevronOpen : ""}`} />
      </button>

      {open && (
        <div className={styles.menu}>
          {sorted.map((m) => (
            <button
              key={m}
              type="button"
              className={`${styles.option} ${m === value ? styles.optionActive : ""}`}
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => {
                onChange(m);
                setOpen(false);
              }}
            >
              <span className={m === value ? styles.checkVisible : styles.checkHidden}>
                <Check size={10} />
              </span>
              <span>{m}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
};

export default ModelSelector;
